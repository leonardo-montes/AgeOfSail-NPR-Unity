using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoSA.RenderPipeline
{
	/// <summary>
	/// Image-processing pass generating soft and heavy blurred versions of the input textures (which are the result from previous 'Shadow pass').
	/// </summary>
	public class BlurPass
	{
		private static readonly ProfilingSampler Sampler = new("Blur Pass");

		private TextureHandle[] m_shadowBuffers, m_softBlurBuffers, m_heavyBlurBuffers;
		private Vector2Int m_bufferSize;
		private bool m_useHDR;
		private AoSARenderPipelineSettings m_settings;

		private void Render(RenderGraphContext context)
		{
			// Get and fill the buffers for rendering
			// Note: this probably creates lots of GC everytime the amount of lights changes.
			// TODO: remove GC allocation for production use.
			int bufferCount = m_shadowBuffers.Length;
			RenderTargetIdentifier[] tempRTs = new RenderTargetIdentifier[bufferCount];
			RenderTargetIdentifier[] shadowRTs = new RenderTargetIdentifier[bufferCount];
			RenderTargetIdentifier[] softBlurRTs = new RenderTargetIdentifier[bufferCount];
			RenderTargetIdentifier[] heavyBlurRTs = new RenderTargetIdentifier[bufferCount];
			for (int i = 0; i < bufferCount; ++i)
			{
				context.cmd.GetTemporaryRT(RenderPipelineHelper.TempRTsId[i], Mathf.CeilToInt(m_bufferSize.x / m_settings.softBlurDownsample), Mathf.CeilToInt(m_bufferSize.y / m_settings.softBlurDownsample), 0, FilterMode.Point, m_useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
			
				tempRTs[i] = new RenderTargetIdentifier(RenderPipelineHelper.TempRTsId[i]);
				shadowRTs[i] = new RenderTargetIdentifier(m_shadowBuffers[i]);
				softBlurRTs[i] = new RenderTargetIdentifier(m_softBlurBuffers[i]);
				heavyBlurRTs[i] = new RenderTargetIdentifier(m_heavyBlurBuffers[i]);
			}

			// Generate the soft blur buffers.
			// - Downsample the input textures.
			RenderPipelineHelper.Draw(context.cmd, shadowRTs, softBlurRTs, (int)Pass.DownsampleArray, m_settings.Material);
			
			// - 2 pass 1D Gaussian blur, ping-ponging with the temporary RTs.
			RenderPipelineHelper.Draw(context.cmd, softBlurRTs, tempRTs, (int)Pass.BlurHorizontalArray, m_settings.Material);
			RenderPipelineHelper.Draw(context.cmd, tempRTs, softBlurRTs, (int)Pass.BlurVerticalArray, m_settings.Material);
			
			// Release and get the RTs with the correct resolution for the heavy blur buffers.
			for (int i = 0; i < bufferCount; ++i)
			{
				context.cmd.ReleaseTemporaryRT(RenderPipelineHelper.TempRTsId[i]);
				context.cmd.GetTemporaryRT(RenderPipelineHelper.TempRTsId[i], Mathf.CeilToInt(m_bufferSize.x / m_settings.heavyBlurDownsample), Mathf.CeilToInt(m_bufferSize.y / m_settings.heavyBlurDownsample), 0, FilterMode.Bilinear, m_useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
			}
			
			// Generate the heavy blur buffers.
			// - Downsample the soft blur buffers.
			RenderPipelineHelper.Draw(context.cmd, softBlurRTs, heavyBlurRTs, (int)Pass.DownsampleArray, m_settings.Material);
			
			// - 2 pass 1D Gaussian blur, ping-ponging with the temporary RTs.
			RenderPipelineHelper.Draw(context.cmd, heavyBlurRTs, tempRTs, (int)Pass.BlurHorizontalArray, m_settings.Material);
			RenderPipelineHelper.Draw(context.cmd, tempRTs, heavyBlurRTs, (int)Pass.BlurVerticalArray, m_settings.Material);

			// Release the temporary RT
			for (int i = 0; i < bufferCount; ++i)
				context.cmd.ReleaseTemporaryRT(RenderPipelineHelper.TempRTsId[i]);

			// Execute the command buffer
			context.renderContext.ExecuteCommandBuffer(context.cmd);
			context.cmd.Clear();
		}
		public static void Record(RenderGraph renderGraph, Vector2Int bufferSize, bool useHDR, AoSARenderPipelineSettings settings, in CameraRendererTextures textures, RendererListHandle listHandle)
		{
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(Sampler.name, out BlurPass pass, Sampler);
			
			pass.m_bufferSize = bufferSize;
			pass.m_useHDR = useHDR;
			pass.m_settings = settings;
			
			pass.m_shadowBuffers = builder.ReadTextures(textures.shadowBuffers);
			pass.m_softBlurBuffers = builder.WriteTextures(textures.softBlurBuffers);
			pass.m_heavyBlurBuffers = builder.WriteTextures(textures.heavyBlurBuffers);

			// Specify that we only render this pass if the previous 'shadow pass' has not been culled.
			builder.DependsOn(listHandle);
			
			builder.SetRenderFunc<BlurPass>((pass, context) => pass.Render(context));
		}
	}
}