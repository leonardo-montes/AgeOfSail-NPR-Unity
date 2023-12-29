using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace AoSA.RenderPipeline
{
	public readonly ref struct ShadowTextures
	{
		public readonly TextureHandle directionalAtlas;
		public readonly TextureHandle otherAtlas;

		public ShadowTextures(TextureHandle directionalAtlas, TextureHandle otherAtlas)
		{
			this.directionalAtlas = directionalAtlas;
			this.otherAtlas = otherAtlas;
		}
	}
}