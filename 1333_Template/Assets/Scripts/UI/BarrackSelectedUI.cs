using System.Collections.Generic;
using System.Resources;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI panel that appears when a Barrack is selected.  
/// - Builds one spawn button per ArmyType in the barrack.  
/// - Each button uses <see cref="UnitSpawnButton"/> to handle cost display, interactivity, and spawn logic.
/// </summary>
public class BarrackSelectedUI : MonoBehaviour
{
    /* ------------------------------------------------------------------ */
    /*  Inspector References                                              */
    /* ------------------------------------------------------------------ */
    [Header("UI References")]
    [SerializeField] private Transform _buttonContainer = null;   // Parent with Layout Group
    [SerializeField] private Button _buttonPrefab = null;         // Prefab must include UnitSpawnButton

    [Header("Managers")]
    [SerializeField] private ResourceManager _resourceManager = null;
    [SerializeField] private ArmyManager _armyManager = null;

    /* ------------------------------------------------------------------ */
    /*  Runtime Data                                                      */
    /* ------------------------------------------------------------------ */
    private BuildingBarrack _barrack;
    private readonly List<Button> _spawnButtons = new();

    /* ------------------------------------------------------------------ */
    /*  Public API                                                        */
    /* ------------------------------------------------------------------ */
    /// <summary>Show this panel.</summary>
    public void Show() => gameObject.SetActive(true);

    /// <summary>Hide this panel.</summary>
    public void Hide() => gameObject.SetActive(false);

    /// <summary>
    /// Bind the selected barrack and rebuild the button list.
    /// </summary>
    /// <param name="barrack">Barrack instance that was selected.</param>
    public void Initialize(BuildingBarrack barrack, ResourceManager rm, ArmyManager am)
    {
        _barrack = barrack;

        // Guard clause: check required references
        if (_barrack == null || _buttonPrefab == null || _buttonContainer == null)
        {
            Debug.LogError("BarrackSelectedUI: Missing barrack reference or UI assignments");
            return;
        }
        _resourceManager = rm;
        _armyManager = am;

        EnsureManagers();
        RebuildButtons();
    }

    /// <summary>
    /// Clear the current barrack reference and destroy existing buttons.
    /// </summary>
    public void ClearBarrackInstance()
    {
        _barrack = null;

        foreach (Button btn in _spawnButtons)
            Destroy(btn.gameObject);

        _spawnButtons.Clear();
    }

    /// <summary>
    /// Destroy the currently selected building.
    /// </summary>
    public void DestroyBuilding()
    {
        if (_barrack != null)
            _barrack.DestroySelf();
    }

    /* ------------------------------------------------------------------ */
    /*  Unity Lifecycle                                                   */
    /* ------------------------------------------------------------------ */
    private void Awake() => Hide();

    /* ------------------------------------------------------------------ */
    /*  Internal Helpers                                                  */
    /* ------------------------------------------------------------------ */
    /// <summary>
    /// Rebuilds the spawn button list according to the barrack's spawnable ArmyTypes.
    /// Each button gets initialized to aggregate costs via ArmyManager and ResourceManager.
    /// </summary>
    private void RebuildButtons()
    {
        // Remove old buttons
        foreach (Button btn in _spawnButtons)
            Destroy(btn.gameObject);
        _spawnButtons.Clear();

        // Create new buttons
        foreach (ArmyType type in _barrack.SpawnableTypes)
        {
            Button btn = Instantiate(_buttonPrefab, _buttonContainer);

            // Try to get UnitSpawnButton component
            UnitSpawnButton spawnBtn = btn.GetComponent<UnitSpawnButton>();
            if (spawnBtn == null)
                spawnBtn = btn.gameObject.AddComponent<UnitSpawnButton>();

            // Initialize with aggregated costs through ArmyManager
            spawnBtn.Initialize(type, _armyManager, _resourceManager, _barrack);

            _spawnButtons.Add(btn);
        }
    }

    /// <summary>
    /// Ensures that ResourceManager and ArmyManager references are assigned.
    /// Attempts to find them in the scene if they are null.
    /// </summary>
    private void EnsureManagers()
    {
        if (_resourceManager == null)
            _resourceManager = FindAnyObjectByType<ResourceManager>();
        if (_armyManager == null)
            _armyManager = FindAnyObjectByType<ArmyManager>();

        if (_resourceManager == null || _armyManager == null)
            Debug.LogError("BarrackSelectedUI: ResourceManager or ArmyManager is missing in the scene.");
    }
}
