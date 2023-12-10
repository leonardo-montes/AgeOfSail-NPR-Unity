using System;
using Unity.Mathematics;
using UnityEngine;

[System.Serializable]
public class AgeOfSailPipelineSettings
{
	public bool usePipeline = false;
	public Shader shader = null;

    [Min(1.0f)] public float blurPassDownsample = 1.0f;
    public float blurPassRadius = 7.0f;
    [Range(0.0f, 1.0f)] public float shadowThreshold = 0.5f;
    [Range(0.0f, 1.0f)] public float shadowThresholdSoftness = 0.01f;
	
	[NonSerialized] Material material;

	public Material Material
	{
		get
		{
			if (material == null && shader != null)
			{
				material = new(shader)
				{
					hideFlags = HideFlags.HideAndDontSave
				};
			}
			return material;
		}
	}
}
