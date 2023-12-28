using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AoS.RenderPipeline
{
	[CreateAssetMenu(menuName = "Rendering/AoS Render Pipeline")]
	public partial class AoSRenderPipelineAsset : RenderPipelineAsset
	{
		public AoSRenderPipelineSettings settings;
		protected override UnityEngine.Rendering.RenderPipeline CreatePipeline() => new AoSRenderPipeline(settings);
	}
}