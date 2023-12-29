using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoSA.RenderPipeline
{
	public class SetupPass
	{
		private static readonly ProfilingSampler Sampler = new("Setup");
		private static readonly int AttachmentSizeID = Shader.PropertyToID("_CameraBufferSize");

		private Vector2Int m_attachmentSize;
		private Camera m_camera;

		private void Render(RenderGraphContext context)
		{
			context.renderContext.SetupCameraProperties(m_camera);
			
			context.cmd.SetGlobalVector(AttachmentSizeID, new Vector4(1.0f / m_attachmentSize.x, 1.0f / m_attachmentSize.y, m_attachmentSize.x, m_attachmentSize.y));
			
			context.renderContext.ExecuteCommandBuffer(context.cmd);
			context.cmd.Clear();
		}

		public static CameraRendererTextures Record(RenderGraph renderGraph, bool useHDR, Vector2Int attachmentSize, Camera camera, int totalLightCount)
		{
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(Sampler.name, out SetupPass pass, Sampler);
			pass.m_attachmentSize = attachmentSize;
			pass.m_camera = camera;

			TextureHandle litColorBuffer, shadowedColorBuffer, overlaySaturationBuffer, depthAttachment, warpColor, warpDepth;
			TextureHandle[] shadowBuffers, blurBuffers;
			TextureDesc desc;

			desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
			{
				colorFormat = SystemInfo.GetGraphicsFormat(useHDR ? DefaultFormat.HDR : DefaultFormat.LDR),
				name = "Lit Color Buffer"
			};
			litColorBuffer = renderGraph.CreateTexture(desc);

			desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
			{
				colorFormat = SystemInfo.GetGraphicsFormat(useHDR ? DefaultFormat.HDR : DefaultFormat.LDR),
				name = "Shadowed Color Buffer"
			};
			shadowedColorBuffer = renderGraph.CreateTexture(desc);

			desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
			{
				colorFormat = SystemInfo.GetGraphicsFormat(useHDR ? DefaultFormat.HDR : DefaultFormat.LDR),
				name = "Overlay Saturation Buffer"
			};
			overlaySaturationBuffer = renderGraph.CreateTexture(desc);

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

			int bufferCount = 1 + Mathf.CeilToInt((float)(totalLightCount - 1) / 2);
			shadowBuffers = new TextureHandle[bufferCount];
			blurBuffers = new TextureHandle[bufferCount];
			for (int i = 0; i < bufferCount; ++i)
			{
				desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
				{
					colorFormat = SystemInfo.GetGraphicsFormat(useHDR ? DefaultFormat.HDR : DefaultFormat.LDR),
					name = string.Format("Shadow Buffer {0}", i)
				};
				shadowBuffers[i] = renderGraph.CreateTexture(desc);

				desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
				{
					colorFormat = SystemInfo.GetGraphicsFormat(useHDR ? DefaultFormat.HDR : DefaultFormat.LDR),
					name = string.Format("Blur Buffer {0}", i)
				};
				blurBuffers[i] = renderGraph.CreateTexture(desc);
			}

			builder.AllowPassCulling(false);
			builder.SetRenderFunc<SetupPass>((pass, context) => pass.Render(context));

			return new CameraRendererTextures(litColorBuffer, shadowedColorBuffer, overlaySaturationBuffer, depthAttachment,
				warpColor, warpDepth, shadowBuffers, blurBuffers);
		}
	}
}