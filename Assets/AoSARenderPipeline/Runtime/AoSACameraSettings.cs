using UnityEngine;

/// <summary>
/// Per camera settings like overlay filter and saturation.
/// </summary>
public class CameraSettings : MonoBehaviour
{
    [SerializeField] private Color m_overlay = GetDefaultOverlay();
    [SerializeField] private float m_saturation = GetDefaultSaturation();

    public void GetSettings (out Color overlay, out float saturation)
    {
        overlay = m_overlay;
        saturation = m_saturation;
    }

    public static void GetDefaultSettings (out Color overlay, out float saturation)
    {
        overlay = GetDefaultOverlay();
        saturation = GetDefaultSaturation();
    }

    private static Color GetDefaultOverlay()
    {
        return new Color(0.5f, 0.5f, 0.5f, 0.0f);
    }

    private static float GetDefaultSaturation()
    {
        return 1.0f;
    }
}
