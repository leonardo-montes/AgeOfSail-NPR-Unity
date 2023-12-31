using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoSA.RenderPipeline
{
	public class CompositingPass
	{
		private static readonly ProfilingSampler m_sampler = new("CompositingPass");

		private static int ShadowThresholdId = Shader.PropertyToID("_ShadowThreshold");
		private static int ShadowThresholdSoftnessId = Shader.PropertyToID("_ShadowThresholdSoftness");
		private static int ShadowInnerGlowId = Shader.PropertyToID("_ShadowInnerGlow");
		private static int ShadowStepCountId = Shader.PropertyToID("_ShadowStepCount");

		private static int TempRTId = Shader.PropertyToID("_TempRT");
		private static int[] SourceIds = new int[14]
		{
			Shader.PropertyToID("_Source0"),
			Shader.PropertyToID("_Source1"),
			Shader.PropertyToID("_Source2"),
			Shader.PropertyToID("_Source3"),
			Shader.PropertyToID("_Source4"),
			Shader.PropertyToID("_Source5"),
			Shader.PropertyToID("_Source6"),
			Shader.PropertyToID("_Source7"),
			Shader.PropertyToID("_Source8"),
			Shader.PropertyToID("_Source9"),
			Shader.PropertyToID("_Source10"),
			Shader.PropertyToID("_Source11"),
			Shader.PropertyToID("_Source12"),
			Shader.PropertyToID("_Source13")
		};

		private AoSARenderPipelineSettings m_settings;
		
		private TextureHandle m_litColorBuffer, m_shadowedColorBuffer;
		private TextureHandle[] m_softBlurBuffers, m_heavyBlurBuffers, m_bloomBuffers;
		private Vector2Int m_bufferSize;
		private bool m_useHDR;

		void Render(RenderGraphContext context)
		{
			context.cmd.GetTemporaryRT(TempRTId, m_bufferSize.x, m_bufferSize.y, 0, FilterMode.Point, m_useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);

			Draw(context.cmd, m_litColorBuffer, TempRTId, AoSARenderPipeline.Pass.Copy);

			context.cmd.SetGlobalInt(ShadowStepCountId, m_settings.shadowStepCount);
			context.cmd.SetGlobalFloat(ShadowThresholdId, m_settings.shadowThreshold);
			context.cmd.SetGlobalFloat(ShadowThresholdSoftnessId, m_settings.shadowThresholdSoftness);
			context.cmd.SetGlobalFloat(ShadowInnerGlowId, m_settings.shadowInnerGlow);

			context.cmd.SetGlobalTexture(SourceIds[0], TempRTId);
			context.cmd.SetGlobalTexture(SourceIds[1], m_shadowedColorBuffer);
			for (int i = 0, j = 0; i < m_softBlurBuffers.Length; ++i, j += 2)
			{
				context.cmd.SetGlobalTexture(SourceIds[2 + j], m_softBlurBuffers[i]);
				context.cmd.SetGlobalTexture(SourceIds[3 + j], m_heavyBlurBuffers[i]);
			}

			int bloomBufferCount = m_bloomBuffers != null ? m_bloomBuffers.Length : 0;
			RenderTargetIdentifier[] renderTargets = new RenderTargetIdentifier[1 + bloomBufferCount];
			renderTargets[0] = m_litColorBuffer;
			for (int i = 0; i < bloomBufferCount; ++i)
				renderTargets[1 + i] = m_bloomBuffers[i];
			context.cmd.SetRenderTarget(renderTargets, BuiltinRenderTextureType.None);
			context.cmd.DrawProcedural(Matrix4x4.identity, m_settings.Material, (int)AoSARenderPipeline.Pass.CompositingPass, MeshTopology.Triangles, 3);

			context.cmd.ReleaseTemporaryRT(TempRTId);

			context.renderContext.ExecuteCommandBuffer(context.cmd);
			context.cmd.Clear();
		}

		private void Draw(CommandBuffer buffer, RenderTargetIdentifier from, RenderTargetIdentifier to, AoSARenderPipeline.Pass pass)
		{
			buffer.SetGlobalTexture(SourceIds[0], from);
			buffer.SetRenderTarget(to);
			buffer.DrawProcedural(Matrix4x4.identity, m_settings.Material, (int)pass, MeshTopology.Triangles, 3);
		}

		public static void Record(RenderGraph renderGraph, Vector2Int bufferSize, bool useHDR, AoSARenderPipelineSettings settings, in CameraRendererTextures textures, RendererListHandle listHandle)
		{
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(m_sampler.name, out CompositingPass pass, m_sampler);
			pass.m_settings = settings;
			pass.m_bufferSize = bufferSize;
			pass.m_useHDR = useHDR;
			pass.m_litColorBuffer = builder.ReadTexture(textures.litColorBuffer);
			pass.m_shadowedColorBuffer = builder.ReadTexture(textures.shadowedColorBuffer);
			pass.m_softBlurBuffers = ReadTextures(builder, textures.softBlurBuffers);
			pass.m_heavyBlurBuffers = ReadTextures(builder, textures.heavyBlurBuffers);
			pass.m_bloomBuffers = WriteTextures(builder, textures.bloomBuffers);
			builder.DependsOn(listHandle);
			builder.AllowPassCulling(false);
			builder.SetRenderFunc<CompositingPass>((pass, context) => pass.Render(context));
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
			if (textures == null || textures.Length <= 0)
				return null;

			TextureHandle[] newHandles = new TextureHandle[textures.Length];
			for (int i = 0; i < textures.Length; ++i)
			{
				newHandles[i] = builder.WriteTexture(textures[i]);
			}
			return newHandles;
		}
	}
}