using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AoS.RenderPipeline
{
    [System.Serializable]
    public class AoSRenderPipelineSettings
    {
        public bool useSRPBatcher = true;
        public ShadowSettings shadows;
        public Shader shader;

        [Header("Warp pass settings")]
        public Texture2D warpTexture;
        public float warpGlobalScale = 1.0f;
        public float warpGlobalDistanceFade = 1.0f;
        public float warpWidth = 1.0f;

        [Header("Blur pass settings")]
        [Range(1.0f, 16.0f)] public float softBlurDownsample = 4;
        [Range(2.0f, 16.0f)] public float heavyBlurDownsample = 8;

        [Header("Final shadow pass settings")]
        [Min(0)] public int shadowStepCount = 3;
        [Range(0.0f, 1.0f)] public float shadowThreshold = 0.5f;
        [Range(0.0f, 1.0f)] public float shadowThresholdSoftness = 0.3f;
        [Range(0.0f, 1.0f)] public float shadowInnerGlow = 0.3f;

        [Header("Final color pass settings")]
        public bool warpBloom = false;

        [NonSerialized] private Material m_material;
        public Material Material
        {
            get
            {
                if (m_material == null && shader != null)
                    m_material = new(shader) { hideFlags = HideFlags.HideAndDontSave };
                return m_material;
            }
        }
    }
}