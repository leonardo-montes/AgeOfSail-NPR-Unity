using UnityEngine.Experimental.Rendering.RenderGraphModule;

public readonly ref struct CameraRendererTextures
{
	public readonly TextureHandle
		colorAttachment, depthAttachment,
		colorCopy, depthCopy,
		edgeBreakupColor, edgeBreakupDepth,
		blurBuffer;

	public CameraRendererTextures(
		TextureHandle colorAttachment,
		TextureHandle depthAttachment,
		TextureHandle colorCopy,
		TextureHandle depthCopy,
		TextureHandle edgeBreakupColor,
		TextureHandle edgeBreakupDepth,
		TextureHandle blurBuffer)
	{
		this.colorAttachment = colorAttachment;
		this.depthAttachment = depthAttachment;
		this.colorCopy = colorCopy;
		this.depthCopy = depthCopy;
		this.edgeBreakupColor = edgeBreakupColor;
		this.edgeBreakupDepth = edgeBreakupDepth;
		this.blurBuffer = blurBuffer;
	}
}
