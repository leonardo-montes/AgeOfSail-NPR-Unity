using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace AoS.RenderPipeline
{
	/// <summary>
	/// Register all the different textures and setup the camera for rendering.
	/// </summary>
	public class SetupPass
	{
		private static readonly ProfilingSampler Sampler = new("Setup");
		private static readonly int AttachmentSizeID = Shader.PropertyToID("_CameraBufferSize");

		private Vector2Int m_attachmentSize;
		private Camera m_camera;

		void Render(RenderGraphContext context)
		{
			// Setup the camera for rendering
			context.renderContext.SetupCameraProperties(m_camera);

			// Set resolution for shaders
			CommandBuffer cmd = context.cmd;
			cmd.SetGlobalVector(AttachmentSizeID, new Vector4(1.0f / m_attachmentSize.x, 1.0f / m_attachmentSize.y, m_attachmentSize.x, m_attachmentSize.y));
			
			// Execute the command buffer
			context.renderContext.ExecuteCommandBuffer(cmd);
			cmd.Clear();
		}

		public static CameraRendererTextures Record(RenderGraph renderGraph, bool useHDR, Vector2Int attachmentSize, Camera camera, AoSRenderPipelineSettings settings)
		{
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(Sampler.name, out SetupPass pass, Sampler);
			
			pass.m_attachmentSize = attachmentSize;
			pass.m_camera = camera;

			TextureHandle colorAttachment, depthAttachment, warpColor, warpDepth, heavyBlurBuffer, softBlurBuffer, finalShadowBuffer;
			TextureDesc desc;

			// Register the color texture
			desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
			{
				colorFormat = SystemInfo.GetGraphicsFormat(useHDR ? DefaultFormat.HDR : DefaultFormat.LDR),
				name = "Color Attachment"
			};
			colorAttachment = renderGraph.CreateTexture(desc);

			// Register the final shadow buffer
			desc.name = "Final Shadow Buffer";
			finalShadowBuffer = renderGraph.CreateTexture(desc);

			// Register the depth texture
			desc.depthBufferBits = DepthBits.Depth32;
			desc.name = "Depth Attachment";
			depthAttachment = renderGraph.CreateTexture(desc);

			// Register the warp color and depth textures
			desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
			{
				colorFormat = GraphicsFormat.R8G8_UNorm,
				name = "Warp Pass Color"
			};
			warpColor = renderGraph.CreateTexture(desc);

			desc.depthBufferBits = DepthBits.Depth32;
			desc.name = "Warp Pass Depth";
			warpDepth = renderGraph.CreateTexture(desc);

			// Register the heavy blur buffer
			desc = new TextureDesc(Mathf.CeilToInt(attachmentSize.x / settings.heavyBlurDownsample), Mathf.CeilToInt(attachmentSize.y / settings.heavyBlurDownsample))
			{
				colorFormat = SystemInfo.GetGraphicsFormat(useHDR ? DefaultFormat.HDR : DefaultFormat.LDR),
				name = "Heavy Blur Buffer"
			};
			heavyBlurBuffer = renderGraph.CreateTexture(desc);

			// Register the soft blur buffer
			desc = new TextureDesc(Mathf.CeilToInt(attachmentSize.x / settings.softBlurDownsample), Mathf.CeilToInt(attachmentSize.y / settings.softBlurDownsample))
			{
				colorFormat = SystemInfo.GetGraphicsFormat(useHDR ? DefaultFormat.HDR : DefaultFormat.LDR),
				name = "Soft Blur Buffer"
			};
			softBlurBuffer = renderGraph.CreateTexture(desc);

			// Force rendering the pass
			builder.AllowPassCulling(false);

			// Render
			builder.SetRenderFunc<SetupPass>((pass, context) => pass.Render(context));

			// Keep track of the textures
			return new CameraRendererTextures(colorAttachment, depthAttachment, warpColor, warpDepth, heavyBlurBuffer, softBlurBuffer, finalShadowBuffer);
		}
	}
}