using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoSA.RenderPipeline
{
	public class BlurPass
	{
		private static readonly ProfilingSampler Sampler = new("Blur Pass");

		private static int[] PingRTsId = new int[5]
		{
			Shader.PropertyToID("_PingRT0"),
			Shader.PropertyToID("_PingRT1"),
			Shader.PropertyToID("_PingRT2"),
			Shader.PropertyToID("_PingRT3"),
			Shader.PropertyToID("_PingRT4")
		};
		private static int[] PongRTsId = new int[5]
		{
			Shader.PropertyToID("_PongRT0"),
			Shader.PropertyToID("_PongRT1"),
			Shader.PropertyToID("_PongRT2"),
			Shader.PropertyToID("_PongRT3"),
			Shader.PropertyToID("_PongRT4")
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

		private TextureHandle[] m_shadowBuffers, m_blurBuffers;
		private Vector2Int m_bufferSize;
		private bool m_useHDR;
		private AoSARenderPipelineSettings m_settings;

		private void Render(RenderGraphContext context)
		{
			RenderTargetIdentifier[] pingRTs = new RenderTargetIdentifier[m_shadowBuffers.Length];
			RenderTargetIdentifier[] pongRTs = new RenderTargetIdentifier[m_shadowBuffers.Length];
			RenderTargetIdentifier[] shadowRTs = new RenderTargetIdentifier[m_shadowBuffers.Length];
			RenderTargetIdentifier[] blurRTs = new RenderTargetIdentifier[m_shadowBuffers.Length];
			for (int i = 0; i < m_shadowBuffers.Length; ++i)
			{
				context.cmd.GetTemporaryRT(PingRTsId[i], Mathf.CeilToInt(m_bufferSize.x / m_settings.downsampleAmount), Mathf.CeilToInt(m_bufferSize.y / m_settings.downsampleAmount), 0, FilterMode.Point, m_useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
				context.cmd.GetTemporaryRT(PongRTsId[i], Mathf.CeilToInt(m_bufferSize.x / m_settings.downsampleAmount), Mathf.CeilToInt(m_bufferSize.y / m_settings.downsampleAmount), 0, FilterMode.Point, m_useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
			
				pingRTs[i] = new RenderTargetIdentifier(PingRTsId[i]);
				pongRTs[i] = new RenderTargetIdentifier(PongRTsId[i]);
				shadowRTs[i] = new RenderTargetIdentifier(m_shadowBuffers[i]);
				blurRTs[i] = new RenderTargetIdentifier(m_blurBuffers[i]);
			}

			context.cmd.SetGlobalFloat(BlurRadiusId, m_settings.blurRadius);

			Draw(context.cmd, shadowRTs, pingRTs, AoSARenderPipeline.Pass.BlurHorizontalArray);
			Draw(context.cmd, pingRTs,   pongRTs, AoSARenderPipeline.Pass.BlurVerticalArray);

			Draw(context.cmd, pongRTs,   blurRTs, AoSARenderPipeline.Pass.CopyArray);

			for (int i = 0; i < m_shadowBuffers.Length; ++i)
			{
				context.cmd.ReleaseTemporaryRT(PingRTsId[i]);
				context.cmd.ReleaseTemporaryRT(PongRTsId[i]);
			}

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
			pass.m_blurBuffers = WriteTextures(builder, textures.blurBuffers);
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