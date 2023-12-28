using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

public class TransparentPass
{
	private static readonly ProfilingSampler Sampler = new("Transparent Geometry");

	private static readonly ShaderTagId[] ShaderTagIDs =
	{
		new("SRPDefaultUnlit"),
		new("CustomLit")
	};

	private RendererListHandle m_list;
	private TextureHandle m_colorAttachment, m_depthAttachment;

	private void Render(RenderGraphContext context)
	{
        context.cmd.SetRenderTarget(m_colorAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, m_depthAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
		
		context.cmd.DrawRendererList(m_list);
		context.renderContext.ExecuteCommandBuffer(context.cmd);
		context.cmd.Clear();
	}

	public static void Record(RenderGraph renderGraph, Camera camera, CullingResults cullingResults, in TextureHandle colorAttachment, in TextureHandle depthAttachment)
	{
		using RenderGraphBuilder builder = renderGraph.AddRenderPass(Sampler.name, out TransparentPass pass, Sampler);

		pass.m_list = builder.UseRendererList(renderGraph.CreateRendererList(
			new RendererListDesc(ShaderTagIDs, cullingResults, camera)
			{
				sortingCriteria = SortingCriteria.CommonTransparent,
				rendererConfiguration = PerObjectData.None,
				renderQueueRange = RenderQueueRange.transparent
			}));

		pass.m_colorAttachment = builder.ReadWriteTexture(colorAttachment);
		pass.m_depthAttachment = builder.ReadWriteTexture(depthAttachment);

		builder.SetRenderFunc<TransparentPass>((pass, context) => pass.Render(context));
	}
}
