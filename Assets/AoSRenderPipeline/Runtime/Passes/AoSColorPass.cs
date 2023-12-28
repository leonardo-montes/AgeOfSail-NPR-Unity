using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

namespace AoS.RenderPipeline
{
	public class ColorPass
	{
		private static readonly ProfilingSampler Sampler = new("Color Pass (Opaque Geometry)");

		private static readonly ShaderTagId[] ShaderTagIDs = { new("ColorPass") };
		private static readonly int BlurBufferId = Shader.PropertyToID("_BlurBuffer");
		private static readonly int FinalShadowBufferId = Shader.PropertyToID("_FinalShadowBuffer");
		private static readonly int SourceId = Shader.PropertyToID("_Source0");


		private RendererListHandle m_list;
		private TextureHandle m_colorAttachment, m_depthAttachment, m_blurBuffer;
		private AoSRenderPipelineSettings m_settings;
		private Vector2Int m_bufferSize;
		private bool m_useHDR;
		private Camera m_camera;

		void Render(RenderGraphContext context)
		{
			context.cmd.GetTemporaryRT(FinalShadowBufferId, m_bufferSize.x, m_bufferSize.y, 0, FilterMode.Point, m_useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
			Draw(context.cmd, m_colorAttachment, FinalShadowBufferId, AoSRenderPipeline.Pass.Copy);

			context.cmd.SetRenderTarget(m_colorAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, m_depthAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
			context.cmd.ClearRenderTarget(true, true, m_camera.backgroundColor.linear);

			context.cmd.SetGlobalTexture(BlurBufferId, m_blurBuffer);

			context.cmd.DrawRendererList(m_list);

			context.cmd.ReleaseTemporaryRT(FinalShadowBufferId);

			context.renderContext.ExecuteCommandBuffer(context.cmd);
			context.cmd.Clear();
		}

		private void Draw(CommandBuffer buffer, RenderTargetIdentifier from, RenderTargetIdentifier to, AoSRenderPipeline.Pass pass)
		{
			buffer.SetGlobalTexture(SourceId, from);
			buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
			buffer.DrawProcedural(Matrix4x4.identity, m_settings.Material, (int)pass, MeshTopology.Triangles, 3);
		}

		public static void Record(RenderGraph renderGraph, Camera camera, CullingResults cullingResults, Vector2Int bufferSize, bool useHDR, AoSRenderPipelineSettings settings, in CameraRendererTextures textures)
		{
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(Sampler.name, out ColorPass pass, Sampler);

			pass.m_list = builder.UseRendererList(renderGraph.CreateRendererList(
				new RendererListDesc(ShaderTagIDs, cullingResults, camera)
				{
					sortingCriteria = SortingCriteria.CommonOpaque,
					rendererConfiguration = PerObjectData.None,
					renderQueueRange = RenderQueueRange.opaque
				}));

			pass.m_camera = camera;
			pass.m_bufferSize = bufferSize;
			pass.m_useHDR = useHDR;
			pass.m_colorAttachment = builder.ReadWriteTexture(textures.colorAttachment);
			pass.m_depthAttachment = builder.ReadWriteTexture(textures.depthAttachment);
			pass.m_blurBuffer = builder.ReadTexture(textures.blurBuffer);
			pass.m_settings = settings;

			builder.SetRenderFunc<ColorPass>((pass, context) => pass.Render(context));
		}
	}
}