using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class CameraRenderer
{
	public const float renderScaleMin = 0.1f, renderScaleMax = 2f;

	static readonly CameraSettings defaultCameraSettings = new();

	readonly PostFXStack postFXStack = new();

	readonly Material material;

	public CameraRenderer(Shader shader) =>
		material = CoreUtils.CreateEngineMaterial(shader);

	public void Dispose() => CoreUtils.Destroy(material);

	public void Render(
		RenderGraph renderGraph,
		ScriptableRenderContext context, Camera camera,
		CameraBufferSettings bufferSettings,
		bool useLightsPerObject,
		ShadowSettings shadowSettings, PostFXSettings postFXSettings, EdgeBreakupSettings edgeBreakupSettings,
		int colorLUTResolution,
		AgeOfSailPipelineSettings ageOfSailPipelineSettings)
	{
		ProfilingSampler cameraSampler;
		CameraSettings cameraSettings;
		if (camera.TryGetComponent(out CustomRenderPipelineCamera crpCamera))
		{
			cameraSampler = crpCamera.Sampler;
			cameraSettings = crpCamera.Settings;
		}
		else
		{
			cameraSampler = ProfilingSampler.Get(camera.cameraType);
			cameraSettings = defaultCameraSettings;
		}

		bool useColorTexture, useDepthTexture;
		if (camera.cameraType == CameraType.Reflection)
		{
			useColorTexture = bufferSettings.copyColorReflection;
			useDepthTexture = bufferSettings.copyDepthReflection;
		}
		else
		{
			useColorTexture =
				bufferSettings.copyColor && cameraSettings.copyColor;
			useDepthTexture =
				bufferSettings.copyDepth && cameraSettings.copyDepth;
		}

		if (cameraSettings.overridePostFX)
		{
			postFXSettings = cameraSettings.postFXSettings;
		}

		float renderScale = cameraSettings.GetRenderScale(
			bufferSettings.renderScale);
		bool useScaledRendering = renderScale < 0.99f || renderScale > 1.01f;

#if UNITY_EDITOR
		if (camera.cameraType == CameraType.SceneView)
		{
			ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
			useScaledRendering = false;
		}
#endif

		if (!camera.TryGetCullingParameters(
			out ScriptableCullingParameters scriptableCullingParameters))
		{
			return;
		}
		scriptableCullingParameters.shadowDistance =
			Mathf.Min(shadowSettings.maxDistance, camera.farClipPlane);
		CullingResults cullingResults = context.Cull(
			ref scriptableCullingParameters);

		bool useHDR = bufferSettings.allowHDR && camera.allowHDR;
		Vector2Int bufferSize = default;
		if (useScaledRendering)
		{
			renderScale = Mathf.Clamp(
				renderScale, renderScaleMin, renderScaleMax);
			bufferSize.x = (int)(camera.pixelWidth * renderScale);
			bufferSize.y = (int)(camera.pixelHeight * renderScale);
		}
		else
		{
			bufferSize.x = camera.pixelWidth;
			bufferSize.y = camera.pixelHeight;
		}

		bufferSettings.fxaa.enabled &= cameraSettings.allowFXAA;
		postFXStack.Setup(
			camera, bufferSize, postFXSettings, cameraSettings.keepAlpha,
			useHDR, colorLUTResolution, cameraSettings.finalBlendMode,
			bufferSettings.bicubicRescaling, bufferSettings.fxaa);

		bool useIntermediateBuffer = useScaledRendering ||
			useColorTexture || useDepthTexture || postFXStack.IsActive;

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
			using var _ = new RenderGraphProfilingScope(
				renderGraph, cameraSampler);

			ShadowTextures shadowTextures = LightingPass.Record(
				renderGraph, cullingResults, shadowSettings, useLightsPerObject,
				cameraSettings.maskLights ? cameraSettings.renderingLayerMask :
				-1);

			CameraRendererTextures textures = SetupPass.Record(
				renderGraph, useIntermediateBuffer, useColorTexture,
				useDepthTexture, useHDR, bufferSize, camera);

			EdgeBreakupPass.Record(renderGraph, camera, cullingResults, cameraSettings.renderingLayerMask, true, textures, edgeBreakupSettings);

			GeometryPass.Record(
				renderGraph, camera, bufferSize, cullingResults,
				useLightsPerObject, cameraSettings.renderingLayerMask, true,
				textures, shadowTextures, ageOfSailPipelineSettings);

			SkyboxPass.Record(renderGraph, camera, textures);

			var copier = new CameraRendererCopier(
				material, camera, cameraSettings.finalBlendMode);
			CopyAttachmentsPass.Record(
				renderGraph, useColorTexture, useDepthTexture,
				copier, textures);

			GeometryPass.Record(
				renderGraph, camera, bufferSize, cullingResults,
				useLightsPerObject, cameraSettings.renderingLayerMask, false,
				textures, shadowTextures, ageOfSailPipelineSettings);

			UnsupportedShadersPass.Record(renderGraph, camera, cullingResults);

			if (postFXStack.IsActive)
			{
				PostFXPass.Record(renderGraph, postFXStack, textures, edgeBreakupSettings, ageOfSailPipelineSettings);
			}
			else if (useIntermediateBuffer)
			{
				FinalPass.Record(renderGraph, copier, textures);
			}
			GizmosPass.Record(renderGraph, useIntermediateBuffer,
				copier, textures);
		}
		
		context.ExecuteCommandBuffer(renderGraphParameters.commandBuffer);
		context.Submit();
		CommandBufferPool.Release(renderGraphParameters.commandBuffer);
	}
}
