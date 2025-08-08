using UnityEngine;

/// <summary>
/// Common audio math helpers for UI sliders, meters, etc.
/// </summary>
public static class AudioUtils
{
    /* ------------------------------------------------------------------ */
    /*  Decibel helpers                                                   */
    /* ------------------------------------------------------------------ */

    /// <summary>Convert linear amplitude [0¡¥1] to decibels.</summary>
    public static float LinearToDb(float linear, float minDb = -80f)
    {
        if (linear <= 0f) return minDb;
        return 20f * Mathf.Log10(linear);
    }

    /// <summary>Convert decibels to linear amplitude [0¡¥1].</summary>
    public static float DbToLinear(float db)
    {
        return db <= -80f ? 0f : Mathf.Pow(10f, db / 20f);
    }

    /// <summary>Slider(0¡¥1) ¡æ Linear with dB curve (equal-step decibel).</summary>
    public static float SliderToLinearDecibel(float slider, float minDb = -60f)
    {
        float db = Mathf.Lerp(minDb, 0f, slider);
        return DbToLinear(db);
    }

    /// <summary>Linear ¡æ Slider(0¡¥1) inverse of <see cref="SliderToLinearDecibel"/>.</summary>
    public static float LinearToSliderDecibel(float linear, float minDb = -60f)
    {
        float db = LinearToDb(linear, minDb);
        return Mathf.InverseLerp(minDb, 0f, db);
    }

    /* ------------------------------------------------------------------ */
    /*  Gamma helpers                                                     */
    /* ------------------------------------------------------------------ */

    /// <summary>Slider ¡æ Linear using gamma curve (slider^gamma).</summary>
    public static float SliderToLinearGamma(float slider, float gamma = 2f)
    {
        return Mathf.Pow(slider, gamma);
    }

    /// <summary>Linear ¡æ Slider inverse of <see cref="SliderToLinearGamma"/>.</summary>
    public static float LinearToSliderGamma(float linear, float gamma = 2f)
    {
        if (gamma <= 0f) gamma = 1f;
        return Mathf.Pow(linear, 1f / gamma);
    }
}
