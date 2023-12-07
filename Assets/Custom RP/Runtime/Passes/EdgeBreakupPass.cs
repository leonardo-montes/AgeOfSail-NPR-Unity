using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

public class EdgeBreakupPass
{
	static readonly ProfilingSampler
		sampler = new("Edge Breakup Pass (Opaque Geometry)");

	static readonly ShaderTagId[] shaderTagIDs = {
		new("EdgeBreakup")
	};

	RendererListHandle list;
	TextureHandle edgeBreakupColor, edgeBreakupDepth;

	void Render(RenderGraphContext context)
	{
        context.cmd.SetRenderTarget(
            edgeBreakupColor,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
            edgeBreakupDepth,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);

		context.cmd.DrawRendererList(list);
		context.renderContext.ExecuteCommandBuffer(context.cmd);
		context.cmd.Clear();
	}

	public static void Record(
		RenderGraph renderGraph,
		Camera camera,
		CullingResults cullingResults,
		int renderingLayerMask,
		bool opaque,
		in CameraRendererTextures textures)
	{
		using RenderGraphBuilder builder = renderGraph.AddRenderPass(
			sampler.name, out EdgeBreakupPass pass, sampler);

		pass.list = builder.UseRendererList(renderGraph.CreateRendererList(
			new RendererListDesc(shaderTagIDs, cullingResults, camera)
			{
				sortingCriteria = opaque ? SortingCriteria.CommonOpaque : SortingCriteria.CommonTransparent,
				rendererConfiguration = PerObjectData.None,
				renderQueueRange = opaque ? RenderQueueRange.opaque : RenderQueueRange.transparent,
				renderingLayerMask = (uint)renderingLayerMask
			}));

        pass.edgeBreakupColor = builder.ReadWriteTexture(textures.edgeBreakupColor);
        pass.edgeBreakupDepth = builder.ReadWriteTexture(textures.edgeBreakupDepth);
		/*if (!opaque)
		{
			if (textures.edgeBreakupColor.IsValid())
			{
				builder.ReadTexture(textures.edgeBreakupColor);
			}
			if (textures.edgeBreakupDepth.IsValid())
			{
				builder.ReadTexture(textures.edgeBreakupDepth);
			}
		}*/

		builder.SetRenderFunc<EdgeBreakupPass>((pass, context) => pass.Render(context));
	}
}
