using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoSA.RenderPipeline
{
	public class BlurPass
	{
		private static readonly ProfilingSampler Sampler = new("Blur Pass");

		private static int[] TempRTsId = new int[5]
		{
			Shader.PropertyToID("_TempRT0"),
			Shader.PropertyToID("_TempRT1"),
			Shader.PropertyToID("_TempRT2"),
			Shader.PropertyToID("_TempRT3"),
			Shader.PropertyToID("_TempRT4")
		};
		private static int[] SourceIds = new int[5]
		{
			Shader.PropertyToID("_Source0"),
			Shader.PropertyToID("_Source1"),
			Shader.PropertyToID("_Source2"),
			Shader.PropertyToID("_Source3"),
			Shader.PropertyToID("_Source4")
		};

		private static int BlurRadiusId = Shader.PropertyToID("_BlurRadius");

		private TextureHandle[] m_shadowBuffers, m_softBlurBuffers, m_heavyBlurBuffers;
		private Vector2Int m_bufferSize;
		private bool m_useHDR;
		private AoSARenderPipelineSettings m_settings;

		private void Render(RenderGraphContext context)
		{
			int bufferCount = m_shadowBuffers.Length;
			RenderTargetIdentifier[] tempRTs = new RenderTargetIdentifier[bufferCount];
			RenderTargetIdentifier[] shadowRTs = new RenderTargetIdentifier[bufferCount];
			RenderTargetIdentifier[] softBlurRTs = new RenderTargetIdentifier[bufferCount];
			RenderTargetIdentifier[] heavyBlurRTs = new RenderTargetIdentifier[bufferCount];
			for (int i = 0; i < bufferCount; ++i)
			{
				context.cmd.GetTemporaryRT(TempRTsId[i], Mathf.CeilToInt(m_bufferSize.x / m_settings.softBlurDownsample), Mathf.CeilToInt(m_bufferSize.y / m_settings.softBlurDownsample), 0, FilterMode.Point, m_useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
			
				tempRTs[i] = new RenderTargetIdentifier(TempRTsId[i]);
				shadowRTs[i] = new RenderTargetIdentifier(m_shadowBuffers[i]);
				softBlurRTs[i] = new RenderTargetIdentifier(m_softBlurBuffers[i]);
				heavyBlurRTs[i] = new RenderTargetIdentifier(m_heavyBlurBuffers[i]);
			}

			Draw(context.cmd, shadowRTs, softBlurRTs, AoSARenderPipeline.Pass.DownsampleArray);
			Draw(context.cmd, softBlurRTs, tempRTs, AoSARenderPipeline.Pass.BlurHorizontalArray);
			Draw(context.cmd, tempRTs, softBlurRTs, AoSARenderPipeline.Pass.BlurVerticalArray);
			
			for (int i = 0; i < bufferCount; ++i)
			{
				context.cmd.ReleaseTemporaryRT(TempRTsId[i]);
				context.cmd.GetTemporaryRT(TempRTsId[i], Mathf.CeilToInt(m_bufferSize.x / m_settings.heavyBlurDownsample), Mathf.CeilToInt(m_bufferSize.y / m_settings.heavyBlurDownsample), 0, FilterMode.Bilinear, m_useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
			}
			
			Draw(context.cmd, softBlurRTs, heavyBlurRTs, AoSARenderPipeline.Pass.DownsampleArray);
			Draw(context.cmd, heavyBlurRTs, tempRTs, AoSARenderPipeline.Pass.BlurHorizontalArray);
			Draw(context.cmd, tempRTs, heavyBlurRTs, AoSARenderPipeline.Pass.BlurVerticalArray);

			for (int i = 0; i < bufferCount; ++i)
				context.cmd.ReleaseTemporaryRT(TempRTsId[i]);

			context.renderContext.ExecuteCommandBuffer(context.cmd);
			context.cmd.Clear();
		}

		private void Draw(CommandBuffer buffer, RenderTargetIdentifier[] from, RenderTargetIdentifier[] to, AoSARenderPipeline.Pass pass)
		{
			for (int i = 0; i < from.Length; ++i)
				buffer.SetGlobalTexture(SourceIds[i], from[i]);

			buffer.SetRenderTarget(to, BuiltinRenderTextureType.None);
			buffer.DrawProcedural(Matrix4x4.identity, m_settings.Material, (int)pass, MeshTopology.Triangles, 3);
		}

		public static void Record(RenderGraph renderGraph, Vector2Int bufferSize, bool useHDR, AoSARenderPipelineSettings settings, in CameraRendererTextures textures, RendererListHandle listHandle)
		{
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(Sampler.name, out BlurPass pass, Sampler);
			pass.m_bufferSize = bufferSize;
			pass.m_useHDR = useHDR;
			pass.m_settings = settings;
			pass.m_shadowBuffers = ReadTextures(builder, textures.shadowBuffers);
			pass.m_softBlurBuffers = WriteTextures(builder, textures.softBlurBuffers);
			pass.m_heavyBlurBuffers = WriteTextures(builder, textures.heavyBlurBuffers);
			builder.DependsOn(listHandle);
			builder.SetRenderFunc<BlurPass>((pass, context) => pass.Render(context));
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

		private static TextureHandle[] WriteTextures(RenderGraphBuilder builder, in TextureHandle[] textures)
		{
			TextureHandle[] newHandles = new TextureHandle[textures.Length];
			for (int i = 0; i < textures.Length; ++i)
			{
				newHandles[i] = builder.WriteTexture(textures[i]);
			}
			return newHandles;
		}
	}
}