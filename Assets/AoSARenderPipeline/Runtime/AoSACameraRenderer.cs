using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoSA.RenderPipeline
{
	public class CameraRenderer
	{
		public void Render(RenderGraph renderGraph, ScriptableRenderContext context, Camera camera, AoSARenderPipelineSettings settings)
		{
			// Setup profiler
			ProfilingSampler cameraSampler = ProfilingSampler.Get(camera.cameraType);
			
	#if UNITY_EDITOR
			// Emit scene view geometry
			if (camera.cameraType == CameraType.SceneView)
				ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
	#endif

			// Do culling
			if (!camera.TryGetCullingParameters(out ScriptableCullingParameters scriptableCullingParameters))
				return;

			// Get camera settings results
			Color overlayColor;
			float saturation;
			if (camera.TryGetComponent(out CameraSettings cameraSettings))
				cameraSettings.GetSettings(out overlayColor, out saturation);
			else
				CameraSettings.GetDefaultSettings(out overlayColor, out saturation);

			// Get culling results
			scriptableCullingParameters.shadowDistance = Mathf.Min(settings.shadows.maxDistance, camera.farClipPlane);
			CullingResults cullingResults = context.Cull(ref scriptableCullingParameters);

			// Get buffer settings (HDR, render resolution)
			bool useHDR = camera.allowHDR;
			Vector2Int bufferSize = new Vector2Int(camera.pixelWidth, camera.pixelHeight);

			// Setup the render graph
			var renderGraphParameters = new RenderGraphParameters
			{
				commandBuffer = CommandBufferPool.Get(),
				currentFrameIndex = Time.frameCount,
				executionName = cameraSampler.name,
				rendererListCulling = true,
				scriptableRenderContext = context
			};
			using (renderGraph.RecordAndExecute(renderGraphParameters))
			{
				using var _ = new RenderGraphProfilingScope(renderGraph, cameraSampler);

				// Lighting pass
				//  Process a the directional, point, and spot lights and their shadow cascades.
				ShadowTextures shadowTextures = LightingPass.Record(renderGraph, cullingResults, camera, settings, -1, out int totalLightCount, out Vector4[] lightColors);

				// Setup
				//  Register all the different textures and setup the camera for rendering.
				CameraRendererTextures textures = SetupPass.Record(renderGraph, useHDR, bufferSize, camera, settings, totalLightCount);

				// Warp pass
				//  Render the scene geometry with to a warp buffer, later used for warping the final texture
				//  and getting a rough edge effect.
				WarpPass.Record(renderGraph, camera, cullingResults, settings, textures);

				// Color Shadow pass
				//  Render the scene geometry and write lit color, shadowed color and lighting information.
				RendererListHandle listHandle = ColorShadowPass.Record(renderGraph, camera, cullingResults, textures, shadowTextures, lightColors);

				// Blur pass
				//  Blurs the shadow buffers generated in the previous pass two times to be used in the
				//  following passes (rounded shadows, inner glow in shadows, bloom).
				BlurPass.Record(renderGraph, bufferSize, useHDR, settings, textures, listHandle);

				// Compositing pass
				//  Generate the final lit color from the lit color, shadowed color and blurred buffers. Also extract bloom from the
				//  blurred buffers.
				CompositingPass.Record(renderGraph, bufferSize, useHDR, settings, textures, listHandle);

				// Skybox and transparent pass
				//  Render Unity's Skybox and the scene's transparent geometry. Useful for particle effects.
				SkyboxPass.Record(renderGraph, camera, textures.litColorBuffer, textures.depthAttachment);
				TransparentPass.Record(renderGraph, camera, cullingResults, textures.litColorBuffer, textures.depthAttachment);

				// Unsupported shaders pass
				//  Render the scene geometry that use an unsupported shader.
				UnsupportedShadersPass.Record(renderGraph, camera, cullingResults, textures.litColorBuffer, textures.depthAttachment);

				// Final compositing pass
				//  Warp the current render with the 'warp pass' and apply the bloom buffers from the 'Compositing pass'. Also apply an
				//  overlay filter and saturation.
				FinalCompositingPass.Record(renderGraph, camera, settings, overlayColor, saturation, textures);

				// Gizmos pass
				//  Render Unity's Gizmos.
				GizmosPass.Record(renderGraph, textures.depthAttachment, camera);
			}
			
			// Execute the previously created render graph.
			context.ExecuteCommandBuffer(renderGraphParameters.commandBuffer);
			context.Submit();
			CommandBufferPool.Release(renderGraphParameters.commandBuffer);
		}
	}
}