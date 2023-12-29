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
		private static int TempRTId = Shader.PropertyToID("_TempRT");
		private static int[] SourceIds = new int[9]
		{
			Shader.PropertyToID("_Source0"),
			Shader.PropertyToID("_Source1"),
			Shader.PropertyToID("_Source2"),
			Shader.PropertyToID("_Source3"),
			Shader.PropertyToID("_Source4"),
			Shader.PropertyToID("_Source5"),
			Shader.PropertyToID("_Source6"),
			Shader.PropertyToID("_Source7"),
			Shader.PropertyToID("_Source8")
		};

		private AoSARenderPipelineSettings m_settings;
		
		private TextureHandle m_litColorBuffer, m_shadowedColorBuffer, m_overlaySaturationBuffer, m_shadowBuffer0;
		private TextureHandle[] m_blurBuffers;
		private Vector2Int m_bufferSize;
		private bool m_useHDR;

		void Render(RenderGraphContext context)
		{
			context.cmd.GetTemporaryRT(TempRTId, m_bufferSize.x, m_bufferSize.y, 0, FilterMode.Point, m_useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);

			Draw(context.cmd, m_litColorBuffer, TempRTId, AoSARenderPipeline.Pass.Copy);

			context.cmd.SetGlobalFloat(ShadowThresholdId, m_settings.shadowThreshold);
			context.cmd.SetGlobalFloat(ShadowThresholdSoftnessId, m_settings.shadowThresholdSoftness);

			context.cmd.SetGlobalTexture(SourceIds[0], TempRTId);
			context.cmd.SetGlobalTexture(SourceIds[1], m_shadowedColorBuffer);
			context.cmd.SetGlobalTexture(SourceIds[2], m_overlaySaturationBuffer);
			context.cmd.SetGlobalTexture(SourceIds[3], m_shadowBuffer0);
			for (int i = 0; i < m_blurBuffers.Length; ++i)
				context.cmd.SetGlobalTexture(SourceIds[4 + i], m_blurBuffers[i]);

			context.cmd.SetRenderTarget(m_litColorBuffer, BuiltinRenderTextureType.None);
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
			pass.m_overlaySaturationBuffer = builder.ReadTexture(textures.overlaySaturationBuffer);
			pass.m_shadowBuffer0 = builder.ReadTexture(textures.shadowBuffers[0]);
			pass.m_blurBuffers = ReadTextures(builder, textures.blurBuffers);
			builder.DependsOn(listHandle);
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
	}
}