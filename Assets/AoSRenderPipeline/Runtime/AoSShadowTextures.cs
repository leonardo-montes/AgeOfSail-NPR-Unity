using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace AoS.RenderPipeline
{
	public readonly ref struct ShadowTextures
	{
		public readonly TextureHandle directionalAtlas;

		public ShadowTextures(TextureHandle directionalAtlas)
		{
			this.directionalAtlas = directionalAtlas;
		}
	}
}