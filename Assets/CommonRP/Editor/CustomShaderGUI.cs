using AoSA.RenderPipeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class CustomShaderGUI : ShaderGUI
{
	enum VectorType { Vector2, Vector2Int, Vector3, Vector3Int, Vector4 }
	enum FramerateMode { Realtime = 0, _24fps = 1, _12fps = 2, _8fps = 3 }
	enum ZWriteMode { Off = 0, On = 1 }

	MaterialEditor editor;

	Object[] materials;

	MaterialProperty[] properties;

	bool showPresets, showWarpPass, showBaseSettings, shadowSettings, transparencySettings;

	bool Clipping
	{
		set => SetProperty("_Clipping", "_CLIPPING", value);
	}

	bool HasPremultiplyAlpha => HasProperty("_PremulAlpha");

	bool PremultiplyAlpha
	{
		set => SetProperty("_PremulAlpha", "_PREMULTIPLY_ALPHA", value);
	}

	BlendMode SrcBlend
	{
		set => SetProperty("_SrcBlend", (float)value);
	}

	BlendMode DstBlend
	{
		set => SetProperty("_DstBlend", (float)value);
	}

	bool ZWrite
	{
		set => SetProperty("_ZWrite", value ? 1f : 0f);
	}

	enum ShadowMode
	{
		On, Clip, Dither, Off
	}
	
	ShadowMode Shadows
	{
		set
		{
			if (SetProperty("_Shadows", (float)value))
			{
				SetKeyword("_SHADOWS_CLIP", value == ShadowMode.Clip);
				SetKeyword("_SHADOWS_DITHER", value == ShadowMode.Dither);
			}
		}
	}

	RenderQueue RenderQueue {
		set
		{
			foreach (Material m in materials)
			{
				m.renderQueue = (int)value;
			}
		}
	}

	public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
	{
		EditorGUI.BeginChangeCheck();
		//base.OnGUI(materialEditor, properties);
		editor = materialEditor;
		materials = materialEditor.targets;
		this.properties = properties;

		bool enabled;
		MaterialProperty prop0, prop1;

		// Default
		bool isDefault = FindProperty("_BaseColor", properties, false) != null;
		if (isDefault)
		{
			showBaseSettings = EditorGUILayout.Foldout(showBaseSettings, "Base settings (Age of Sail Render Pipeline)", true, EditorStyles.foldoutHeader);
			if (showBaseSettings)
			{
				++EditorGUI.indentLevel;
				
				prop0 = FindProperty("_BaseColor", properties);
				editor.ColorProperty(prop0, prop0.displayName);

				prop0 = FindProperty("_BaseMap", properties);
				editor.TextureProperty(prop0, prop0.displayName);

				// Lit shader only
				prop0 = FindProperty("_BaseShadowedMap", properties, false);
				if (prop0 != null)
				{
					prop1 = FindProperty("_BaseShadowedColor", properties);
					editor.TexturePropertyWithHDRColor(new GUIContent(prop0.displayName), prop0, prop1, true);

					prop0 = FindProperty("_BreakupMap", properties);
					editor.TextureProperty(prop0, prop0.displayName);
				}
				
				bool isAoSA = RenderPipelineManager.currentPipeline.GetType() == typeof(AoSARenderPipeline);
				if (!isAoSA)
				{
					prop0 = FindProperty("_BaseColorOverlay", properties);
					editor.ColorProperty(prop0, prop0.displayName);

					prop0 = FindProperty("_BaseColorSaturation", properties);
					editor.FloatProperty(prop0, prop0.displayName);
				}

				// Lit shader only
				if (FindProperty("_MaskMapToggle", properties, false) != null)
				{
					DrawPropertyToggleKeyword("_MaskMapToggle", "_MASK_MAP");

					enabled = GUI.enabled;
					++EditorGUI.indentLevel;
					GUI.enabled = enabled && FindProperty("_MaskMapToggle", properties).floatValue != 0.0f;
					prop0 = FindProperty("_MaskMap", properties);
					editor.TexturePropertySingleLine(new GUIContent(prop0.displayName), prop0);
					GUI.enabled = enabled;
					--EditorGUI.indentLevel;
					
					prop0 = FindProperty("_Smoothness", properties);
					editor.RangeProperty(prop0, "Specular highlight");

					GUI.enabled = enabled;
				}
				--EditorGUI.indentLevel;
				EditorGUILayout.Space();
			}
		}
		else
		{
			prop0 = FindProperty("_BaseMap", properties);
			editor.TextureProperty(prop0, prop0.displayName);
			EditorGUILayout.Space();
		}

		// Edge Breakup
		if (FindProperty("_UseSmoothUVGradient", properties, false) != null)
		{
			if (isDefault)
				enabled = FoldoutWithToggle(ref showWarpPass, "_Warp");
			else
			{
				EditorGUILayout.LabelField("Edge Breakup Warp Pass", EditorStyles.boldLabel);
				showWarpPass = true;
				enabled = GUI.enabled;
			}
			if (showWarpPass)
			{
				if (isDefault)
					++EditorGUI.indentLevel;
				DrawPropertyToggleKeyword("_UseSmoothUVGradient", "_USE_SMOOTH_UV_GRADIENT");
				DrawPropertyToggleKeyword("_CompensateRadialAngle", "_COMPENSATE_RADIAL_ANGLE");
				DrawPropertyToggleKeyword("_CompensateSkew", "_COMPENSATE_SKEW");
				DrawPropertyToggleKeyword("_CompensateDistance", "_COMPENSATE_DISTANCE");
				DrawPropertyToggleKeyword("_UseAnimatedLineBoil", "_USE_ANIMATED_LINE_BOIL");
				++EditorGUI.indentLevel;
				bool isEnabled = GUI.enabled;
				GUI.enabled = isEnabled && FindProperty("_UseAnimatedLineBoil", properties, false).floatValue != 0.0f;
				DrawPropertyEnum<FramerateMode>("_AnimatedLineBoilFramerate");
				GUI.enabled = isEnabled;
				--EditorGUI.indentLevel;

				DrawPropertyVector("_WorldSpaceUVGradient", VectorType.Vector2);
				
				prop0 = FindProperty("_WarpDistanceFadeMultiplier", properties);
				editor.FloatProperty(prop0, prop0.displayName);
					
				prop0 = FindProperty("_WarpWidthMultiplier", properties);
				editor.FloatProperty(prop0, prop0.displayName);
					
				prop0 = FindProperty("_WarpSkew", properties);
				editor.FloatProperty(prop0, prop0.displayName);
				if (isDefault)
					--EditorGUI.indentLevel;
				EditorGUILayout.Space();
			}
			GUI.enabled = enabled;
		}

		// Transparency
		transparencySettings = EditorGUILayout.Foldout(transparencySettings, "Transparency", true, EditorStyles.foldoutHeader);
		if (transparencySettings)
		{
			++EditorGUI.indentLevel;
			prop0 = FindProperty("_Cutoff", properties);
			editor.RangeProperty(prop0, prop0.displayName);

			DrawPropertyToggleKeyword("_Clipping", "_CLIPPING");
			DrawPropertyToggleKeyword("_PremulAlpha", "_PREMULTIPLY_ALPHA");
			DrawPropertyEnum<BlendMode>("_SrcBlend");
			DrawPropertyEnum<BlendMode>("_DstBlend");
			DrawPropertyEnum<ZWriteMode>("_ZWrite");
			DrawPropertyEnum<CullMode>("_Cull");
			--EditorGUI.indentLevel;
			EditorGUILayout.Space();
		}
		
		// Shadows
		if (FindProperty("_Shadows", properties, false) != null)
		{
			shadowSettings = EditorGUILayout.Foldout(shadowSettings, "Shadows", true, EditorStyles.foldoutHeader);
			if (shadowSettings)
			{
				++EditorGUI.indentLevel;
				DrawPropertyToggleKeyword("_ReceiveShadows", "_RECEIVE_SHADOWS");
				DrawPropertyShadowEnumKeyword("_Shadows");
				--EditorGUI.indentLevel;
				EditorGUILayout.Space();
			}
		}

		showPresets = EditorGUILayout.Foldout(showPresets, "Presets", true, EditorStyles.foldoutHeader);
		if (showPresets)
		{
			OpaquePreset();
			ClipPreset();
			FadePreset();
			TransparentPreset();
		}

		if (EditorGUI.EndChangeCheck())
		{
			SetShadowCasterPass();
			SetWarpPass();
			CopyLightMappingProperties();
		}
	}

	bool FoldoutWithToggle(ref bool foldout, string propertyName)
	{		
		Rect rect = EditorGUILayout.BeginHorizontal();
		foldout = EditorGUI.Foldout(rect, foldout, "", EditorStyles.foldoutHeader);
		DrawPropertyToggle(propertyName, true, true);
		bool prevEnable = GUI.enabled;
		GUI.enabled = prevEnable && FindProperty(propertyName, properties, false).floatValue != 0.0f;
		EditorGUILayout.EndHorizontal();

		return prevEnable;
	}

	bool FoldoutWithToggleKeyword(ref bool foldout, string propertyName, string keyword, bool bold = true)
	{
		Rect rect = EditorGUILayout.BeginHorizontal();
		foldout = EditorGUI.Foldout(rect, foldout, "", EditorStyles.foldoutHeader);
		DrawPropertyToggleKeyword(propertyName, keyword, true, bold);
		bool prevEnable = GUI.enabled;
		GUI.enabled = prevEnable && FindProperty(propertyName, properties, false).floatValue != 0.0f;
		EditorGUILayout.EndHorizontal();

		return prevEnable;
	}

	void DrawPropertyVector(string propertyName, VectorType vectorType)
	{
		MaterialProperty property = FindProperty(propertyName, properties, false);
		if (property == null) return;
		Vector4 value = property.vectorValue;

		EditorGUI.BeginChangeCheck();
		EditorGUI.showMixedValue = property.hasMixedValue;
		switch (vectorType)
		{
			case VectorType.Vector2:
				value = EditorGUILayout.Vector2Field(property.displayName, value);
				break;
			case VectorType.Vector2Int:
				Vector2Int valueInt2 = EditorGUILayout.Vector2IntField(property.displayName, new Vector2Int((int)value.x, (int)value.y));
				value = new Vector4(valueInt2.x, valueInt2.y);
				break;
			case VectorType.Vector3:
				value = EditorGUILayout.Vector3Field(property.displayName, value);
				break;
			case VectorType.Vector3Int:
				Vector3Int valueInt3 = EditorGUILayout.Vector3IntField(property.displayName, new Vector3Int((int)value.x, (int)value.y, (int)value.z));
				value = new Vector4(valueInt3.x, valueInt3.y, valueInt3.z);
				break;
			case VectorType.Vector4:
				value = EditorGUILayout.Vector4Field(property.displayName, value);
				break;
			default:
				break;
		}
		EditorGUI.showMixedValue = false;
		if (EditorGUI.EndChangeCheck())
			property.vectorValue = value;
	}

	void DrawPropertyEnum<T>(string propertyName) where T : System.Enum
	{
		MaterialProperty property = FindProperty(propertyName, properties, false);
		if (property == null) return;
		T value = (T)System.Enum.ToObject(typeof(T), (int)property.floatValue);

		EditorGUI.BeginChangeCheck();
		EditorGUI.showMixedValue = property.hasMixedValue;
		value = (T)EditorGUILayout.EnumPopup(property.displayName, value);
		EditorGUI.showMixedValue = false;
		if (EditorGUI.EndChangeCheck())
			property.floatValue = System.Convert.ToInt32(value);
	}

	void DrawPropertyShadowEnumKeyword(string propertyName)
	{
		MaterialProperty property = FindProperty(propertyName, properties, false);
		if (property == null) return;
		ShadowMode value = (ShadowMode)property.floatValue;

		EditorGUI.BeginChangeCheck();
		EditorGUI.showMixedValue = property.hasMixedValue;
		value = (ShadowMode)EditorGUILayout.EnumPopup(property.displayName, value);
		EditorGUI.showMixedValue = false;
		if (EditorGUI.EndChangeCheck())
			Shadows = value;
	}

	void DrawPropertyToggle(string propertyName, bool toggleLeft = false, bool bold = false)
	{
		MaterialProperty property = FindProperty(propertyName, properties, false);
		if (property == null) return;
		bool value = property.floatValue != 0.0f;

		EditorGUI.BeginChangeCheck();
		EditorGUI.showMixedValue = property.hasMixedValue;
		value = toggleLeft ? EditorGUILayout.ToggleLeft(property.displayName, value, bold ? EditorStyles.boldLabel : EditorStyles.label) : EditorGUILayout.Toggle(property.displayName, value, bold ? EditorStyles.boldLabel : EditorStyles.label);
		EditorGUI.showMixedValue = false;
		if (EditorGUI.EndChangeCheck())
			property.floatValue = value ? 1.0f : 0.0f;
	}

	void DrawPropertyToggleKeyword(string propertyName, string keyword, bool toggleLeft = false, bool bold = false)
	{
		MaterialProperty property = FindProperty(propertyName, properties, false);
		if (property == null) return;
		bool value = property.floatValue != 0.0f;

		EditorGUI.BeginChangeCheck();
		EditorGUI.showMixedValue = property.hasMixedValue;
		if (bold)
			value = toggleLeft ? EditorGUILayout.ToggleLeft(property.displayName, value, EditorStyles.boldLabel) : EditorGUILayout.Toggle(property.displayName, value, EditorStyles.boldLabel);
		else
			value = toggleLeft ? EditorGUILayout.ToggleLeft(property.displayName, value) : EditorGUILayout.Toggle(property.displayName, value);
		EditorGUI.showMixedValue = false;
		if (EditorGUI.EndChangeCheck())
			SetProperty(propertyName, keyword, value);
	}

	void CopyLightMappingProperties()
	{
		MaterialProperty mainTex = FindProperty("_MainTex", properties, false);
		MaterialProperty baseMap = FindProperty("_BaseMap", properties, false);
		if (mainTex != null && baseMap != null)
		{
			mainTex.textureValue = baseMap.textureValue;
			mainTex.textureScaleAndOffset = baseMap.textureScaleAndOffset;
		}
		MaterialProperty color = FindProperty("_Color", properties, false);
		MaterialProperty baseColor = FindProperty("_BaseColor", properties, false);
		if (color != null && baseColor != null)
		{
			color.colorValue = baseColor.colorValue;
		}
	}

	void BakedEmission()
	{
		EditorGUI.BeginChangeCheck();
		editor.LightmapEmissionProperty();
		if (EditorGUI.EndChangeCheck())
		{
			foreach (Material m in editor.targets)
			{
				m.globalIlluminationFlags &=
					~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
			}
		}
	}

	void OpaquePreset()
	{
		if (PresetButton("Opaque"))
		{
			Clipping = false;
			Shadows = ShadowMode.On;
			PremultiplyAlpha = false;
			SrcBlend = BlendMode.One;
			DstBlend = BlendMode.Zero;
			ZWrite = true;
			RenderQueue = RenderQueue.Geometry;
		}
	}

	void ClipPreset()
	{
		if (PresetButton("Clip"))
		{
			Clipping = true;
			Shadows = ShadowMode.Clip;
			PremultiplyAlpha = false;
			SrcBlend = BlendMode.One;
			DstBlend = BlendMode.Zero;
			ZWrite = true;
			RenderQueue = RenderQueue.AlphaTest;
		}
	}

	void FadePreset()
	{
		if (PresetButton("Fade"))
		{
			Clipping = false;
			Shadows = ShadowMode.Dither;
			PremultiplyAlpha = false;
			SrcBlend = BlendMode.SrcAlpha;
			DstBlend = BlendMode.OneMinusSrcAlpha;
			ZWrite = false;
			RenderQueue = RenderQueue.Transparent;
		}
	}

	void TransparentPreset()
	{
		if (HasPremultiplyAlpha && PresetButton("Transparent"))
		{
			Clipping = false;
			Shadows = ShadowMode.Dither;
			PremultiplyAlpha = true;
			SrcBlend = BlendMode.One;
			DstBlend = BlendMode.OneMinusSrcAlpha;
			ZWrite = false;
			RenderQueue = RenderQueue.Transparent;
		}
	}

	bool PresetButton(string name)
	{
		if (GUILayout.Button(name))
		{
			editor.RegisterPropertyChangeUndo(name);
			return true;
		}
		return false;
	}

	bool HasProperty(string name) =>
		FindProperty(name, properties, false) != null;

	void SetProperty (string name, string keyword, bool value)
	{
		if (SetProperty(name, value ? 1f : 0f))
		{
			SetKeyword(keyword, value);
		}
	}

	bool SetProperty(string name, float value)
	{
		MaterialProperty property = FindProperty(name, properties, false);
		if (property != null)
		{
			property.floatValue = value;
			return true;
		}
		return false;
	}

	void SetKeyword(string keyword, bool enabled)
	{
		if (enabled)
		{
			foreach (Material m in materials)
			{
				m.EnableKeyword(keyword);
			}
		}
		else
		{
			foreach (Material m in materials)
			{
				m.DisableKeyword(keyword);
			}
		}
	}

	void SetShadowCasterPass()
	{
		MaterialProperty shadows = FindProperty("_Shadows", properties, false);
		if (shadows == null || shadows.hasMixedValue)
		{
			return;
		}
		bool enabled = shadows.floatValue < (float)ShadowMode.Off;
		foreach (Material m in materials)
		{
			m.SetShaderPassEnabled("ShadowCaster", enabled);
		}
	}

	void SetWarpPass()
	{
		MaterialProperty warpPass = FindProperty("_Warp", properties, false);
		if (warpPass == null || warpPass.hasMixedValue)
		{
			return;
		}
		bool enabled = warpPass.floatValue != 0.0f;
		foreach (Material m in materials)
		{
			m.SetShaderPassEnabled("Warp", enabled);
		}
	}
}
