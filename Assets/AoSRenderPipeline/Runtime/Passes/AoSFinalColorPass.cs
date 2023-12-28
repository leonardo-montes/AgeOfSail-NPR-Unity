using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoS.RenderPipeline
{
	public class FinalColorPass
	{
		private static readonly ProfilingSampler m_sampler = new("Final Color Pass");

		private static int Source0Id = Shader.PropertyToID("_Source0");
		private static int Source1Id = Shader.PropertyToID("_Source1");
		private static int Source2Id = Shader.PropertyToID("_Source2");

		private TextureHandle m_colorAttachment, m_warpBuffer, m_blurBuffer;
		private AoSRenderPipelineSettings m_settings;

		void Render(RenderGraphContext context)
		{
			context.cmd.SetGlobalTexture(Source1Id, m_warpBuffer);
			context.cmd.SetGlobalTexture(Source2Id, m_blurBuffer);
			Draw(context.cmd, m_colorAttachment, BuiltinRenderTextureType.CameraTarget, AoSRenderPipeline.Pass.FinalColorPass);

			context.renderContext.ExecuteCommandBuffer(context.cmd);
			context.cmd.Clear();
		}

		private void Draw(CommandBuffer buffer, RenderTargetIdentifier from, RenderTargetIdentifier to, AoSRenderPipeline.Pass pass)
		{
			buffer.SetGlobalTexture(Source0Id, from);
			buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
			buffer.DrawProcedural(Matrix4x4.identity, m_settings.Material, (int)pass, MeshTopology.Triangles, 3);
		}

		public static void Record(RenderGraph renderGraph, AoSRenderPipelineSettings settings, in CameraRendererTextures textures)
		{
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(m_sampler.name, out FinalColorPass pass, m_sampler);
			pass.m_settings = settings;
			pass.m_colorAttachment = builder.ReadTexture(textures.colorAttachment);
			pass.m_warpBuffer = builder.ReadTexture(textures.warpColor);
			pass.m_blurBuffer = builder.ReadTexture(textures.blurBuffer);
			builder.SetRenderFunc<FinalColorPass>((pass, context) => pass.Render(context));
		}
	}
}