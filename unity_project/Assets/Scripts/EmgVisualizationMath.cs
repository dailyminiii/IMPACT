using UnityEngine;

/// <summary>
/// Shared EMG aggregation convention for the IMPACT visualizations.
///
/// Each device supplies eight channels. Values are first RMS/MVC normalized
/// per channel. That physiological ratio is retained even when it exceeds 1.0
/// (i.e., 100% MVC). Capping to [0, 1.5] is a separate, display-only operation
/// used to keep the headset charts and bars on a stable scale.
/// </summary>
public static class EmgVisualizationMath
{
    public const float DisplayMaximum = 1.5f;

    // Local channel indices for each eight-channel device.
    public static readonly int[] FlexionChannels = { 0, 1, 6, 7 };
    public static readonly int[] ExtensionChannels = { 2, 3, 4, 5 };

    /// <summary>Returns RMS/MVC without imposing a display range.</summary>
    public static float NormalizeRmsByMvc(float rms, float mvc)
    {
        if (float.IsNaN(rms) || float.IsInfinity(rms) ||
            float.IsNaN(mvc) || float.IsInfinity(mvc) || mvc <= 0.000001f)
        {
            return 0f;
        }

        float normalized = rms / mvc;
        return float.IsNaN(normalized) || float.IsInfinity(normalized) ? 0f : normalized;
    }

    /// <summary>
    /// Combines the specified MVC-normalized channels as a group RMS.
    /// This does not cap a value above 1.0.
    /// </summary>
    public static float ComputeGroupRms(float[] mvcNormalizedChannels, int[] channelIndices)
    {
        if (mvcNormalizedChannels == null || channelIndices == null || channelIndices.Length == 0)
        {
            return 0f;
        }

        float sumOfSquares = 0f;
        int validCount = 0;
        foreach (int channel in channelIndices)
        {
            if (channel < 0 || channel >= mvcNormalizedChannels.Length)
            {
                continue;
            }

            float value = mvcNormalizedChannels[channel];
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                continue;
            }

            value = Mathf.Max(0f, value);
            sumOfSquares += value * value;
            validCount++;
        }

        return validCount == 0 ? 0f : Mathf.Sqrt(sumOfSquares / validCount);
    }

    /// <summary>Applies the [0, 1.5] cap only for headset display.</summary>
    public static float CapForDisplay(float mvcNormalizedValue)
    {
        return float.IsNaN(mvcNormalizedValue) || float.IsInfinity(mvcNormalizedValue)
            ? 0f
            : Mathf.Clamp(mvcNormalizedValue, 0f, DisplayMaximum);
    }
}
