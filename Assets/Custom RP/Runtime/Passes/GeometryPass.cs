using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

public class GeometryPass
{
	static readonly ProfilingSampler
		samplerOpaque = new("Opaque Geometry"),
		samplerTransparent = new("Transparent Geometry");

	static readonly ShaderTagId[] shaderTagIDs = {
		new("SRPDefaultUnlit"),
		new("CustomLit")
	};

	RendererListHandle list;
	TextureHandle colorAttachment, depthAttachment;

	void Render(RenderGraphContext context)
	{
        context.cmd.SetRenderTarget(
            colorAttachment,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
            depthAttachment,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
		
		context.cmd.DrawRendererList(list);
		context.renderContext.ExecuteCommandBuffer(context.cmd);
		context.cmd.Clear();
	}

	public static void Record(
		RenderGraph renderGraph,
		Camera camera,
		CullingResults cullingResults,
		bool useLightsPerObject,
		int renderingLayerMask,
		bool opaque,
		in CameraRendererTextures textures,
		in ShadowTextures shadowTextures)
	{
		ProfilingSampler sampler = opaque ? samplerOpaque : samplerTransparent;

		using RenderGraphBuilder builder = renderGraph.AddRenderPass(
			sampler.name, out GeometryPass pass, sampler);

		pass.list = builder.UseRendererList(renderGraph.CreateRendererList(
			new RendererListDesc(shaderTagIDs, cullingResults, camera)
			{
				sortingCriteria = opaque ?
					SortingCriteria.CommonOpaque :
					SortingCriteria.CommonTransparent,
				rendererConfiguration =
					PerObjectData.ReflectionProbes |
					PerObjectData.Lightmaps |
					PerObjectData.ShadowMask |
					PerObjectData.LightProbe |
					PerObjectData.OcclusionProbe |
					PerObjectData.LightProbeProxyVolume |
					PerObjectData.OcclusionProbeProxyVolume |
					(useLightsPerObject ?
						PerObjectData.LightData | PerObjectData.LightIndices :
						PerObjectData.None),
				renderQueueRange = opaque ?
					RenderQueueRange.opaque : RenderQueueRange.transparent,
				renderingLayerMask = (uint)renderingLayerMask
			}));

		pass.colorAttachment = builder.ReadWriteTexture(textures.colorAttachment);
		pass.depthAttachment = builder.ReadWriteTexture(textures.depthAttachment);
		if (!opaque)
		{
			if (textures.colorCopy.IsValid())
			{
				pass.colorAttachment = builder.ReadTexture(textures.colorCopy);
			}
			if (textures.depthCopy.IsValid())
			{
				pass.depthAttachment = builder.ReadTexture(textures.depthCopy);
			}
		}
		builder.ReadTexture(shadowTextures.directionalAtlas);
		builder.ReadTexture(shadowTextures.otherAtlas);

		builder.SetRenderFunc<GeometryPass>(
			(pass, context) => pass.Render(context));
	}
}
