using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace AoS.RenderPipeline
{
	/// <summary>
	/// Struct containing all textures used for rendering.
	/// </summary>
	public readonly ref struct CameraRendererTextures
	{
		public readonly TextureHandle colorAttachment, depthAttachment, warpColor, warpDepth, heavyBlurBuffer, softBlurBuffer, finalShadowBuffer;

		public CameraRendererTextures(TextureHandle colorAttachment, TextureHandle depthAttachment, TextureHandle warpColor, TextureHandle warpDepth,
			TextureHandle heavyBlurBuffer, TextureHandle softBlurBuffer, TextureHandle finalShadowBuffer)
		{
			this.colorAttachment = colorAttachment;
			this.depthAttachment = depthAttachment;
			this.warpColor = warpColor;
			this.warpDepth = warpDepth;
			this.heavyBlurBuffer = heavyBlurBuffer;
			this.softBlurBuffer = softBlurBuffer;
			this.finalShadowBuffer = finalShadowBuffer;
		}
	}
}