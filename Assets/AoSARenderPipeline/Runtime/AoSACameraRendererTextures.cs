using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace AoSA.RenderPipeline
{
	public readonly ref struct CameraRendererTextures
	{
		public readonly TextureHandle litColorBuffer, shadowedColorBuffer, overlaySaturationBuffer, depthAttachment,  warpColor, warpDepth;
		public readonly TextureHandle[] shadowBuffers, softBlurBuffers, heavyBlurBuffers, bloomBuffers;

		public CameraRendererTextures(TextureHandle litColorBuffer, TextureHandle shadowedColorBuffer, TextureHandle overlaySaturationBuffer,
			TextureHandle depthAttachment, TextureHandle warpColor, TextureHandle warpDepth, TextureHandle[] shadowBuffers, TextureHandle[] heavyBlurBuffers,
			TextureHandle[] softBlurBuffers, TextureHandle[] bloomBuffers)
		{
			this.litColorBuffer = litColorBuffer;
			this.shadowedColorBuffer = shadowedColorBuffer;
			this.overlaySaturationBuffer = overlaySaturationBuffer;
			this.depthAttachment = depthAttachment;
			this.warpColor = warpColor;
			this.warpDepth = warpDepth;
			this.shadowBuffers = shadowBuffers;
			this.heavyBlurBuffers = heavyBlurBuffers;
			this.softBlurBuffers = softBlurBuffers;
			this.bloomBuffers = bloomBuffers;
		}
	}
}