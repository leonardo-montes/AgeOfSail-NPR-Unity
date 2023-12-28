using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoS.RenderPipeline
{
	public class BlurPass
	{
		private static readonly ProfilingSampler Sampler = new("Blur Pass");

		private static int TempRT0Id = Shader.PropertyToID("_TempRT0");
		private static int TempRT1Id = Shader.PropertyToID("_TempRT1");
		private static int SourceId = Shader.PropertyToID("_Source0");

		private static int BlurRadiusId = Shader.PropertyToID("_BlurRadius");

		private TextureHandle m_colorAttachment, m_blurBuffer;
		private Vector2Int m_bufferSize;
		private bool m_useHDR;
		private AoSRenderPipelineSettings m_settings;

		private void Render(RenderGraphContext context)
		{
			context.cmd.GetTemporaryRT(TempRT0Id, Mathf.CeilToInt(m_bufferSize.x / m_settings.downsampleAmount), Mathf.CeilToInt(m_bufferSize.y / m_settings.downsampleAmount), 0, FilterMode.Point, m_useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
			context.cmd.GetTemporaryRT(TempRT1Id, Mathf.CeilToInt(m_bufferSize.x / m_settings.downsampleAmount), Mathf.CeilToInt(m_bufferSize.y / m_settings.downsampleAmount), 0, FilterMode.Point, m_useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);

			context.cmd.SetGlobalFloat(BlurRadiusId, m_settings.blurRadius);

			Draw(context.cmd, m_colorAttachment, TempRT0Id, AoSRenderPipeline.Pass.BlurHorizontal);
			Draw(context.cmd, TempRT0Id,         TempRT1Id, AoSRenderPipeline.Pass.BlurVertical);

			Draw(context.cmd, TempRT1Id, m_blurBuffer, AoSRenderPipeline.Pass.Copy);

			context.cmd.ReleaseTemporaryRT(TempRT0Id);
			context.cmd.ReleaseTemporaryRT(TempRT1Id);

			context.renderContext.ExecuteCommandBuffer(context.cmd);
			context.cmd.Clear();
		}

		private void Draw(CommandBuffer buffer, RenderTargetIdentifier from, RenderTargetIdentifier to, AoSRenderPipeline.Pass pass)
		{
			buffer.SetGlobalTexture(SourceId, from);
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
			pass.m_blurBuffer = builder.WriteTexture(textures.blurBuffer);
			builder.DependsOn(listHandle);
			builder.SetRenderFunc<BlurPass>((pass, context) => pass.Render(context));
		}
	}
}