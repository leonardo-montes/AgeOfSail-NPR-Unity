using UnityEngine;

[System.Serializable]
public class EdgeBreakupSettings
{
    public Texture2D warpTexture;
    public bool ignoreRenderScale;
    public bool debug;

    [Min(0f)] public float distance;
    public float warpTextureScale;
    public float skew;
}
