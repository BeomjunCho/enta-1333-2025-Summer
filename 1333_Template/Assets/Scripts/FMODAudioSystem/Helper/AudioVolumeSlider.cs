using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI Slider that controls an AudioManager channel with selectable loudness curve.
/// Supports Decibel (log) or Gamma (slider^γ) mapping for even perceived loudness.
/// </summary>
[RequireComponent(typeof(Slider))]
public class AudioVolumeSlider : MonoBehaviour
{
    /* ------------------------------------------------------------------ */
    /*  Types                                                             */
    /* ------------------------------------------------------------------ */

    /// <summary>Curve type for slider-to-volume mapping.</summary>
    public enum VolumeCurve
    {
        Decibel,
        Gamma
    }

    /* ------------------------------------------------------------------ */
    /*  Inspector                                                         */
    /* ------------------------------------------------------------------ */

    [Header("Channel")]
    [SerializeField] private AudioChannel _channel = AudioChannel.Master;

    [Header("Curve")]
    [SerializeField] private VolumeCurve _curveType = VolumeCurve.Decibel;
    [SerializeField, Tooltip("Minimum dB when slider = 0 (Decibel curve).")]
    private float _minDb = -60f;
    [SerializeField, Tooltip("Gamma exponent (Gamma curve).")]
    private float _gamma = 2f;

    [Header("Sync")]
    [SerializeField] private bool _syncOnEnable = true;

    [Header("Optional UI")]
    [SerializeField] private TMP_Text _labelText = null;
    [SerializeField] private TMP_Text _valueText = null;
    [SerializeField] private bool _autoLabel = true;

    /* ------------------------------------------------------------------ */
    /*  Runtime                                                           */
    /* ------------------------------------------------------------------ */

    private Slider _slider;

    /* ============================ Unity ============================== */

    private void Awake()
    {
        _slider = GetComponent<Slider>();
        _slider.minValue = 0f;
        _slider.maxValue = 1f;
        _slider.wholeNumbers = false;
        _slider.onValueChanged.AddListener(OnSliderChanged);

        if (_autoLabel && _labelText != null)
            _labelText.text = _channel.ToString();
    }

    private void Start() => SyncFromManager();
    private void OnEnable() { if (_syncOnEnable) SyncFromManager(); }
    private void OnDestroy() => _slider.onValueChanged.RemoveListener(OnSliderChanged);

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_autoLabel && _labelText != null)
            _labelText.text = _channel.ToString();
    }
#endif

    /* ============================ Event ============================== */

    private void OnSliderChanged(float sliderVal)
    {
        float linear = SliderToLinear(sliderVal);
        AudioManager.Instance?.SetVolume(_channel, linear);

        if (_valueText != null)
            _valueText.text = Mathf.RoundToInt(sliderVal * 100f) + " %";
    }

    /* ------------------------------------------------------------------ */
    /*  Helpers                                                           */
    /* ------------------------------------------------------------------ */

    private void SyncFromManager()
    {
        float linear = GetVolumeLinear();
        float sliderVal = LinearToSlider(linear);
        _slider.SetValueWithoutNotify(sliderVal);

        if (_valueText != null)
            _valueText.text = Mathf.RoundToInt(sliderVal * 100f) + " %";
    }

    private float GetVolumeLinear()
    {
        if (AudioManager.Instance == null) return 1f;

        AudioManager am = AudioManager.Instance;
        return _channel switch
        {
            AudioChannel.Master => am.masterVolume,
            AudioChannel.Music => am.musicVolume,
            AudioChannel.Ambience => am.ambienceVolume,
            AudioChannel.SFX => am.sfxVolume,
            AudioChannel.Foley => am.foleyVolume,
            AudioChannel.Dialogue => am.dialougeVolume,   // spelling kept
            _ => 1f
        };
    }

    private float SliderToLinear(float sliderVal)
    {
        return _curveType == VolumeCurve.Decibel
            ? AudioUtils.SliderToLinearDecibel(sliderVal, _minDb)
            : AudioUtils.SliderToLinearGamma(sliderVal, _gamma);
    }

    private float LinearToSlider(float linearVal)
    {
        return _curveType == VolumeCurve.Decibel
            ? AudioUtils.LinearToSliderDecibel(linearVal, _minDb)
            : AudioUtils.LinearToSliderGamma(linearVal, _gamma);
    }
}
