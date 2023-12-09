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

	static readonly int edgeBreakupTextureId = Shader.PropertyToID("_EdgeBreakupWarpTexture");
	static readonly int edgeBreakupTextureScaleId = Shader.PropertyToID("_EdgeBreakupWarpTextureScale");
	static readonly int edgeBreakupSkewId = Shader.PropertyToID("_EdgeBreakupSkew");
	static readonly int edgeBreakupTimeId = Shader.PropertyToID("_EdgeBreakupTime");

	RendererListHandle list;
	TextureHandle edgeBreakupColor, edgeBreakupDepth;
	Texture2D edgeBreakupWarpTexture;
	float edgeBreakupWarpTextureScale;
	float skew;
	// x = realtime
	// y = 24 fps
	// z = 12 fps (animate on twos)
	// w = 8  fps (animate on threes)
	Vector4 time;

	void Render(RenderGraphContext context)
	{
        context.cmd.SetRenderTarget(
            edgeBreakupColor,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
            edgeBreakupDepth,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);

		float t = (float)(Time.realtimeSinceStartupAsDouble % 3600.0);
		time = new Vector4(t,
						   Mathf.Floor(t * 24) / 24,
						   Mathf.Floor(t * 12) / 12,
						   Mathf.Floor(t * 8) / 8);

		context.cmd.SetGlobalTexture(edgeBreakupTextureId, edgeBreakupWarpTexture);
		context.cmd.SetGlobalFloat(edgeBreakupTextureScaleId, edgeBreakupWarpTextureScale);
		context.cmd.SetGlobalFloat(edgeBreakupSkewId, skew);
		context.cmd.SetGlobalVector(edgeBreakupTimeId, time);

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
		in CameraRendererTextures textures,
		in EdgeBreakupSettings edgeBreakupSettings)
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
		pass.edgeBreakupWarpTexture = edgeBreakupSettings.warpTexture;
		pass.edgeBreakupWarpTextureScale = edgeBreakupSettings.warpTextureScale;
		pass.skew = edgeBreakupSettings.skew;
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
