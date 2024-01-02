using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoS.RenderPipeline
{
	/// <summary>
	/// Image-processing pass generating soft and heavy blurred versions of the input texture (which is the result from previous 'Shadow pass').
	/// </summary>
	public class BlurPass
	{
		private static readonly ProfilingSampler Sampler = new("Blur Pass");

		private TextureHandle m_colorAttachment, m_heavyBlurBuffer, m_softBlurBuffer;
		private Vector2Int m_bufferSize;
		private bool m_useHDR;
		private AoSRenderPipelineSettings m_settings;

		private void Render(RenderGraphContext context)
		{
			// Get a temporary RT for ping-ponging between textures for the soft blur buffer.
			context.cmd.GetTemporaryRT(RenderPipelineHelper.TempRTsId[0], Mathf.CeilToInt(m_bufferSize.x / m_settings.softBlurDownsample), Mathf.CeilToInt(m_bufferSize.y / m_settings.softBlurDownsample), 0, FilterMode.Bilinear, m_useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);

			// Generate the soft blur buffer.
			// - Downsample the input texture.
			RenderPipelineHelper.Draw(context.cmd, m_colorAttachment, m_softBlurBuffer, (int)Pass.Downsample, m_settings.Material);

			// - 2 pass 1D Gaussian blur, ping-ponging with the temporary RT.
			RenderPipelineHelper.Draw(context.cmd, m_softBlurBuffer, RenderPipelineHelper.TempRTsId[0], (int)Pass.BlurHorizontal, m_settings.Material);
			RenderPipelineHelper.Draw(context.cmd, RenderPipelineHelper.TempRTsId[0], m_softBlurBuffer, (int)Pass.BlurVertical, m_settings.Material);
			
			// Release the temporary RT
			context.cmd.ReleaseTemporaryRT(RenderPipelineHelper.TempRTsId[0]);
			
			// Get a temporary RT for ping-ponging between textures for the heavy blur buffer.
			context.cmd.GetTemporaryRT(RenderPipelineHelper.TempRTsId[0], Mathf.CeilToInt(m_bufferSize.x / m_settings.heavyBlurDownsample), Mathf.CeilToInt(m_bufferSize.y / m_settings.heavyBlurDownsample), 0, FilterMode.Bilinear, m_useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);

			// Generate the heavy blur buffer.
			// - Downsample the soft blur buffer.
			RenderPipelineHelper.Draw(context.cmd, m_softBlurBuffer, m_heavyBlurBuffer, (int)Pass.Downsample, m_settings.Material);

			// - 2 pass 1D Gaussian blur, ping-ponging with the temporary RT.
			RenderPipelineHelper.Draw(context.cmd, m_heavyBlurBuffer, RenderPipelineHelper.TempRTsId[0], (int)Pass.BlurHorizontal, m_settings.Material);
			RenderPipelineHelper.Draw(context.cmd, RenderPipelineHelper.TempRTsId[0], m_heavyBlurBuffer, (int)Pass.BlurVertical, m_settings.Material);

			// Release the temporary RT
			context.cmd.ReleaseTemporaryRT(RenderPipelineHelper.TempRTsId[0]);

			// Execute the command buffer
			context.renderContext.ExecuteCommandBuffer(context.cmd);
			context.cmd.Clear();
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
			
			// Specify that we only render this pass if the previous 'shadow pass' has not been culled.
			builder.DependsOn(listHandle);

			builder.SetRenderFunc<BlurPass>((pass, context) => pass.Render(context));
		}
	}
}