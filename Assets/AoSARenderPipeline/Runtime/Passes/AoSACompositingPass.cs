using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoSA.RenderPipeline
{
	/// <summary>
	/// Image-processing pass compositing the lit color, shadowed color, and the blurred buffers into a final texture and several bloom textures.
	/// </summary>
	public class CompositingPass
	{
		private static readonly ProfilingSampler m_sampler = new("CompositingPass");

		private static int ShadowThresholdId = Shader.PropertyToID("_ShadowThreshold");
		private static int ShadowThresholdSoftnessId = Shader.PropertyToID("_ShadowThresholdSoftness");
		private static int ShadowInnerGlowId = Shader.PropertyToID("_ShadowInnerGlow");
		private static int ShadowStepCountId = Shader.PropertyToID("_ShadowStepCount");

		private AoSARenderPipelineSettings m_settings;
		
		private TextureHandle m_litColorBuffer, m_shadowedColorBuffer;
		private TextureHandle[] m_softBlurBuffers, m_heavyBlurBuffers, m_bloomBuffers;
		private Vector2Int m_bufferSize;
		private bool m_useHDR;

		void Render(RenderGraphContext context)
		{
			// Get the temporary RT
			context.cmd.GetTemporaryRT(RenderPipelineHelper.TempRTsId[0], m_bufferSize.x, m_bufferSize.y, 0, FilterMode.Point, m_useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);

			// Copy the current lit buffer for readback
			RenderPipelineHelper.Draw(context.cmd, m_litColorBuffer, RenderPipelineHelper.TempRTsId[0], (int)Pass.Copy, m_settings.Material);
			
			// Set shader properties
			context.cmd.SetGlobalInt(ShadowStepCountId, m_settings.shadowStepCount);
			context.cmd.SetGlobalFloat(ShadowThresholdId, m_settings.shadowThreshold);
			context.cmd.SetGlobalFloat(ShadowThresholdSoftnessId, m_settings.shadowThresholdSoftness);
			context.cmd.SetGlobalFloat(ShadowInnerGlowId, m_settings.shadowInnerGlow);

			// Set shader textures
			context.cmd.SetGlobalTexture(RenderPipelineHelper.SourceIds[0], RenderPipelineHelper.TempRTsId[0]);
			context.cmd.SetGlobalTexture(RenderPipelineHelper.SourceIds[1], m_shadowedColorBuffer);
			for (int i = 0, j = 0; i < m_softBlurBuffers.Length; ++i, j += 2)
			{
				context.cmd.SetGlobalTexture(RenderPipelineHelper.SourceIds[2 + j], m_softBlurBuffers[i]);
				context.cmd.SetGlobalTexture(RenderPipelineHelper.SourceIds[3 + j], m_heavyBlurBuffers[i]);
			}

			// Fill render target array
			int bloomBufferCount = m_bloomBuffers != null ? m_bloomBuffers.Length : 0;
			RenderTargetIdentifier[] renderTargets = new RenderTargetIdentifier[1 + bloomBufferCount];
			renderTargets[0] = m_litColorBuffer;
			for (int i = 0; i < bloomBufferCount; ++i)
				renderTargets[1 + i] = m_bloomBuffers[i];

			// Set render targets
			context.cmd.SetRenderTarget(renderTargets, BuiltinRenderTextureType.None);

			// Do compositing pass
			context.cmd.DrawProcedural(Matrix4x4.identity, m_settings.Material, (int)Pass.CompositingPass, MeshTopology.Triangles, 3);

			// Release the temporary RT
			context.cmd.ReleaseTemporaryRT(RenderPipelineHelper.TempRTsId[0]);

			// Execute the command buffer
			context.renderContext.ExecuteCommandBuffer(context.cmd);
			context.cmd.Clear();
		}

		public static void Record(RenderGraph renderGraph, Vector2Int bufferSize, bool useHDR, AoSARenderPipelineSettings settings, in CameraRendererTextures textures, RendererListHandle listHandle)
		{
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(m_sampler.name, out CompositingPass pass, m_sampler);
			
			pass.m_settings = settings;
			pass.m_bufferSize = bufferSize;
			pass.m_useHDR = useHDR;
			
			pass.m_litColorBuffer = builder.ReadTexture(textures.litColorBuffer);
			pass.m_shadowedColorBuffer = builder.ReadTexture(textures.shadowedColorBuffer);
			pass.m_softBlurBuffers = builder.ReadTextures(textures.softBlurBuffers);
			pass.m_heavyBlurBuffers = builder.ReadTextures(textures.heavyBlurBuffers);
			pass.m_bloomBuffers = builder.WriteTextures(textures.bloomBuffers);
			
			// Depends on the previous pass's results
			builder.DependsOn(listHandle);
			
			builder.SetRenderFunc<CompositingPass>((pass, context) => pass.Render(context));
		}
	}
}