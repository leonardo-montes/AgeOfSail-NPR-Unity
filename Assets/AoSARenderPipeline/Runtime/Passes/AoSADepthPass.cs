using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoSA.RenderPipeline
{
	public class DepthPass
	{
		private static readonly ProfilingSampler Sampler = new("Depth Pass");

		private const int MaxLightCount = 9; // 8 max RT, 3 already used, so 5 RT * 4 channels - 1 (for thresholds) = 19
		private const int MaxDirLightCount = 4;
		private const int MaxOtherLightCount = 18;

		private static readonly int TotalLightCountId = Shader.PropertyToID("_TotalLightCount");
		private static readonly int LightColorsId = Shader.PropertyToID("_LightColors");

		private static Vector4[] LightColors = new Vector4[MaxLightCount];

		private static readonly int DirLightCountId = Shader.PropertyToID("_DirectionalLightCount");
		private static readonly int DirLightDirectionsAndMasksId = Shader.PropertyToID("_DirectionalLightDirectionsAndMasks");
		private static readonly int	DirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");

		private static Vector4[] DirLightDirectionsAndMasks = new Vector4[MaxDirLightCount];
		private static Vector4[] DirLightShadowData = new Vector4[MaxDirLightCount];

		private static readonly int OtherLightCountId = Shader.PropertyToID("_OtherLightCount");
		private static readonly int OtherLightPositionsId = Shader.PropertyToID("_OtherLightPositions");
		private static readonly int OtherLightDirectionsAndMasksId = Shader.PropertyToID("_OtherLightDirectionsAndMasks");
		private static readonly int OtherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles");
		private static readonly int OtherLightShadowDataId = Shader.PropertyToID("_OtherLightShadowData");

		private static readonly Vector4[] OtherLightPositions = new Vector4[MaxOtherLightCount];
		private static readonly Vector4[] OtherLightDirectionsAndMasks = new Vector4[MaxOtherLightCount];
		private static readonly Vector4[] OtherLightSpotAngles = new Vector4[MaxOtherLightCount];
		private static readonly Vector4[] OtherLightShadowData = new Vector4[MaxOtherLightCount];

		private CullingResults m_cullingResults;
		private readonly Shadows m_shadows = new();
		private int m_dirLightCount;
		private int m_otherLightCount;
		private int m_totalLightCount;

		public int Setup(CullingResults cullingResults, ShadowSettings shadowSettings, int renderingLayerMask)
		{
			m_cullingResults = cullingResults;
			m_shadows.Setup(cullingResults, shadowSettings);
			return SetupLight(renderingLayerMask);
		}

		private int SetupLight(int renderingLayerMask)
		{
			NativeArray<VisibleLight> visibleLights = m_cullingResults.visibleLights;

			int i;
			m_dirLightCount = m_otherLightCount = m_totalLightCount = 0;
			for (i = 0; i < visibleLights.Length; i++)
			{
				VisibleLight visibleLight = visibleLights[i];
				Light light = visibleLight.light;
				if ((light.renderingLayerMask & renderingLayerMask) != 0)
				{
					switch (visibleLight.lightType)
					{
						case LightType.Directional:
							if (m_totalLightCount < MaxLightCount && m_dirLightCount < MaxDirLightCount)
								SetupDirectionalLight(m_totalLightCount++, m_dirLightCount++, i, ref visibleLight, light);
							break;
						case LightType.Point:
							if (m_totalLightCount < MaxLightCount && m_otherLightCount < MaxOtherLightCount)
								SetupPointLight(m_totalLightCount++, m_otherLightCount++, i, ref visibleLight, light);
							break;
						case LightType.Spot:
							if (m_totalLightCount < MaxLightCount && m_otherLightCount < MaxOtherLightCount)
								SetupSpotLight(m_totalLightCount++, m_otherLightCount++, i, ref visibleLight, light);
							break;
					}
				}
			}

			return m_totalLightCount;
		}

		private void Render(RenderGraphContext context)
		{
			CommandBuffer buffer = context.cmd;

			buffer.SetGlobalInt(TotalLightCountId, m_totalLightCount);
			buffer.SetGlobalVectorArray(LightColorsId, LightColors);

			buffer.SetGlobalInt(DirLightCountId, m_dirLightCount);
			if (m_dirLightCount > 0)
			{
				buffer.SetGlobalVectorArray(DirLightDirectionsAndMasksId, DirLightDirectionsAndMasks);
				buffer.SetGlobalVectorArray(DirLightShadowDataId, DirLightShadowData);
			}

			buffer.SetGlobalInt(OtherLightCountId, m_otherLightCount);
			if (m_otherLightCount > 0)
			{
				buffer.SetGlobalVectorArray(OtherLightPositionsId, OtherLightPositions);
				buffer.SetGlobalVectorArray(OtherLightDirectionsAndMasksId, OtherLightDirectionsAndMasks);
				buffer.SetGlobalVectorArray(OtherLightSpotAnglesId, OtherLightSpotAngles);
				buffer.SetGlobalVectorArray(OtherLightShadowDataId, OtherLightShadowData);
			}

			m_shadows.Render(context);
			context.renderContext.ExecuteCommandBuffer(buffer);
			buffer.Clear();
		}

		private void SetupDirectionalLight(int totalIndex, int index, int visibleIndex, ref VisibleLight visibleLight, Light light)
		{
			LightColors[totalIndex] = GetLightColor(visibleLight);

			Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
			dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
			DirLightDirectionsAndMasks[index] = dirAndMask;
			DirLightShadowData[index] = m_shadows.ReserveDirectionalShadows(light, visibleIndex);
		}

		private void SetupPointLight(int totalIndex, int index, int visibleIndex, ref VisibleLight visibleLight, Light light)
		{
			LightColors[totalIndex] = GetLightColor(visibleLight);

			Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
			position.w = Mathf.Max(visibleLight.range, 0.00001f);
			OtherLightPositions[index] = position;
			OtherLightSpotAngles[index] = new Vector4(0f, 1f);
			Vector4 dirAndmask = Vector4.zero;
			dirAndmask.w = light.renderingLayerMask.ReinterpretAsFloat();
			OtherLightDirectionsAndMasks[index] = dirAndmask;
			OtherLightShadowData[index] = m_shadows.ReserveOtherShadows(light, visibleIndex);
		}

		void SetupSpotLight(int totalIndex, int index, int visibleIndex, ref VisibleLight visibleLight, Light light)
		{
			LightColors[totalIndex] = GetLightColor(visibleLight);

			Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
			position.w = Mathf.Max(visibleLight.range, 0.00001f);
			OtherLightPositions[index] = position;
			Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
			dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
			OtherLightDirectionsAndMasks[index] = dirAndMask;

			float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
			float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
			float angleRangeInv = 1.0f / Mathf.Max(innerCos - outerCos, 0.001f);
			OtherLightSpotAngles[index] = new Vector4(angleRangeInv, -outerCos * angleRangeInv);
			OtherLightShadowData[index] = m_shadows.ReserveOtherShadows(light, visibleIndex);
		}

		private Color GetLightColor(VisibleLight visibleLight)
		{
			Color color = visibleLight.finalColor;
			color.a = visibleLight.lightType == LightType.Directional ? 1.0f : 0.0f;
			return color;
		}

		public static ShadowTextures Record(RenderGraph renderGraph, CullingResults cullingResults, Camera camera, AoSARenderPipelineSettings settings,
			int renderingLayerMask, out int totalLightCount, out Vector4[] lightColors)
		{
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(Sampler.name, out DepthPass pass, Sampler);
			totalLightCount = pass.Setup(cullingResults, settings.shadows, renderingLayerMask);
			lightColors = LightColors;
			builder.SetRenderFunc<DepthPass>((pass, context) => pass.Render(context));
			builder.AllowPassCulling(false);
			return pass.m_shadows.GetRenderTextures(renderGraph, builder);
		}
	}
}