using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI panel shown when a gate building is selected.
/// Provides open / close controls plus a destroy button.
/// </summary>
public class GateSelectedUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("TMP text that shows the gate name.")]
    [SerializeField] private TMP_Text _titleText = null;

    [Tooltip("TMP text that shows the gate description.")]
    [SerializeField] private TMP_Text _descriptionText = null;

    [Tooltip("Button that triggers gate opening.")]
    [SerializeField] private Button _openButton = null;

    [Tooltip("Button that triggers gate closing.")]
    [SerializeField] private Button _closeButton = null;

    [Tooltip("Button that destroys the gate.")]
    [SerializeField] private Button _destroyButton = null;

    // currently selected gate
    private BuildingGate _gate;

    /* -------------------------------------------------------- */
    /*  Life-cycle                                             */
    /* -------------------------------------------------------- */

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);

    /* -------------------------------------------------------- */
    /*  Public API                                             */
    /* -------------------------------------------------------- */

    /// <summary>
    /// Fills the panel with gate data and wires up the buttons.
    /// </summary>
    public void Bind(BuildingGate gate, BuildingDataSO data)
    {
        _gate = gate;

        if (_gate == null || data == null)
        {
            Debug.LogError("GateSelectedUI: Missing gate or data reference.");
            return;
        }

        /* ---------- static texts ---------- */
        _titleText.text = data.BuildingName;
        _descriptionText.text = data.Description;

        /* ---------- button listeners ---------- */
        _openButton.onClick.RemoveAllListeners();
        _closeButton.onClick.RemoveAllListeners();
        _destroyButton.onClick.RemoveAllListeners();

        _openButton.onClick.AddListener(_gate.OpenGate);
        _closeButton.onClick.AddListener(_gate.CloseGate);
        _destroyButton.onClick.AddListener(() =>
        {
            _gate.DestroySelf();
            Hide();
        });

        Show();
    }

    /// <summary>Clears the reference and hides the panel.</summary>
    public void Clear()
    {
        _gate = null;

        _openButton.onClick.RemoveAllListeners();
        _closeButton.onClick.RemoveAllListeners();
        _destroyButton.onClick.RemoveAllListeners();

        Hide();
    }
}
