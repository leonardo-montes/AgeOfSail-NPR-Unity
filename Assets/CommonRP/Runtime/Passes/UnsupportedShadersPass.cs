﻿using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

public class UnsupportedShadersPass
{
#if UNITY_EDITOR
	private static readonly ProfilingSampler Sampler = new("Unsupported Shaders");

	private static readonly ShaderTagId[] ShaderTagIDs =
	{
		new("Always"),
		new("ForwardBase"),
		new("PrepassBase"),
		new("Vertex"),
		new("VertexLMRGBM"),
		new("VertexLM")
	};

	private static Material m_errorMaterial;

	private RendererListHandle m_list;

	private void Render(RenderGraphContext context)
	{
		context.cmd.DrawRendererList(m_list);
		context.renderContext.ExecuteCommandBuffer(context.cmd);
		context.cmd.Clear();
	}
#endif

	[Conditional("UNITY_EDITOR")]
	public static void Record(RenderGraph renderGraph, Camera camera, CullingResults cullingResults)
	{
#if UNITY_EDITOR
		using RenderGraphBuilder builder = renderGraph.AddRenderPass(Sampler.name, out UnsupportedShadersPass pass, Sampler);

		if (m_errorMaterial == null)
		{
			m_errorMaterial = new(Shader.Find("Hidden/InternalErrorShader"));
		}

		pass.m_list = builder.UseRendererList(renderGraph.CreateRendererList(
			new RendererListDesc(ShaderTagIDs, cullingResults, camera)
			{
				overrideMaterial = m_errorMaterial,
				renderQueueRange = RenderQueueRange.all
			}));

		builder.SetRenderFunc<UnsupportedShadersPass>((pass, context) => pass.Render(context));
#endif
	}
}
