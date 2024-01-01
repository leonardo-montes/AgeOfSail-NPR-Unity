using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoSA.RenderPipeline
{
	public class CameraRenderer
	{
		public void Render(RenderGraph renderGraph, ScriptableRenderContext context, Camera camera, AoSARenderPipelineSettings settings)
		{
			ProfilingSampler cameraSampler = ProfilingSampler.Get(camera.cameraType);
			
	#if UNITY_EDITOR
			if (camera.cameraType == CameraType.SceneView)
				ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
	#endif

			if (!camera.TryGetCullingParameters(out ScriptableCullingParameters scriptableCullingParameters))
				return;

			Color overlayColor;
			float saturation;
			if (camera.TryGetComponent(out AoSACameraSettings cameraSettings))
				cameraSettings.GetSettings(out overlayColor, out saturation);
			else
				AoSACameraSettings.GetDefaultSettings(out overlayColor, out saturation);

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
				ShadowTextures shadowTextures = DepthPass.Record(renderGraph, cullingResults, camera, settings, -1, out int totalLightCount, out Vector4[] lightColors);

				// Setup
				CameraRendererTextures textures = SetupPass.Record(renderGraph, useHDR, bufferSize, camera, settings, totalLightCount);

				// Warp pass
				WarpPass.Record(renderGraph, camera, cullingResults, settings, textures);

				// Color Shadow pass
				RendererListHandle listHandle = ColorShadowPass.Record(renderGraph, camera, cullingResults, textures, shadowTextures, lightColors);

				// Blur pass
				BlurPass.Record(renderGraph, bufferSize, useHDR, settings, textures, listHandle);

				// Compositing pass
				CompositingPass.Record(renderGraph, bufferSize, useHDR, settings, textures, listHandle);

				// Skybox and transparent pass
				SkyboxPass.Record(renderGraph, camera, textures.litColorBuffer, textures.depthAttachment);
				TransparentPass.Record(renderGraph, camera, cullingResults, textures.litColorBuffer, textures.depthAttachment);

				// Unsupported shaders pass
				UnsupportedShadersPass.Record(renderGraph, camera, cullingResults, textures.litColorBuffer, textures.depthAttachment);

				// Final compositing pass
				FinalCompositingPass.Record(renderGraph, camera, settings, overlayColor, saturation, textures);

				// Gizmos pass
				GizmosPass.Record(renderGraph, textures.depthAttachment, camera);
			}
			
			context.ExecuteCommandBuffer(renderGraphParameters.commandBuffer);
			context.Submit();
			CommandBufferPool.Release(renderGraphParameters.commandBuffer);
		}
	}
}