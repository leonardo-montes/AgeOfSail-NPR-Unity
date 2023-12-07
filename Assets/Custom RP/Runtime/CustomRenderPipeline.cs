using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline
{
	readonly CameraRenderer renderer;

	readonly CameraBufferSettings cameraBufferSettings;

	readonly bool useLightsPerObject;

	readonly ShadowSettings shadowSettings;

	readonly PostFXSettings postFXSettings;

	readonly int colorLUTResolution;

	readonly RenderGraph renderGraph = new("Custom SRP Render Graph");

	public CustomRenderPipeline(
		CameraBufferSettings cameraBufferSettings,
		bool useSRPBatcher,
		bool useLightsPerObject, ShadowSettings shadowSettings,
		PostFXSettings postFXSettings, int colorLUTResolution,
		Shader cameraRendererShader)
	{
		this.colorLUTResolution = colorLUTResolution;
		this.cameraBufferSettings = cameraBufferSettings;
		this.postFXSettings = postFXSettings;
		this.shadowSettings = shadowSettings;
		this.useLightsPerObject = useLightsPerObject;
		GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
		GraphicsSettings.lightsUseLinearIntensity = true;
		InitializeForEditor();
		renderer = new(cameraRendererShader);
	}

	protected override void Render(
		ScriptableRenderContext context, Camera[] cameras) {}

	protected override void Render(
		ScriptableRenderContext context, List<Camera> cameras)
	{
		for (int i = 0; i < cameras.Count; i++)
		{
			renderer.Render(
				renderGraph, context, cameras[i], cameraBufferSettings,
				useLightsPerObject,
				shadowSettings, postFXSettings, colorLUTResolution);
		}
		renderGraph.EndFrame();
	}

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		DisposeForEditor();
		renderer.Dispose();
		renderGraph.Cleanup();
	}
}
