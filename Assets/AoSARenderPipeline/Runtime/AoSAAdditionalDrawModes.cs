#if UNITY_EDITOR
using UnityEditor;

namespace AoSA.RenderPipeline
{
    public enum AdditionalCameraModes { Warp, LitColor, ShadowedColor, Shadow, SoftBlur, HeavyBlur, _Count };

    [InitializeOnLoad]
    public static class AdditionalDrawModes
    {
        public const string Section = "AoSA RP";

        static AdditionalDrawModes()
        {
            for (int i = 0; i < (int)AdditionalCameraModes._Count; ++i)
                SceneView.AddCameraMode(((AdditionalCameraModes)i).ToString(), Section);
        }
    }
}
#endif