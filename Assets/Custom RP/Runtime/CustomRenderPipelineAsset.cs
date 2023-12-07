using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public partial class CustomRenderPipelineAsset : RenderPipelineAsset
{
	[SerializeField]
	CameraBufferSettings cameraBuffer = new()
	{
		allowHDR = true,
		renderScale = 1f,
		fxaa = new()
		{
			fixedThreshold = 0.0833f,
			relativeThreshold = 0.166f,
			subpixelBlending = 0.75f
		}
	};

	[SerializeField]
	bool
		useSRPBatcher = true,
		useLightsPerObject = true;

	[SerializeField]
	ShadowSettings shadows = default;

	[SerializeField]
	PostFXSettings postFXSettings = default;

	public enum ColorLUTResolution
	{ _16 = 16, _32 = 32, _64 = 64 }

	[SerializeField]
	ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;

	[SerializeField]
	Shader cameraRendererShader = default;

	[Header("Deprecated Settings")]
	[SerializeField, Tooltip("Dynamic batching is no longer used.")]
	bool useDynamicBatching;

	[SerializeField, Tooltip("GPU instancing is always enabled.")]
	bool useGPUInstancing;

	protected override RenderPipeline CreatePipeline() =>
		new CustomRenderPipeline(cameraBuffer, useSRPBatcher,
			useLightsPerObject, shadows, postFXSettings,
			(int)colorLUTResolution, cameraRendererShader);
}
