using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace AoSA.RenderPipeline
{
	public readonly ref struct CameraRendererTextures
	{
		public readonly TextureHandle litColorBuffer, shadowedColorBuffer, overlaySaturationBuffer, depthAttachment,  warpColor, warpDepth;
		public readonly TextureHandle[] shadowBuffers, blurBuffers;

		public CameraRendererTextures(TextureHandle litColorBuffer, TextureHandle shadowedColorBuffer, TextureHandle overlaySaturationBuffer,
			TextureHandle depthAttachment, TextureHandle warpColor, TextureHandle warpDepth, TextureHandle[] shadowBuffers, TextureHandle[] blurBuffers)
		{
			this.litColorBuffer = litColorBuffer;
			this.shadowedColorBuffer = shadowedColorBuffer;
			this.overlaySaturationBuffer = overlaySaturationBuffer;
			this.depthAttachment = depthAttachment;
			this.warpColor = warpColor;
			this.warpDepth = warpDepth;
			this.shadowBuffers = shadowBuffers;
			this.blurBuffers = blurBuffers;
		}
	}
}