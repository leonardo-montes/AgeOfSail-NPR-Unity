﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoSA.RenderPipeline
{		
	/// <summary>
	/// Enum containing the different shader passes from '/Shaders/CameraRenderer.shader'.
	/// </summary>
	public enum Pass { Copy, CopyArray, DownsampleArray, BlurHorizontalArray, BlurVerticalArray, CompositingPass, FinalCompositingPass, DebugMultiple }

	public partial class AoSARenderPipeline : UnityEngine.Rendering.RenderPipeline
	{
		private readonly CameraRenderer m_renderer;
		private readonly AoSARenderPipelineSettings m_settings;

		private readonly RenderGraph m_renderGraph = new("AoS SRP Render Graph");

		public AoSARenderPipeline(AoSARenderPipelineSettings settings)
		{
			m_settings = settings;
			GraphicsSettings.useScriptableRenderPipelineBatching = m_settings.useSRPBatcher;
			GraphicsSettings.lightsUseLinearIntensity = true;
			InitializeForEditor();
			m_renderer = new();
		}

		protected override void Render(ScriptableRenderContext context, Camera[] cameras) {}

		protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
		{
			for (int i = 0; i < cameras.Count; i++)
			{
				m_renderer.Render(m_renderGraph, context, cameras[i], m_settings);
			}
			m_renderGraph.EndFrame();
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			DisposeForEditor();
			m_renderGraph.Cleanup();
		}
	}
}