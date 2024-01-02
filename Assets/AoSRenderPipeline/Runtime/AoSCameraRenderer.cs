using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering;

namespace AoS.RenderPipeline
{
	public class CameraRenderer
	{
		public void Render(RenderGraph renderGraph, ScriptableRenderContext context, Camera camera, AoSRenderPipelineSettings settings)
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

				// 'Depth pass'
				//  Process a single directional light and its shadow cascades.
				ShadowTextures shadowTextures = DepthPass.Record(renderGraph, cullingResults, camera, settings.shadows);

				// Setup
				//  Register all the different textures and setup the camera for rendering.
				CameraRendererTextures textures = SetupPass.Record(renderGraph, useHDR, bufferSize, camera, settings);

				// 'Warp pass'
				//  Render the scene geometry with to a warp buffer, later used for warping the final texture
				//  and getting a rough edge effect.
				WarpPass.Record(renderGraph, camera, cullingResults, settings, textures);

				// 'Shadow pass'
				//  Render the scene geometry and process lighting.
				RendererListHandle listHandle = ShadowPass.Record(renderGraph, camera, cullingResults, textures, shadowTextures);

				// 'Blur pass'
				//  Blurs the previous 'Shadow pass' two times to be used in the following pass (rounded shadows, inner glow in shadows, bloom).
				BlurPass.Record(renderGraph, bufferSize, useHDR, settings, textures, listHandle);

				// 'Final shadow pass'
				//  Composite the previous 'blur pass' textures and 'shadow pass' texture into a single texture.
				FinalShadowPass.Record(renderGraph, settings, textures);

				// 'Color pass'
				//  Render the scene geometry and use the 'Final shadow pass' result to lerp between lit and shadowed textures.
				ColorPass.Record(renderGraph, camera, cullingResults, textures);

				// Skybox and transparent pass
				//  Render Unity's Skybox and the scene's transparent geometry. Useful for particle effects.
				SkyboxPass.Record(renderGraph, camera, textures.colorAttachment, textures.depthAttachment);
				TransparentPass.Record(renderGraph, camera, cullingResults, textures.colorAttachment, textures.depthAttachment);

				// Unsupported shaders pass
				//  Render the scene geometry that use an unsupported shader.
				UnsupportedShadersPass.Record(renderGraph, camera, cullingResults, textures.colorAttachment, textures.depthAttachment);

				// 'Final color pass'
				//  Warp the current render with the 'warp pass' and apply the bloom from the 'final shadow pass'.
				FinalColorPass.Record(renderGraph, camera, settings, textures);

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