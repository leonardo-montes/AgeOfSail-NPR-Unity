using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace AoS.RenderPipeline
{
	public readonly ref struct CameraRendererTextures
	{
		public readonly TextureHandle colorAttachment, depthAttachment, warpColor, warpDepth, blurBuffer;

		public CameraRendererTextures(TextureHandle colorAttachment, TextureHandle depthAttachment, TextureHandle warpColor, TextureHandle warpDepth, TextureHandle blurBuffer)
		{
			this.colorAttachment = colorAttachment;
			this.depthAttachment = depthAttachment;
			this.warpColor = warpColor;
			this.warpDepth = warpDepth;
			this.blurBuffer = blurBuffer;
		}
	}
}