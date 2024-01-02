using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using LightType = UnityEngine.LightType;

namespace AoS.RenderPipeline
{
	public partial class AoSRenderPipeline
	{
		partial void InitializeForEditor();

		partial void DisposeForEditor();

	#if UNITY_EDITOR

		partial void InitializeForEditor() =>
			Lightmapping.SetDelegate(lightsDelegate);

		partial void DisposeForEditor() => Lightmapping.ResetDelegate();

		private static readonly Lightmapping.RequestLightsDelegate lightsDelegate = (Light[] lights, NativeArray<LightDataGI> output) =>
		{
			var lightData = new LightDataGI();
			for (int i = 0; i < lights.Length; i++)
			{
				Light light = lights[i];
				switch (light.type)
				{
					case LightType.Directional:
						var directionalLight = new DirectionalLight();
						LightmapperUtils.Extract(light, ref directionalLight);
						lightData.Init(ref directionalLight);
						break;
					default:
						lightData.InitNoBake(light.GetInstanceID());
						break;
				}
				lightData.falloff = FalloffType.InverseSquared;
				output[i] = lightData;
			}
		};

	#endif
	}
}