using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AoSA.RenderPipeline
{
	[CreateAssetMenu(menuName = "Rendering/AoSA Render Pipeline")]
	public partial class AoSARenderPipelineAsset : RenderPipelineAsset
	{
		public AoSARenderPipelineSettings settings;
		protected override UnityEngine.Rendering.RenderPipeline CreatePipeline() => new AoSARenderPipeline(settings);
	}
}