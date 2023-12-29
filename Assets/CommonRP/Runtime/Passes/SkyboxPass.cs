using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class SkyboxPass
{
	private static readonly ProfilingSampler Sampler = new("Skybox");

	private Camera m_camera;

	private TextureHandle m_colorAttachment, m_depthAttachment;

	void Render(RenderGraphContext context)
	{
		context.cmd.SetRenderTarget(m_colorAttachment, m_depthAttachment);
		context.renderContext.ExecuteCommandBuffer(context.cmd);
		context.cmd.Clear();

		context.renderContext.DrawSkybox(m_camera);
	}

	public static void Record(RenderGraph renderGraph, Camera camera, in TextureHandle colorAttachment, in TextureHandle depthAttachment)
	{
		if (camera.clearFlags == CameraClearFlags.Skybox)
		{
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(Sampler.name, out SkyboxPass pass, Sampler);
			pass.m_camera = camera;
			pass.m_colorAttachment = builder.ReadWriteTexture(colorAttachment);
			pass.m_depthAttachment = builder.ReadTexture(depthAttachment);
			builder.SetRenderFunc<SkyboxPass>((pass, context) => pass.Render(context));
		}
	}
}
