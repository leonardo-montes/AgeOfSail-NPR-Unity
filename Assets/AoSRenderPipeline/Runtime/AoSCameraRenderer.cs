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
			ProfilingSampler cameraSampler = ProfilingSampler.Get(camera.cameraType);
			
	#if UNITY_EDITOR
			if (camera.cameraType == CameraType.SceneView)
				ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
	#endif

			if (!camera.TryGetCullingParameters(out ScriptableCullingParameters scriptableCullingParameters))
				return;

			scriptableCullingParameters.shadowDistance = Mathf.Min(settings.shadows.maxDistance, camera.farClipPlane);
			CullingResults cullingResults = context.Cull(ref scriptableCullingParameters);

			bool useHDR = camera.allowHDR;
			Vector2Int bufferSize = new Vector2Int(camera.pixelWidth, camera.pixelHeight);

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

				// Depth pass
				ShadowTextures shadowTextures = DepthPass.Record(renderGraph, cullingResults, camera, settings.shadows);

				// Setup
				CameraRendererTextures textures = SetupPass.Record(renderGraph, useHDR, bufferSize, camera, settings);

				// Warp pass
				WarpPass.Record(renderGraph, camera, cullingResults, settings, textures);

				// Shadow pass
				RendererListHandle listHandle = ShadowPass.Record(renderGraph, camera, cullingResults, textures, shadowTextures);

				// Blur pass
				BlurPass.Record(renderGraph, bufferSize, useHDR, settings, textures, listHandle);

				// Final shadow pass
				FinalShadowPass.Record(renderGraph, settings, textures);

				// Color pass
				ColorPass.Record(renderGraph, camera, cullingResults, bufferSize, useHDR, settings, textures);

				// Skybox and transparent pass
				SkyboxPass.Record(renderGraph, camera, textures.colorAttachment, textures.depthAttachment);
				TransparentPass.Record(renderGraph, camera, cullingResults, textures.colorAttachment, textures.depthAttachment);

				// Unsupported shaders pass
				UnsupportedShadersPass.Record(renderGraph, camera, cullingResults, textures.colorAttachment, textures.depthAttachment);

				// Final color pass
				FinalColorPass.Record(renderGraph, settings, textures);

				// Gizmos pass
				GizmosPass.Record(renderGraph, textures.depthAttachment, camera);
			}
			
			context.ExecuteCommandBuffer(renderGraphParameters.commandBuffer);
			context.Submit();
			CommandBufferPool.Release(renderGraphParameters.commandBuffer);
		}
	}
}