using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoS.RenderPipeline
{
	public class SetupPass
	{
		private static readonly ProfilingSampler Sampler = new("Setup");
		private static readonly int AttachmentSizeID = Shader.PropertyToID("_CameraBufferSize");

		private Vector2Int m_attachmentSize;
		private Camera m_camera;

		void Render(RenderGraphContext context)
		{
			context.renderContext.SetupCameraProperties(m_camera);
			CommandBuffer cmd = context.cmd;
			cmd.SetGlobalVector(AttachmentSizeID, new Vector4(1.0f / m_attachmentSize.x, 1.0f / m_attachmentSize.y, m_attachmentSize.x, m_attachmentSize.y));
			context.renderContext.ExecuteCommandBuffer(cmd);
			cmd.Clear();
		}

		public static CameraRendererTextures Record(RenderGraph renderGraph, bool useHDR, Vector2Int attachmentSize, Camera camera)
		{
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(Sampler.name, out SetupPass pass, Sampler);
			pass.m_attachmentSize = attachmentSize;
			pass.m_camera = camera;

			TextureHandle colorAttachment, depthAttachment, warpColor, warpDepth, blurBuffer;
			TextureDesc desc;

			desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
			{
				colorFormat = SystemInfo.GetGraphicsFormat(useHDR ? DefaultFormat.HDR : DefaultFormat.LDR),
				name = "Color Attachment"
			};
			colorAttachment = renderGraph.CreateTexture(desc);

			desc.depthBufferBits = DepthBits.Depth32;
			desc.name = "Depth Attachment";
			depthAttachment = renderGraph.CreateTexture(desc);

			desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
			{
				colorFormat = GraphicsFormat.R8G8_UNorm,
				name = "Warp Pass Color"
			};
			warpColor = renderGraph.CreateTexture(desc);

			desc.depthBufferBits = DepthBits.Depth32;
			desc.name = "Warp Pass Depth";
			warpDepth = renderGraph.CreateTexture(desc);

			desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
			{
				colorFormat = SystemInfo.GetGraphicsFormat(useHDR ? DefaultFormat.HDR : DefaultFormat.LDR),
				name = "Blur Buffer"
			};
			blurBuffer = renderGraph.CreateTexture(desc);

			builder.AllowPassCulling(false);
			builder.SetRenderFunc<SetupPass>((pass, context) => pass.Render(context));

			return new CameraRendererTextures(colorAttachment, depthAttachment, warpColor, warpDepth, blurBuffer);
		}
	}
}