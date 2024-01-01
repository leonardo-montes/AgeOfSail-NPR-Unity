using UnityEditor;

namespace AoS.RenderPipeline
{
    public enum AdditionalCameraModes { Warp, FinalShadow, SoftBlur, HeavyBlur, _Count };

    [InitializeOnLoad]
    public static class AdditionalDrawModes
    {
        public const string Section = "AoS RP";

        static AdditionalDrawModes()
        {
            for (int i = 0; i < (int)AdditionalCameraModes._Count; ++i)
                SceneView.AddCameraMode(((AdditionalCameraModes)i).ToString(), Section);
        }
    }
}