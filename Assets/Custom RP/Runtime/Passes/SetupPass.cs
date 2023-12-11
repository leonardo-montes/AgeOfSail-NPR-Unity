﻿using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class SetupPass
{
	static readonly ProfilingSampler sampler = new("Setup");

	static readonly int attachmentSizeID = Shader.PropertyToID("_CameraBufferSize");

	static readonly Color clearColor = new Color(0.5f, 0.5f, 0.0f, 1.0f);

	bool useIntermediateAttachments;

	TextureHandle colorAttachment, depthAttachment, edgeBreakupColor, edgeBreakupDepth, blurBuffer;

	Vector2Int attachmentSize;

	Camera camera;

	CameraClearFlags clearFlags;

	void Render(RenderGraphContext context)
	{
		context.renderContext.SetupCameraProperties(camera);
		CommandBuffer cmd = context.cmd;
		if (useIntermediateAttachments)
		{
			cmd.SetRenderTarget(
				colorAttachment,
				RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
				depthAttachment,
				RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
		}
		cmd.ClearRenderTarget(
			clearFlags <= CameraClearFlags.Depth,
			clearFlags <= CameraClearFlags.Color,
			clearFlags == CameraClearFlags.Color ?
				camera.backgroundColor.linear : Color.clear);

		cmd.SetRenderTarget(
			edgeBreakupColor,
			RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
			edgeBreakupDepth,
			RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
		cmd.ClearRenderTarget(
			true, true, clearColor);

		cmd.SetGlobalVector(attachmentSizeID, new Vector4(
			1f / attachmentSize.x, 1f / attachmentSize.y,
			attachmentSize.x, attachmentSize.y));
		context.renderContext.ExecuteCommandBuffer(cmd);
		cmd.Clear();
	}

	public static CameraRendererTextures Record(
		RenderGraph renderGraph,
		bool useIntermediateAttachments,
		bool copyColor,
		bool copyDepth,
		bool useHDR,
		Vector2Int attachmentSize,
		Camera camera)
	{
		using RenderGraphBuilder builder = renderGraph.AddRenderPass(
			sampler.name, out SetupPass pass, sampler);
		pass.useIntermediateAttachments = useIntermediateAttachments;
		pass.attachmentSize = attachmentSize;
		pass.camera = camera;
		pass.clearFlags = camera.clearFlags;

		TextureHandle colorAttachment, depthAttachment;
		TextureHandle colorCopy = default, depthCopy = default;
		TextureHandle edgeBreakupColor, edgeBreakupDepth;
		TextureHandle blurBuffer;
		TextureDesc desc;
		if (useIntermediateAttachments)
		{
			if (pass.clearFlags > CameraClearFlags.Color)
			{
				pass.clearFlags = CameraClearFlags.Color;
			}
			desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
			{
				colorFormat = SystemInfo.GetGraphicsFormat(
					useHDR ? DefaultFormat.HDR : DefaultFormat.LDR),
				name = "Color Attachment"
			};

			colorAttachment = pass.colorAttachment =
				builder.WriteTexture(renderGraph.CreateTexture(desc));
			if (copyColor)
			{
				desc.name = "Color Copy";
				colorCopy = renderGraph.CreateTexture(desc);
			}
			desc.depthBufferBits = DepthBits.Depth32;
			desc.name = "Depth Attachment";
			depthAttachment = pass.depthAttachment =
				builder.WriteTexture(renderGraph.CreateTexture(desc));
			if (copyDepth)
			{
				desc.name = "Depth Copy";
				depthCopy = renderGraph.CreateTexture(desc);
			}
		}
		else
		{
			colorAttachment = depthAttachment =
				pass.colorAttachment = pass.depthAttachment =
				builder.WriteTexture(renderGraph.ImportBackbuffer(
					BuiltinRenderTextureType.CameraTarget));
		}

		desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
		{
			colorFormat = GraphicsFormat.R16G16_UNorm,
			name = "Edge Breakup Color"
		};
		edgeBreakupColor = pass.edgeBreakupColor =
			builder.WriteTexture(renderGraph.CreateTexture(desc));

		desc.depthBufferBits = DepthBits.Depth32;
		desc.name = "Edge Breakup Depth";
		edgeBreakupDepth = pass.edgeBreakupDepth =
			builder.WriteTexture(renderGraph.CreateTexture(desc));

		desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
		{
			colorFormat = GraphicsFormat.R8G8B8A8_UNorm,
			name = "Blur Buffer"
		};
		blurBuffer = pass.blurBuffer = 
			builder.WriteTexture(renderGraph.CreateTexture(desc));

		builder.AllowPassCulling(false);
		builder.SetRenderFunc<SetupPass>(
			(pass, context) => pass.Render(context));

		return new CameraRendererTextures(
			colorAttachment, depthAttachment, colorCopy, depthCopy, edgeBreakupColor, edgeBreakupDepth, blurBuffer);
	}
}
