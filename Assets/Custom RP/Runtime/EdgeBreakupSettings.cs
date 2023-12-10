using Unity.Mathematics;
using UnityEngine;

[System.Serializable]
public class EdgeBreakupSettings
{
    public Texture2D warpTexture = null;
    public bool ignoreRenderScale = true;
    public bool debug = false;

    [Min(0f)] public float distance = 1.0f;
    [Min(0f)] public float distanceFadeMultiplierGlobal = 1.0f;
    public float warpTextureScale = 1.0f;
    [Min(math.EPSILON)] public float skew = 4.0f;
}
