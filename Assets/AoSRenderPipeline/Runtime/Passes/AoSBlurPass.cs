using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoS.RenderPipeline
{
	public class BlurPass
	{
		private static readonly ProfilingSampler Sampler = new("Blur Pass");

		private static int TempRT0Id = Shader.PropertyToID("_TempRT0");
		private static int Source0Id = Shader.PropertyToID("_Source0");

		private TextureHandle m_colorAttachment, m_heavyBlurBuffer, m_softBlurBuffer;
		private Vector2Int m_bufferSize;
		private bool m_useHDR;
		private AoSRenderPipelineSettings m_settings;

		private void Render(RenderGraphContext context)
		{
			context.cmd.GetTemporaryRT(TempRT0Id, Mathf.CeilToInt(m_bufferSize.x / m_settings.softBlurDownsample), Mathf.CeilToInt(m_bufferSize.y / m_settings.softBlurDownsample), 0, FilterMode.Bilinear, m_useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);

			Draw(context.cmd, m_colorAttachment, m_softBlurBuffer, AoSRenderPipeline.Pass.Downsample);
			Draw(context.cmd, m_softBlurBuffer, TempRT0Id, AoSRenderPipeline.Pass.BlurHorizontal);
			Draw(context.cmd, TempRT0Id, m_softBlurBuffer, AoSRenderPipeline.Pass.BlurVertical);
			
			context.cmd.ReleaseTemporaryRT(TempRT0Id);
			context.cmd.GetTemporaryRT(TempRT0Id, Mathf.CeilToInt(m_bufferSize.x / m_settings.heavyBlurDownsample), Mathf.CeilToInt(m_bufferSize.y / m_settings.heavyBlurDownsample), 0, FilterMode.Bilinear, m_useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);

			Draw(context.cmd, m_softBlurBuffer, m_heavyBlurBuffer, AoSRenderPipeline.Pass.Downsample);
			Draw(context.cmd, m_heavyBlurBuffer, TempRT0Id, AoSRenderPipeline.Pass.BlurHorizontal);
			Draw(context.cmd, TempRT0Id, m_heavyBlurBuffer, AoSRenderPipeline.Pass.BlurVertical);

			context.cmd.ReleaseTemporaryRT(TempRT0Id);

			context.renderContext.ExecuteCommandBuffer(context.cmd);
			context.cmd.Clear();
		}

		private void Draw(CommandBuffer buffer, RenderTargetIdentifier from, RenderTargetIdentifier to, AoSRenderPipeline.Pass pass)
		{
			buffer.SetGlobalTexture(Source0Id, from);
			buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
			buffer.DrawProcedural(Matrix4x4.identity, m_settings.Material, (int)pass, MeshTopology.Triangles, 3);
		}

		public static void Record(RenderGraph renderGraph, Vector2Int bufferSize, bool useHDR, AoSRenderPipelineSettings settings, in CameraRendererTextures textures, RendererListHandle listHandle)
		{
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(Sampler.name, out BlurPass pass, Sampler);
			pass.m_bufferSize = bufferSize;
			pass.m_useHDR = useHDR;
			pass.m_settings = settings;
			pass.m_colorAttachment = builder.ReadTexture(textures.colorAttachment);
			pass.m_heavyBlurBuffer = builder.ReadWriteTexture(textures.heavyBlurBuffer);
			pass.m_softBlurBuffer = builder.ReadWriteTexture(textures.softBlurBuffer);
			builder.DependsOn(listHandle);
			builder.SetRenderFunc<BlurPass>((pass, context) => pass.Render(context));
		}
	}
}