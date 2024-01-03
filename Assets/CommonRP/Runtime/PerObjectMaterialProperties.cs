using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PerObjectMaterialProperties : MonoBehaviour
{
	private static readonly int LitColorId = Shader.PropertyToID("_BaseColor");
	private static readonly int ShadowedColorId = Shader.PropertyToID("_BaseShadowedColor");
	private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
	private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
	private static readonly int WorldSpaceUVGradientId = Shader.PropertyToID("_WorldSpaceUVGradient");

	private static readonly int BaseColorOverlayId = Shader.PropertyToID("_BaseColorOverlay");
	private static readonly int BaseColorSaturationId = Shader.PropertyToID("_BaseColorSaturation");

	private static MaterialPropertyBlock Block;

	[SerializeField] private Color m_litColor = Color.white;
	[SerializeField] private Color m_shadowedColor = Color.black;

	[SerializeField, Range(0f, 1f)] private float m_alphaCutoff = 0.5f;
	[SerializeField, Range(0f, 1f)] private float m_smoothness = 0.5f;
	[SerializeField] private Vector2 m_worldSpaceUVGradient = Vector2.one;

	[SerializeField] private Color m_overlay = Color.grey;
	[SerializeField] private float m_saturation = 1.0f;

	private void Awake()
	{
		OnValidate();
	}

	private void OnValidate()
	{
		Block ??= new();
		Block.SetColor(LitColorId, m_litColor);
		Block.SetColor(ShadowedColorId, m_shadowedColor);
		Block.SetFloat(CutoffId, m_alphaCutoff);
		Block.SetFloat(SmoothnessId, m_smoothness);
		Block.SetVector(WorldSpaceUVGradientId, m_worldSpaceUVGradient);
		Block.SetColor(BaseColorOverlayId, m_overlay);
		Block.SetFloat(BaseColorSaturationId, m_saturation);
		GetComponent<Renderer>().SetPropertyBlock(Block);
	}
}
