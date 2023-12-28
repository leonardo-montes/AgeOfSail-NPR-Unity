using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class GizmosPass
{
#if UNITY_EDITOR
	private static readonly ProfilingSampler Sampler = new("Gizmos");
	private Camera m_camera;
	private TextureHandle m_depthAttachment;

	void Render(RenderGraphContext context)
	{
		context.cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, m_depthAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
		context.renderContext.DrawGizmos(m_camera, GizmoSubset.PreImageEffects);
		context.renderContext.DrawGizmos(m_camera, GizmoSubset.PostImageEffects);
	}
#endif

	[Conditional("UNITY_EDITOR")]
	public static void Record(RenderGraph renderGraph, in TextureHandle depthAttachment, in Camera camera)
	{
#if UNITY_EDITOR
		if (Handles.ShouldRenderGizmos())
		{
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(Sampler.name, out GizmosPass pass, Sampler);
			pass.m_camera = camera;
			pass.m_depthAttachment = builder.ReadWriteTexture(depthAttachment);
			builder.SetRenderFunc<GizmosPass>((pass, context) => pass.Render(context));
		}
#endif
	}
}
