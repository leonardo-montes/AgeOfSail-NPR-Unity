using UnityEngine.Experimental.Rendering.RenderGraphModule;

public readonly ref struct CameraRendererTextures
{
	public readonly TextureHandle
		colorAttachment, depthAttachment,
		colorCopy, depthCopy,
		edgeBreakupColor, edgeBreakupDepth;

	public CameraRendererTextures(
		TextureHandle colorAttachment,
		TextureHandle depthAttachment,
		TextureHandle colorCopy,
		TextureHandle depthCopy,
		TextureHandle edgeBreakupColor,
		TextureHandle edgeBreakupDepth)
	{
		this.colorAttachment = colorAttachment;
		this.depthAttachment = depthAttachment;
		this.colorCopy = colorCopy;
		this.depthCopy = depthCopy;
		this.edgeBreakupColor = edgeBreakupColor;
		this.edgeBreakupDepth = edgeBreakupDepth;
	}
}
