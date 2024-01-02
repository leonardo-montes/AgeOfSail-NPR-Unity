using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoS.RenderPipeline
{
	/// <summary>
	/// Image-processing pass compositing the result from the 'Shadow pass' and the two blurred version of it from the 'Blur pass'.
	/// </summary>
	public class FinalShadowPass
	{
		private static readonly ProfilingSampler Sampler = new("Final Shadow Pass");

		private static int ShadowStepCountId = Shader.PropertyToID("_ShadowStepCount");
		private static int ShadowThresholdId = Shader.PropertyToID("_ShadowThreshold");
		private static int ShadowThresholdSoftnessId = Shader.PropertyToID("_ShadowThresholdSoftness");
		private static int ShadowInnerGlowId = Shader.PropertyToID("_ShadowInnerGlow");

		private TextureHandle m_colorAttachment, m_finalShadowBuffer, m_heavyBlurBuffer, m_softBlurBuffer;
		private AoSRenderPipelineSettings m_settings;

		void Render(RenderGraphContext context)
		{
			// Set the shader properties
			context.cmd.SetGlobalInt(ShadowStepCountId, m_settings.shadowStepCount);
			context.cmd.SetGlobalFloat(ShadowThresholdId, m_settings.shadowThreshold);
			context.cmd.SetGlobalFloat(ShadowThresholdSoftnessId, m_settings.shadowThresholdSoftness);
			context.cmd.SetGlobalFloat(ShadowInnerGlowId, m_settings.shadowInnerGlow);

			// Draw 'shadow pass' texture and the two 'blur pass' textures into the final shadow buffer
			context.cmd.SetGlobalTexture(RenderPipelineHelper.SourceIds[1], m_softBlurBuffer);
			context.cmd.SetGlobalTexture(RenderPipelineHelper.SourceIds[2], m_heavyBlurBuffer);
			RenderPipelineHelper.Draw(context.cmd, m_colorAttachment, m_finalShadowBuffer, (int)Pass.FinalShadowPass, m_settings.Material);

			// Execute the command buffer
			context.renderContext.ExecuteCommandBuffer(context.cmd);
			context.cmd.Clear();
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