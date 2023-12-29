using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoSA.RenderPipeline
{
	public class Shadows
	{
		public const int MaxShadowedDirLightCount = 4;
		public const int MaxShadowedOtherLightCount = 16;
		private const int MaxCascades = 4;

		private static readonly GlobalKeyword[] DirectionalFilterKeywords =
		{
			GlobalKeyword.Create("_DIRECTIONAL_PCF3"),
			GlobalKeyword.Create("_DIRECTIONAL_PCF5"),
			GlobalKeyword.Create("_DIRECTIONAL_PCF7"),
		};

		private static readonly GlobalKeyword[] OtherFilterKeywords =
		{
			GlobalKeyword.Create("_OTHER_PCF3"),
			GlobalKeyword.Create("_OTHER_PCF5"),
			GlobalKeyword.Create("_OTHER_PCF7"),
		};

		private static readonly GlobalKeyword[] CascadeBlendKeywords =
		{
			GlobalKeyword.Create("_CASCADE_BLEND_SOFT"),
			GlobalKeyword.Create("_CASCADE_BLEND_DITHER"),
		};

		private static readonly int DirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");
		private static readonly int DirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices");
		private static readonly int OtherShadowAtlasId = Shader.PropertyToID("_OtherShadowAtlas");
		private static readonly int OtherShadowMatricesId = Shader.PropertyToID("_OtherShadowMatrices");
		private static readonly int OtherShadowTilesId = Shader.PropertyToID("_OtherShadowTiles");
		private static readonly int CascadeCountId = Shader.PropertyToID("_CascadeCount");
		private static readonly int CascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres");
		private static readonly int CascadeDataId = Shader.PropertyToID("_CascadeData");
		private static readonly int ShadowAtlastSizeId = Shader.PropertyToID("_ShadowAtlasSize");
		private static readonly int ShadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");
		private static readonly int ShadowPancakingId = Shader.PropertyToID("_ShadowPancaking");

		private static readonly Vector4[] CascadeCullingSpheres = new Vector4[MaxCascades];
		private static readonly Vector4[] CascadeData = new Vector4[MaxCascades];
		private static readonly Vector4[] OtherShadowTiles = new Vector4[MaxShadowedOtherLightCount];

		private static readonly Matrix4x4[] DirShadowMatrices = new Matrix4x4[MaxShadowedDirLightCount * MaxCascades];
		private static readonly Matrix4x4[] OtherShadowMatrices = new Matrix4x4[MaxShadowedOtherLightCount];

		private struct ShadowedDirectionalLight
		{
			public int visibleLightIndex;
			public float slopeScaleBias;
			public float nearPlaneOffset;
		}

		private ShadowedDirectionalLight[] m_shadowedDirectionalLights = new ShadowedDirectionalLight[MaxShadowedDirLightCount];

		private struct ShadowedOtherLight
		{
			public int visibleLightIndex;
			public float slopeScaleBias;
			public float normalBias;
			public bool isPoint;
		}

		private readonly ShadowedOtherLight[] m_shadowedOtherLights = new ShadowedOtherLight[MaxShadowedOtherLightCount];

		private int m_shadowedDirLightCount;
		private int m_shadowedOtherLightCount;

		private CommandBuffer m_buffer;
		private ScriptableRenderContext m_context;
		private CullingResults m_cullingResults;
		private ShadowSettings m_settings;

		private Vector4 m_atlasSizes;

		private TextureHandle m_directionalAtlas;
		private TextureHandle m_otherAtlas;

		public void Setup(CullingResults cullingResults, ShadowSettings settings)
		{
			m_cullingResults = cullingResults;
			m_settings = settings;
			m_shadowedDirLightCount = m_shadowedOtherLightCount = 0;
		}

		public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex)
		{
			if (m_shadowedDirLightCount < MaxShadowedDirLightCount && light.shadows != LightShadows.None && light.shadowStrength > 0.0f)
			{
				if (!m_cullingResults.GetShadowCasterBounds(visibleLightIndex, out _))
					return new Vector4(-light.shadowStrength, 0f, 0f, -1);

				m_shadowedDirectionalLights[m_shadowedDirLightCount] = new ShadowedDirectionalLight
					{
						visibleLightIndex = visibleLightIndex,
						slopeScaleBias = light.shadowBias,
						nearPlaneOffset = light.shadowNearPlane
					};
				return new Vector4(light.shadowStrength, m_settings.directional.cascadeCount * m_shadowedDirLightCount++, light.shadowNormalBias, -1);
			}
			return new Vector4(0f, 0f, 0f, -1f);
		}

		public Vector4 ReserveOtherShadows(Light light, int visibleLightIndex)
		{
			if (light.shadows == LightShadows.None || light.shadowStrength <= 0f)
				return new Vector4(0f, 0f, 0f, -1f);

			bool isPoint = light.type == LightType.Point;
			int newLightCount = m_shadowedOtherLightCount + (isPoint ? 6 : 1);
			if (newLightCount > MaxShadowedOtherLightCount || !m_cullingResults.GetShadowCasterBounds(visibleLightIndex, out _))
				return new Vector4(-light.shadowStrength, 0f, 0f, -1);

			m_shadowedOtherLights[m_shadowedOtherLightCount] = new ShadowedOtherLight
			{
				visibleLightIndex = visibleLightIndex,
				slopeScaleBias = light.shadowBias,
				normalBias = light.shadowNormalBias,
				isPoint = isPoint
			};

			var data = new Vector4(light.shadowStrength, m_shadowedOtherLightCount, isPoint ? 1.0f : 0.0f, -1);
			m_shadowedOtherLightCount = newLightCount;
			return data;
		}

		public ShadowTextures GetRenderTextures(RenderGraph renderGraph, RenderGraphBuilder builder)
		{
			int atlasSize = (int)m_settings.directional.atlasSize;
			var desc = new TextureDesc(atlasSize, atlasSize)
			{
				depthBufferBits = DepthBits.Depth32,
				isShadowMap = true,
				name = "Directional Shadow Atlas"
			};
			m_directionalAtlas = m_shadowedDirLightCount > 0 ? builder.WriteTexture(renderGraph.CreateTexture(desc)) : renderGraph.defaultResources.defaultShadowTexture;

			atlasSize = (int)m_settings.other.atlasSize;
			desc.width = desc.height = atlasSize;
			desc.name = "Other Shadow Atlas";
			m_otherAtlas = m_shadowedOtherLightCount > 0 ? builder.WriteTexture(renderGraph.CreateTexture(desc)) : renderGraph.defaultResources.defaultShadowTexture;
			return new ShadowTextures(m_directionalAtlas, m_otherAtlas);
		}

		public void Render(RenderGraphContext context)
		{
			m_buffer = context.cmd;
			m_context = context.renderContext;

			if (m_shadowedDirLightCount > 0)
				RenderDirectionalShadows();
			
			if (m_shadowedOtherLightCount > 0)
				RenderOtherShadows();

			m_buffer.SetGlobalTexture(DirShadowAtlasId, m_directionalAtlas);
			m_buffer.SetGlobalTexture(OtherShadowAtlasId, m_otherAtlas);

			m_buffer.SetGlobalInt(CascadeCountId, m_shadowedDirLightCount > 0 ? m_settings.directional.cascadeCount : 0);
			float f = 1f - m_settings.directional.cascadeFade;
			m_buffer.SetGlobalVector(ShadowDistanceFadeId, new Vector4(1.0f / m_settings.maxDistance, 1.0f / m_settings.distanceFade, 1.0f / (1.0f - f * f)));
			m_buffer.SetGlobalVector(ShadowAtlastSizeId, m_atlasSizes);
			ExecuteBuffer();
		}

		private void RenderDirectionalShadows()
		{
			int atlasSize = (int)m_settings.directional.atlasSize;
			m_atlasSizes.x = atlasSize;
			m_atlasSizes.y = 1.0f / atlasSize;
			m_buffer.SetRenderTarget(m_directionalAtlas, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
			m_buffer.ClearRenderTarget(true, false, Color.clear);
			m_buffer.SetGlobalFloat(ShadowPancakingId, 1f);
			m_buffer.BeginSample("Directional Shadows");
			ExecuteBuffer();

			int tiles = m_shadowedDirLightCount * m_settings.directional.cascadeCount;
			int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
			int tileSize = atlasSize / split;

			for (int i = 0; i < m_shadowedDirLightCount; i++)
			{
				RenderDirectionalShadows(i, split, tileSize);
			}

			m_buffer.SetGlobalVectorArray(CascadeCullingSpheresId, CascadeCullingSpheres);
			m_buffer.SetGlobalVectorArray(CascadeDataId, CascadeData);
			m_buffer.SetGlobalMatrixArray(DirShadowMatricesId, DirShadowMatrices);
			SetKeywords(DirectionalFilterKeywords, (int)m_settings.directional.filter - 1);
			SetKeywords(CascadeBlendKeywords, (int)m_settings.directional.cascadeBlend - 1);
			m_buffer.EndSample("Directional Shadows");
			ExecuteBuffer();
		}

		private void RenderDirectionalShadows(int index, int split, int tileSize)
		{
			ShadowedDirectionalLight light = m_shadowedDirectionalLights[index];
			var shadowSettings = new ShadowDrawingSettings(m_cullingResults, light.visibleLightIndex, BatchCullingProjectionType.Orthographic)
			{
				useRenderingLayerMaskTest = true
			};
			int cascadeCount = m_settings.directional.cascadeCount;
			int tileOffset = index * cascadeCount;
			Vector3 ratios = m_settings.directional.CascadeRatios;
			float cullingFactor =
				Mathf.Max(0f, 0.8f - m_settings.directional.cascadeFade);
			float tileScale = 1.0f / split;
			for (int i = 0; i < cascadeCount; i++)
			{
				m_cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
					light.visibleLightIndex, i, cascadeCount, ratios, tileSize,
					light.nearPlaneOffset, out Matrix4x4 viewMatrix,
					out Matrix4x4 projectionMatrix, out ShadowSplitData splitData);
				splitData.shadowCascadeBlendCullingFactor = cullingFactor;
				shadowSettings.splitData = splitData;
				if (index == 0)
				{
					SetCascadeData(i, splitData.cullingSphere, tileSize);
				}
				int tileIndex = tileOffset + i;
				DirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix, SetTileViewport(tileIndex, split, tileSize), tileScale);
				m_buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
				m_buffer.SetGlobalDepthBias(0.0f, light.slopeScaleBias);
				ExecuteBuffer();
				m_context.DrawShadows(ref shadowSettings);
				m_buffer.SetGlobalDepthBias(0.0f, 0.0f);
			}
		}

		private void SetCascadeData(int index, Vector4 cullingSphere, float tileSize)
		{
			float texelSize = 2.0f * cullingSphere.w / tileSize;
			float filterSize = texelSize * ((float)m_settings.directional.filter + 1.0f);
			cullingSphere.w -= filterSize;
			cullingSphere.w *= cullingSphere.w;
			CascadeCullingSpheres[index] = cullingSphere;
			CascadeData[index] = new Vector4(1.0f / cullingSphere.w, filterSize * 1.4142136f);
		}

		private void RenderOtherShadows()
		{
			int atlasSize = (int)m_settings.other.atlasSize;
			m_atlasSizes.z = atlasSize;
			m_atlasSizes.w = 1.0f / atlasSize;
			m_buffer.SetRenderTarget(m_otherAtlas, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
			m_buffer.ClearRenderTarget(true, false, Color.clear);
			m_buffer.SetGlobalFloat(ShadowPancakingId, 0.0f);
			m_buffer.BeginSample("Other Shadows");
			ExecuteBuffer();

			int tiles = m_shadowedOtherLightCount;
			int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
			int tileSize = atlasSize / split;

			for (int i = 0; i < m_shadowedOtherLightCount;)
			{
				if (m_shadowedOtherLights[i].isPoint)
				{
					RenderPointShadows(i, split, tileSize);
					i += 6;
				}
				else
				{
					RenderSpotShadows(i, split, tileSize);
					i += 1;
				}
			}

			m_buffer.SetGlobalMatrixArray(OtherShadowMatricesId, OtherShadowMatrices);
			m_buffer.SetGlobalVectorArray(OtherShadowTilesId, OtherShadowTiles);
			SetKeywords(OtherFilterKeywords, (int)m_settings.other.filter - 1);
			m_buffer.EndSample("Other Shadows");
			ExecuteBuffer();
		}

		private void RenderSpotShadows(int index, int split, int tileSize)
		{
			ShadowedOtherLight light = m_shadowedOtherLights[index];
			var shadowSettings = new ShadowDrawingSettings(m_cullingResults, light.visibleLightIndex, BatchCullingProjectionType.Perspective)
			{
				useRenderingLayerMaskTest = true
			};
			m_cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(
				light.visibleLightIndex, out Matrix4x4 viewMatrix,
				out Matrix4x4 projectionMatrix, out ShadowSplitData splitData);
			shadowSettings.splitData = splitData;
			float texelSize = 2f / (tileSize * projectionMatrix.m00);
			float filterSize = texelSize * ((float)m_settings.other.filter + 1f);
			float bias = light.normalBias * filterSize * 1.4142136f;
			Vector2 offset = SetTileViewport(index, split, tileSize);
			float tileScale = 1f / split;
			SetOtherTileData(index, offset, tileScale, bias);
			OtherShadowMatrices[index] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix, offset, tileScale);

			m_buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
			m_buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
			ExecuteBuffer();
			m_context.DrawShadows(ref shadowSettings);
			m_buffer.SetGlobalDepthBias(0f, 0f);
		}

		private void RenderPointShadows(int index, int split, int tileSize)
		{
			ShadowedOtherLight light = m_shadowedOtherLights[index];
			var shadowSettings = new ShadowDrawingSettings(m_cullingResults, light.visibleLightIndex, BatchCullingProjectionType.Perspective)
			{
				useRenderingLayerMaskTest = true
			};
			float texelSize = 2.0f / tileSize;
			float filterSize = texelSize * ((float)m_settings.other.filter + 1.0f);
			float bias = light.normalBias * filterSize * 1.4142136f;
			float tileScale = 1.0f / split;
			float fovBias = Mathf.Atan(1.0f + bias + filterSize) * Mathf.Rad2Deg * 2.0f - 90.0f;
			for (int i = 0; i < 6; i++)
			{
				m_cullingResults.ComputePointShadowMatricesAndCullingPrimitives(
					light.visibleLightIndex, (CubemapFace)i, fovBias,
					out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
					out ShadowSplitData splitData);
				viewMatrix.m11 = -viewMatrix.m11;
				viewMatrix.m12 = -viewMatrix.m12;
				viewMatrix.m13 = -viewMatrix.m13;

				shadowSettings.splitData = splitData;
				int tileIndex = index + i;
				Vector2 offset = SetTileViewport(tileIndex, split, tileSize);
				SetOtherTileData(tileIndex, offset, tileScale, bias);
				OtherShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix, offset, tileScale);

				m_buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
				m_buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
				ExecuteBuffer();
				m_context.DrawShadows(ref shadowSettings);
				m_buffer.SetGlobalDepthBias(0f, 0f);
			}
		}

		private void SetOtherTileData(int index, Vector2 offset, float scale, float bias)
		{
			float border = m_atlasSizes.w * 0.5f;
			Vector4 data;
			data.x = offset.x * scale + border;
			data.y = offset.y * scale + border;
			data.z = scale - border - border;
			data.w = bias;
			OtherShadowTiles[index] = data;
		}

		private Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, float scale)
		{
			if (SystemInfo.usesReversedZBuffer)
			{
				m.m20 = -m.m20;
				m.m21 = -m.m21;
				m.m22 = -m.m22;
				m.m23 = -m.m23;
			}
			m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
			m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
			m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
			m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
			m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
			m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
			m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
			m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
			m.m20 = 0.5f * (m.m20 + m.m30);
			m.m21 = 0.5f * (m.m21 + m.m31);
			m.m22 = 0.5f * (m.m22 + m.m32);
			m.m23 = 0.5f * (m.m23 + m.m33);
			return m;
		}

		private Vector2 SetTileViewport(int index, int split, float tileSize)
		{
			var offset = new Vector2(index % split, index / split);
			m_buffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
			return offset;
		}

		private void SetKeywords(GlobalKeyword[] keywords, int enabledIndex)
		{
			for (int i = 0; i < keywords.Length; i++)
			{
				m_buffer.SetKeyword(keywords[i], i == enabledIndex);
			}
		}

		private void ExecuteBuffer()
		{
			m_context.ExecuteCommandBuffer(m_buffer);
			m_buffer.Clear();
		}
	}
}