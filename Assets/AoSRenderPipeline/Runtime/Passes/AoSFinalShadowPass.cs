using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoS.RenderPipeline
{
	public class FinalShadowPass
	{
		private static readonly ProfilingSampler Sampler = new("Final Shadow Pass");

		private static int ShadowStepCountId = Shader.PropertyToID("_ShadowStepCount");
		private static int ShadowThresholdId = Shader.PropertyToID("_ShadowThreshold");
		private static int ShadowThresholdSoftnessId = Shader.PropertyToID("_ShadowThresholdSoftness");
		private static int ShadowInnerGlowId = Shader.PropertyToID("_ShadowInnerGlow");

		private static int Source0Id = Shader.PropertyToID("_Source0");
		private static int Source1Id = Shader.PropertyToID("_Source1");
		private static int Source2Id = Shader.PropertyToID("_Source2");

		private TextureHandle m_colorAttachment, m_finalShadowBuffer, m_heavyBlurBuffer, m_softBlurBuffer;
		private AoSRenderPipelineSettings m_settings;

		void Render(RenderGraphContext context)
		{
			context.cmd.SetGlobalInt(ShadowStepCountId, m_settings.shadowStepCount);
			context.cmd.SetGlobalFloat(ShadowThresholdId, m_settings.shadowThreshold);
			context.cmd.SetGlobalFloat(ShadowThresholdSoftnessId, m_settings.shadowThresholdSoftness);
			context.cmd.SetGlobalFloat(ShadowInnerGlowId, m_settings.shadowInnerGlow);

			context.cmd.SetGlobalTexture(Source1Id, m_softBlurBuffer);
			context.cmd.SetGlobalTexture(Source2Id, m_heavyBlurBuffer);
			Draw(context.cmd, m_colorAttachment, m_finalShadowBuffer, AoSRenderPipeline.Pass.FinalShadowPass);

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
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(Sampler.name, out FinalShadowPass pass, Sampler);
			pass.m_settings = settings;
			pass.m_colorAttachment = builder.ReadTexture(textures.colorAttachment);
			pass.m_finalShadowBuffer = builder.WriteTexture(textures.finalShadowBuffer);
			pass.m_heavyBlurBuffer = builder.ReadTexture(textures.heavyBlurBuffer);
			pass.m_softBlurBuffer = builder.ReadTexture(textures.softBlurBuffer);
			builder.SetRenderFunc<FinalShadowPass>((pass, context) => pass.Render(context));
		}
	}
}