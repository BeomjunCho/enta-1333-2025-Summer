// WallSelectedUI.cs
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel shown when a wall segment is selected.
/// Displays wall information and buttons for managing garrison/ranged units.
/// Also includes slots for destroy or additional actions.
/// </summary>
public class WallSelectedUI : MonoBehaviour
{
    // ======= UI References =======
    [Header("UI References")]
    [SerializeField] private TMP_Text _wallNameText = null;          // Wall name text
    [SerializeField] private TMP_Text _wallDescText = null;          // Wall description text
    [SerializeField] private Transform _buttonContainer = null;      // Where buttons are parented (layout group)
    [SerializeField] private Button _unitButtonPrefab = null;        // Button prefab for units
    [SerializeField] private Button _destroyButton = null;           // Button to destroy wall

    // ======= State =======
    // Reference to the currently bound wall object
    private BuildingWall _wall;
    // Tracks dynamically generated unit buttons for cleanup
    private readonly List<Button> _dynamicButtons = new();

    /* -------------------------------------------------------- */
    /*  Life-cycle                                             */
    /* -------------------------------------------------------- */

    /// <summary>
    /// Shows the panel (sets active).
    /// </summary>
    public void Show() => gameObject.SetActive(true);

    /// <summary>
    /// Hides the panel (sets inactive).
    /// </summary>
    public void Hide() => gameObject.SetActive(false);

    /* -------------------------------------------------------- */
    /*  Public API                                             */
    /* -------------------------------------------------------- */

    /// <summary>
    /// Populates the panel with wall info and unit buttons.
    /// Handles garrison and ungarrison logic and hooks up destroy button.
    /// </summary>
    /// <param name="wall">The wall being selected</param>
    /// <param name="data">Data describing the wall (name, description)</param>
    /// <param name="manager">Unit manager to query nearby ranged units</param>
    public void Bind(BuildingWall wall, BuildingDataSO data, UnitManager manager)
    {
        _wall = wall;

        // Set wall name and description
        _wallNameText.text = data.BuildingName;
        _wallDescText.text = data.Description;

        // Remove all existing children/buttons from the container
        foreach (Transform child in _buttonContainer)
            Destroy(child.gameObject);

        // Clear out previous dynamic buttons
        foreach (Button b in _dynamicButtons) Destroy(b.gameObject);
        _dynamicButtons.Clear();

        // If wall has a unit garrisoned, show "Release Unit" button
        if (_wall.HasGarrison)
        {
            // Create a button to ungarrison the unit
            Button btn = Instantiate(_unitButtonPrefab, _buttonContainer);
            TMP_Text txt = btn.GetComponentInChildren<TMP_Text>(true);

            txt.text = "Release Unit";
            txt.color = Color.blue;

            btn.onClick.AddListener(() =>
            {
                _wall.Ungarrison(); // Call ungarrison on wall
                Hide();             // Hide UI after action
            });

            _dynamicButtons.Add(btn);
        }
        else
        {
            // List all nearby ranged units as buttons to garrison
            List<UnitBase> ranged = wall.GetNearbyRangedUnits(manager);
            foreach (UnitBase u in ranged)
            {
                Button btn = Instantiate(_unitButtonPrefab, _buttonContainer);
                TMP_Text label = btn.GetComponentInChildren<TMP_Text>(true);
                label.text = u.UnitType.TypeName;

                btn.onClick.AddListener(() =>
                {
                    _wall.Garrison(u);
                    Hide();
                });
                _dynamicButtons.Add(btn);
            }
            // If no ranged units are nearby, show a disabled/info button
            if (ranged.Count == 0)
            {
                TMP_Text msg = Instantiate(_unitButtonPrefab, _buttonContainer)
                               .GetComponentInChildren<TMP_Text>(true);
                msg.text = "No ranged unit nearby";
                msg.color = Color.gray;
                _dynamicButtons.Add(msg.GetComponentInParent<Button>());
            }
        }

        // Setup destroy button (calls wall.DestroySelf and hides the panel)
        _destroyButton.onClick.RemoveAllListeners();
        _destroyButton.onClick.AddListener(() =>
        {
            _wall.DestroySelf();
            Hide();
        });

        // Finally, show the panel
        Show();
    }

    /// <summary>
    /// Clears the UI, destroys all dynamic buttons, and hides the panel.
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < _dynamicButtons.Count; i++)
        {
            Button b = _dynamicButtons[i];
            if (b != null)
                Destroy(b.gameObject);
        }
        _dynamicButtons.Clear();
        foreach (Transform child in _buttonContainer)
            Destroy(child.gameObject);
        Hide();
    }
}
