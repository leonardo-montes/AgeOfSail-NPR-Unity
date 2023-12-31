using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoSA.RenderPipeline
{
	public class FinalCompositingPass
	{
		private static readonly ProfilingSampler m_sampler = new("FinalCompositingPass");

		private static int WarpBloomId = Shader.PropertyToID("_WarpBloom");
		private static int[] SourceIds = new int[5]
		{
			Shader.PropertyToID("_Source0"),
			Shader.PropertyToID("_Source1"),
			Shader.PropertyToID("_Source2"),
			Shader.PropertyToID("_Source3"),
			Shader.PropertyToID("_Source4")
		};

		private AoSARenderPipelineSettings m_settings;
		
		private TextureHandle m_litColorBuffer, m_warpBuffer;
		private TextureHandle[] m_bloomBuffers;

		void Render(RenderGraphContext context)
		{
			context.cmd.SetGlobalFloat(WarpBloomId, m_settings.warpBloom ? 1.0f : 0.0f);
			
			context.cmd.SetGlobalTexture(SourceIds[0], m_litColorBuffer);
			context.cmd.SetGlobalTexture(SourceIds[1], m_warpBuffer);
			for (int i = 0; i < m_bloomBuffers.Length; ++i)
				context.cmd.SetGlobalTexture(SourceIds[2 + i], m_bloomBuffers[i]);

			context.cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, BuiltinRenderTextureType.None);
			context.cmd.DrawProcedural(Matrix4x4.identity, m_settings.Material, (int)AoSARenderPipeline.Pass.FinalCompositingPass, MeshTopology.Triangles, 3);

			context.renderContext.ExecuteCommandBuffer(context.cmd);
			context.cmd.Clear();
		}

		public static void Record(RenderGraph renderGraph, AoSARenderPipelineSettings settings, in CameraRendererTextures textures)
		{
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(m_sampler.name, out FinalCompositingPass pass, m_sampler);
			pass.m_settings = settings;
			pass.m_litColorBuffer = builder.ReadTexture(textures.litColorBuffer);
			pass.m_warpBuffer = builder.ReadTexture(textures.warpColor);
			pass.m_bloomBuffers = ReadTextures(builder, textures.bloomBuffers);
			builder.SetRenderFunc<FinalCompositingPass>((pass, context) => pass.Render(context));
		}

		private static TextureHandle[] ReadTextures(RenderGraphBuilder builder, in TextureHandle[] textures)
		{
			TextureHandle[] newHandles = new TextureHandle[textures.Length];
			for (int i = 0; i < textures.Length; ++i)
			{
				newHandles[i] = builder.ReadTexture(textures[i]);
			}
			return newHandles;
		}
	}
}