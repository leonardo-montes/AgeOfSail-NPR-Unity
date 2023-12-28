using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoS.RenderPipeline
{
	public class Shadows
	{
		private const int MaxCascades = 4;

		private static readonly GlobalKeyword[] DirectionalFilterKeywords =
		{
			GlobalKeyword.Create("_DIRECTIONAL_PCF3"),
			GlobalKeyword.Create("_DIRECTIONAL_PCF5"),
			GlobalKeyword.Create("_DIRECTIONAL_PCF7"),
		};

		private static readonly GlobalKeyword[] CascadeBlendKeywords =
		{
			GlobalKeyword.Create("_CASCADE_BLEND_SOFT"),
			GlobalKeyword.Create("_CASCADE_BLEND_DITHER"),
		};

		private static readonly GlobalKeyword UseShadowsKeyword = GlobalKeyword.Create("_USE_SHADOWS");

		private static readonly int DirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");
		private static readonly int DirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices");
		private static readonly int CascadeCountId = Shader.PropertyToID("_CascadeCount");
		private static readonly int CascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres");
		private static readonly int CascadeDataId = Shader.PropertyToID("_CascadeData");
		private static readonly int ShadowAtlastSizeId = Shader.PropertyToID("_ShadowAtlasSize");
		private static readonly int ShadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");
		private static readonly int ShadowPancakingId = Shader.PropertyToID("_ShadowPancaking");

		private static readonly Vector4[] CascadeCullingSpheres = new Vector4[MaxCascades];
		private static readonly Vector4[] CascadeData = new Vector4[MaxCascades];

		private static readonly Matrix4x4[] DirShadowMatrices = new Matrix4x4[MaxCascades];

		private struct ShadowedDirectionalLight
		{
			public int visibleLightIndex;
			public float slopeScaleBias;
			public float nearPlaneOffset;
		}

		private ShadowedDirectionalLight m_shadowedDirectionalLight;

		private CommandBuffer m_buffer;
		private ScriptableRenderContext m_context;
		private CullingResults m_cullingResults;
		private ShadowSettings m_settings;

		private Vector4 m_atlasSizes;

		private TextureHandle m_directionalAtlas;

		public void Setup(CullingResults cullingResults, ShadowSettings settings)
		{
			m_cullingResults = cullingResults;
			m_settings = settings;
		}

		public Vector4 ReserveDirectionalShadows(Light light)
		{
			if (light.shadows != LightShadows.None && light.shadowStrength > 0f)
			{
				if (!m_cullingResults.GetShadowCasterBounds(0, out _))
					return new Vector4(-light.shadowStrength, 0f, 0f, -1);

				m_shadowedDirectionalLight = new ShadowedDirectionalLight
				{
					visibleLightIndex = 0,
					slopeScaleBias = light.shadowBias,
					nearPlaneOffset = light.shadowNearPlane
				};
				return new Vector4(light.shadowStrength, 0, light.shadowNormalBias, -1);
			}
			return new Vector4(0f, 0f, 0f, -1f);
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
			m_directionalAtlas = builder.WriteTexture(renderGraph.CreateTexture(desc));
			return new ShadowTextures(m_directionalAtlas);
		}

		public void Render(RenderGraphContext context, bool render)
		{
			m_buffer = context.cmd;
			m_context = context.renderContext;

			RenderDirectionalShadows(render);

			m_buffer.SetGlobalTexture(DirShadowAtlasId, m_directionalAtlas);

			m_buffer.SetGlobalInt(CascadeCountId, m_settings.directional.cascadeCount);
			float f = 1.0f - m_settings.directional.cascadeFade;
			m_buffer.SetGlobalVector(ShadowDistanceFadeId, new Vector4(1.0f / m_settings.maxDistance, 1f / m_settings.distanceFade, 1.0f / (1.0f - f * f)));
			m_buffer.SetGlobalVector(ShadowAtlastSizeId, m_atlasSizes);
			ExecuteBuffer();
		}

		private void RenderDirectionalShadows(bool render)
		{
			int atlasSize = (int)m_settings.directional.atlasSize;
			m_atlasSizes.x = atlasSize;
			m_atlasSizes.y = 1f / atlasSize;
			m_buffer.SetRenderTarget(m_directionalAtlas, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
			m_buffer.ClearRenderTarget(true, false, Color.clear);
			m_buffer.SetGlobalFloat(ShadowPancakingId, 1f);
			m_buffer.BeginSample("Directional Shadows");
			ExecuteBuffer();

			if (render)
			{
				int tiles = m_settings.directional.cascadeCount;
				int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
				int tileSize = atlasSize / split;

				RenderDirectionalShadows(split, tileSize);

				m_buffer.SetGlobalVectorArray(CascadeCullingSpheresId, CascadeCullingSpheres);
				m_buffer.SetGlobalVectorArray(CascadeDataId, CascadeData);
				m_buffer.SetGlobalMatrixArray(DirShadowMatricesId, DirShadowMatrices);

				m_buffer.EnableKeyword(UseShadowsKeyword);
			}
			else
			{
				m_buffer.DisableKeyword(UseShadowsKeyword);
			}

			SetKeywords(DirectionalFilterKeywords, (int)m_settings.directional.filter - 1);
			SetKeywords(CascadeBlendKeywords, (int)m_settings.directional.cascadeBlend - 1);
			m_buffer.EndSample("Directional Shadows");
			ExecuteBuffer();
		}

		private void RenderDirectionalShadows(int split, int tileSize)
		{
			ShadowedDirectionalLight light = m_shadowedDirectionalLight;
			var shadowSettings = new ShadowDrawingSettings(m_cullingResults, light.visibleLightIndex, BatchCullingProjectionType.Orthographic)
			{
				useRenderingLayerMaskTest = true
			};
			int cascadeCount = m_settings.directional.cascadeCount;
			Vector3 ratios = m_settings.directional.CascadeRatios;
			float cullingFactor =
				Mathf.Max(0f, 0.8f - m_settings.directional.cascadeFade);
			float tileScale = 1f / split;
			for (int i = 0; i < cascadeCount; i++)
			{
				m_cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
					light.visibleLightIndex, i, cascadeCount, ratios, tileSize,
					light.nearPlaneOffset, out Matrix4x4 viewMatrix,
					out Matrix4x4 projectionMatrix, out ShadowSplitData splitData);
				splitData.shadowCascadeBlendCullingFactor = cullingFactor;
				shadowSettings.splitData = splitData;
				SetCascadeData(i, splitData.cullingSphere, tileSize);
				DirShadowMatrices[i] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix, SetTileViewport(i, split, tileSize), tileScale);
				m_buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
				m_buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
				ExecuteBuffer();
				m_context.DrawShadows(ref shadowSettings);
				m_buffer.SetGlobalDepthBias(0f, 0f);
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