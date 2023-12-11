using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class PostFXPass
{
	static readonly ProfilingSampler sampler = new("Post FX");

	PostFXStack postFXStack;

	TextureHandle colorAttachment, edgeBreakupColor, blurBuffer;
	EdgeBreakupSettings edgeBreakupSettings;
	AgeOfSailPipelineSettings ageOfSailPipelineSettings;

	void Render(RenderGraphContext context) =>
		postFXStack.Render(context, colorAttachment, edgeBreakupColor, blurBuffer, ageOfSailPipelineSettings, edgeBreakupSettings);

	public static void Record(
		RenderGraph renderGraph,
		PostFXStack postFXStack,
		in CameraRendererTextures textures,
		in EdgeBreakupSettings edgeBreakupSettings,
		in AgeOfSailPipelineSettings ageOfSailPipelineSettings)
	{
		using RenderGraphBuilder builder = renderGraph.AddRenderPass(
			sampler.name, out PostFXPass pass, sampler);
		pass.postFXStack = postFXStack;
		pass.colorAttachment = builder.ReadTexture(textures.colorAttachment);
		pass.edgeBreakupColor = builder.ReadTexture(textures.edgeBreakupColor);
		pass.blurBuffer = builder.ReadTexture(textures.blurBuffer);
		pass.edgeBreakupSettings = edgeBreakupSettings;
		pass.ageOfSailPipelineSettings = ageOfSailPipelineSettings;
		builder.SetRenderFunc<PostFXPass>((pass, context) => pass.Render(context));
	}
}
