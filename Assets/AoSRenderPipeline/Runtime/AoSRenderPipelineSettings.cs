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
        public float blurRadius = 1.0f;
        public float downsampleAmount = 1.0f;

        [Header("Final shadow pass settings")]
        public float shadowThreshold = 1.0f;
        public float shadowThresholdSoftness = 1.0f;

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