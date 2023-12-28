using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoS.RenderPipeline
{
	public class FinalShadowPass
	{
		private static readonly ProfilingSampler Sampler = new("Final Shadow Pass");

		private static int TempRTId = Shader.PropertyToID("_TempRT");
		private static int Source0Id = Shader.PropertyToID("_Source0");
		private static int Source1Id = Shader.PropertyToID("_Source1");
		private static int ShadowThresholdId = Shader.PropertyToID("_ShadowThreshold");
		private static int ShadowThresholdSoftnessId = Shader.PropertyToID("_ShadowThresholdSoftness");

		private TextureHandle m_colorAttachment, m_blurBuffer;
		private Vector2Int m_bufferSize;
		private bool m_useHDR;
		private AoSRenderPipelineSettings m_settings;

		void Render(RenderGraphContext context)
		{
			context.cmd.GetTemporaryRT(TempRTId, m_bufferSize.x, m_bufferSize.y, 0, FilterMode.Point, m_useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);

			Draw(context.cmd, m_colorAttachment, TempRTId, AoSRenderPipeline.Pass.Copy);

			context.cmd.SetGlobalFloat(ShadowThresholdId, m_settings.shadowThreshold);
			context.cmd.SetGlobalFloat(ShadowThresholdSoftnessId, m_settings.shadowThresholdSoftness);
			context.cmd.SetGlobalTexture(Source1Id, m_blurBuffer);
			Draw(context.cmd, TempRTId, m_colorAttachment, AoSRenderPipeline.Pass.FinalShadowPass);

			context.cmd.ReleaseTemporaryRT(TempRTId);

			context.renderContext.ExecuteCommandBuffer(context.cmd);
			context.cmd.Clear();
		}

		private void Draw(CommandBuffer buffer, RenderTargetIdentifier from, RenderTargetIdentifier to, AoSRenderPipeline.Pass pass)
		{
			buffer.SetGlobalTexture(Source0Id, from);
			buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
			buffer.DrawProcedural(Matrix4x4.identity, m_settings.Material, (int)pass, MeshTopology.Triangles, 3);
		}

		public static void Record(RenderGraph renderGraph, Vector2Int bufferSize, bool useHDR, AoSRenderPipelineSettings settings, in CameraRendererTextures textures)
		{
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(Sampler.name, out FinalShadowPass pass, Sampler);
			pass.m_bufferSize = bufferSize;
			pass.m_useHDR = useHDR;
			pass.m_settings = settings;
			pass.m_colorAttachment = builder.ReadWriteTexture(textures.colorAttachment);
			pass.m_blurBuffer = builder.ReadTexture(textures.blurBuffer);
			builder.SetRenderFunc<FinalShadowPass>((pass, context) => pass.Render(context));
		}
	}
}