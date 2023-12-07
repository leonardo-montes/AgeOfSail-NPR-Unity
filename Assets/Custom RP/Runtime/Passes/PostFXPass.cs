using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class PostFXPass
{
	static readonly ProfilingSampler sampler = new("Post FX");

	PostFXStack postFXStack;

	TextureHandle colorAttachment, edgeBreakupColor;
	EdgeBreakupSettings edgeBreakupSettings;

	void Render(RenderGraphContext context) =>
		postFXStack.Render(context, colorAttachment, edgeBreakupColor, edgeBreakupSettings);

	public static void Record(
		RenderGraph renderGraph,
		PostFXStack postFXStack,
		in CameraRendererTextures textures,
		in EdgeBreakupSettings edgeBreakupSettings)
	{
		using RenderGraphBuilder builder = renderGraph.AddRenderPass(
			sampler.name, out PostFXPass pass, sampler);
		pass.postFXStack = postFXStack;
		pass.colorAttachment = builder.ReadTexture(textures.colorAttachment);
		pass.edgeBreakupColor = builder.ReadTexture(textures.edgeBreakupColor);
		pass.edgeBreakupSettings = edgeBreakupSettings;
		builder.SetRenderFunc<PostFXPass>((pass, context) => pass.Render(context));
	}
}
