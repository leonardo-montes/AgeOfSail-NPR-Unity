using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoS.RenderPipeline
{
	/// <summary>
	/// Scene geometry rendering pass for rendering a single directional light and its shadows.
	/// 
	/// Based on 'https://bitbucket.org/catlikecoding-projects/custom-srp-project/src/master/'
	/// </summary>
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

		/// <summary>
		/// Setup the light and its shadows before rendering.
		/// </summary>
		/// <returns>Returns true if a light was indeed rendered.</returns>
		public bool Setup(CullingResults cullingResults, ShadowSettings shadowSettings)
		{
			m_cullingResults = cullingResults;
			m_shadows.Setup(cullingResults, shadowSettings);
			return SetupLight();
		}

		/// <summary>
		/// Setup the directional light if there is one.
		/// </summary>
		/// <returns>Returns true if there was a light to render.</returns>
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

		/// <summary>
		/// Render the shadows for the single directional light.
		/// </summary>
		private void Render(RenderGraphContext context)
		{
			CommandBuffer buffer = context.cmd;
			buffer.SetGlobalVector(DirLightDirectionAndMaskId, DirLightDirectionAndMask);
			buffer.SetGlobalVector(DirLightShadowDataId, DirLightShadowData);

			m_shadows.Render(context, m_setupComplete && m_camera.cameraType <= CameraType.SceneView);

			context.renderContext.ExecuteCommandBuffer(buffer);
			buffer.Clear();
		}

		/// <summary>
		/// Setup the directional light.
		/// </summary>
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

			// Force this pass to always execute even if there is no scene geometry to render.
			builder.AllowPassCulling(false);

			return pass.m_shadows.GetRenderTextures(renderGraph, builder);
		}
	}
}