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

		/// <summary>
		/// 
		/// </summary>
		/// <param name="context"></param>
		private void Render(RenderGraphContext context)
		{
			context.renderContext.SetupCameraProperties(m_camera);
			
			context.cmd.SetGlobalVector(AttachmentSizeID, new Vector4(1.0f / m_attachmentSize.x, 1.0f / m_attachmentSize.y, m_attachmentSize.x, m_attachmentSize.y));

			context.renderContext.ExecuteCommandBuffer(context.cmd);
			context.cmd.Clear();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="renderGraph"></param>
		/// <param name="useHDR"></param>
		/// <param name="attachmentSize"></param>
		/// <param name="camera"></param>
		/// <param name="settings"></param>
		/// <param name="totalLightCount"></param>
		/// <returns></returns>
		public static CameraRendererTextures Record(RenderGraph renderGraph, bool useHDR, Vector2Int attachmentSize, Camera camera,
			AoSARenderPipelineSettings settings, int totalLightCount)
		{
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(Sampler.name, out SetupPass pass, Sampler);
			pass.m_attachmentSize = attachmentSize;
			pass.m_camera = camera;

			int bufferCount;

			TextureHandle litColorBuffer, shadowedColorBuffer, depthAttachment, warpColor, warpDepth;
			TextureHandle[] shadowBuffers, softBlurBuffers, heavyBlurBuffers, bloomBuffers;
			TextureDesc desc;

			GraphicsFormat format = SystemInfo.GetGraphicsFormat(useHDR ? DefaultFormat.HDR : DefaultFormat.LDR);

			// Default buffers
			desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
			{
				colorFormat = format,
				name = "Lit Color Buffer"
			};
			litColorBuffer = renderGraph.CreateTexture(desc);

			desc.name = "Shadowed Color Buffer";
			shadowedColorBuffer = renderGraph.CreateTexture(desc);
			
			// Bloom buffers
			bufferCount = Mathf.CeilToInt((float)totalLightCount / 4);
			bloomBuffers = new TextureHandle[bufferCount];
			for (int i = 0; i < bufferCount; ++i)
			{
				desc.name = string.Format("Bloom Buffer {0}", i);
				bloomBuffers[i] = renderGraph.CreateTexture(desc);
			}

			// Shadow buffers
			bufferCount = Mathf.CeilToInt((float)totalLightCount / 2);
			shadowBuffers = new TextureHandle[bufferCount];
			for (int i = 0; i < bufferCount; ++i)
			{
				desc.name = string.Format("Shadow Buffer {0}", i);
				shadowBuffers[i] = renderGraph.CreateTexture(desc);
			}

			// Depth buffer
			desc.depthBufferBits = DepthBits.Depth32;
			desc.name = "Depth Attachment";
			depthAttachment = renderGraph.CreateTexture(desc);

			// Warp buffer (color and depth)
			desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
			{
				colorFormat = GraphicsFormat.R8G8_UNorm,
				name = "Warp Pass Color"
			};
			warpColor = renderGraph.CreateTexture(desc);

			desc.depthBufferBits = DepthBits.Depth32;
			desc.name = "Warp Pass Depth";
			warpDepth = renderGraph.CreateTexture(desc);

			// Soft blur buffers
			softBlurBuffers = new TextureHandle[bufferCount];
			desc = new TextureDesc(Mathf.CeilToInt(attachmentSize.x / settings.softBlurDownsample), Mathf.CeilToInt(attachmentSize.y / settings.softBlurDownsample))
			{
				colorFormat = format
			};
			for (int i = 0; i < bufferCount; ++i)
			{
				desc.name = string.Format("Soft Blur Buffer {0}", i);
				softBlurBuffers[i] = renderGraph.CreateTexture(desc);
			}

			// Heavy blur buffers
			heavyBlurBuffers = new TextureHandle[bufferCount];
			desc = new TextureDesc(Mathf.CeilToInt(attachmentSize.x / settings.heavyBlurDownsample), Mathf.CeilToInt(attachmentSize.y / settings.heavyBlurDownsample))
			{
				colorFormat = format
			};
			for (int i = 0; i < bufferCount; ++i)
			{
				desc.name = string.Format("Heavy Blur Buffer {0}", i);
				heavyBlurBuffers[i] = renderGraph.CreateTexture(desc);
			}

			builder.AllowPassCulling(false);
			builder.SetRenderFunc<SetupPass>((pass, context) => pass.Render(context));

			return new CameraRendererTextures(litColorBuffer, shadowedColorBuffer, depthAttachment, warpColor, warpDepth, shadowBuffers,
				heavyBlurBuffers, softBlurBuffers, bloomBuffers);
		}
	}
}