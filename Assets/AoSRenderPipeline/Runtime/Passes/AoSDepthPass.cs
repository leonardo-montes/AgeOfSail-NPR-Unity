using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoS.RenderPipeline
{
	public class DepthPass
	{
		private static readonly ProfilingSampler Sampler = new("Depth Pass");

		private static readonly int DirLightDirectionAndMaskId = Shader.PropertyToID("_DirectionalLightDirectionAndMask");
		private static readonly int	DirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");

		private static Vector4 DirLightDirectionAndMask;
		private static Vector4 DirLightShadowData;
		private CullingResults m_cullingResults;
		private readonly Shadows m_shadows = new();
		private bool m_setupComplete = false;
		private Camera m_camera;

		public bool Setup(CullingResults cullingResults, ShadowSettings shadowSettings)
		{
			m_cullingResults = cullingResults;
			m_shadows.Setup(cullingResults, shadowSettings);
			return SetupLight();
		}

		private bool SetupLight()
		{
			NativeArray<VisibleLight> visibleLights = m_cullingResults.visibleLights;

			if (visibleLights.Length <= 0)
				return false;

			VisibleLight visibleLight = visibleLights[0];
			Light light = visibleLight.light;
			if (light.renderingLayerMask != 0)
			{
				switch (visibleLight.lightType)
				{
					case LightType.Directional:
						SetupDirectionalLight(ref visibleLight, light);
						break;
				}
			}

			return true;
		}

		private void Render(RenderGraphContext context)
		{
			CommandBuffer buffer = context.cmd;
			buffer.SetGlobalVector(DirLightDirectionAndMaskId, DirLightDirectionAndMask);
			buffer.SetGlobalVector(DirLightShadowDataId, DirLightShadowData);

			m_shadows.Render(context, m_setupComplete && m_camera.cameraType <= CameraType.SceneView);

			context.renderContext.ExecuteCommandBuffer(buffer);
			buffer.Clear();
		}

		private void SetupDirectionalLight(ref VisibleLight visibleLight, Light light)
		{
			Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
			dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
			DirLightDirectionAndMask = dirAndMask;
			DirLightShadowData = m_shadows.ReserveDirectionalShadows(light);
		}

		public static ShadowTextures Record(RenderGraph renderGraph, CullingResults cullingResults, Camera camera, ShadowSettings shadowSettings)
		{
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(Sampler.name, out DepthPass pass, Sampler);
			pass.m_camera = camera;
			pass.m_setupComplete = pass.Setup(cullingResults, shadowSettings);
			builder.SetRenderFunc<DepthPass>((pass, context) => pass.Render(context));
			builder.AllowPassCulling(false);
			return pass.m_shadows.GetRenderTextures(renderGraph, builder);
		}
	}
}