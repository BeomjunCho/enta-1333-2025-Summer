using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI button that spends the total cost of an ArmyCompositionSO (via ArmyManager)
/// and requests spawning of that army. It auto-disables when resources are insufficient.
/// </summary>
[RequireComponent(typeof(Button))]
public class UnitSpawnButton : MonoBehaviour
{
    /* ------------------------------------------------------------------ */
    /*  Serialized UI References                                          */
    /* ------------------------------------------------------------------ */
    [Header("UI")]
    [SerializeField] private TMP_Text _nameText = null;
    [SerializeField] private TMP_Text _costText = null;
    [SerializeField] private TMP_Text _countText = null;   

    /* ------------------------------------------------------------------ */
    /*  Runtime References                                                */
    /* ------------------------------------------------------------------ */
    private Button _button;
    private ResourceManager _resourceManager;
    private ArmyManager _armyManager;
    private BuildingBarrack _barrack;

    /* ------------------------------------------------------------------ */
    /*  Data                                                              */
    /* ------------------------------------------------------------------ */
    private ArmyType _armyType;
    private List<ResourceCost> _costs = new List<ResourceCost>();
    private int _totalUnits;

    /// <summary>ArmyType this button is responsible for spawning.</summary>
    public ArmyType armyType => _armyType;
    /// <summary>Total number of units that will be spawned with this ArmyType.</summary>
    public int totalUnits => _totalUnits;

    /* ------------------------------------------------------------------ */
    /*  Unity Lifecycle                                                   */
    /* ------------------------------------------------------------------ */
    /// <summary>
    /// Cache Button and register click listener.
    /// </summary>
    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(OnClick);
    }

    /// <summary>
    /// Subscribe to resource change events when enabled.
    /// </summary>
    private void OnEnable()
    {
        if (_resourceManager != null)
            _resourceManager.OnResourceChanged += HandleResourceChanged;

        UpdateInteractableState();
    }

    /// <summary>
    /// Unsubscribe from events on disable.
    /// </summary>
    private void OnDisable()
    {
        if (_resourceManager != null)
            _resourceManager.OnResourceChanged -= HandleResourceChanged;
    }

    /// <summary>
    /// Extra safety: unsubscribe when destroyed.
    /// </summary>
    private void OnDestroy()
    {
        if (_resourceManager != null)
            _resourceManager.OnResourceChanged -= HandleResourceChanged;
    }

    /* ------------------------------------------------------------------ */
    /*  Initialization                                                    */
    /* ------------------------------------------------------------------ */
    /// <summary>
    /// Initializes this button with ArmyType and required managers.
    /// Costs are aggregated from ArmyCompositionSO via ArmyManager.
    /// Also caches total unit count for display.
    /// </summary>
    /// <param name="type">ArmyType to spawn.</param>
    /// <param name="armyManager">ArmyManager used to retrieve composition & spawn.</param>
    /// <param name="rm">ResourceManager for cost check/spend.</param>
    /// <param name="barrack">Barrack (or spawner wrapper) that actually triggers spawn.</param>
    public void Initialize(ArmyType type, ArmyManager armyManager, ResourceManager rm, BuildingBarrack barrack)
    {
        _armyType = type;
        _armyManager = armyManager;
        _resourceManager = rm;
        _barrack = barrack;

        // 1) Aggregate costs once
        _costs = _armyManager != null
            ? _armyManager.GetArmyTotalCosts(_armyType)
            : new List<ResourceCost>();

        // 2) Get total unit count for display
        _totalUnits = _armyManager != null
            ? _armyManager.GetCompositionCount(_armyType)
            : 0;

        // UI setup
        if (_nameText != null)
            _nameText.text = _armyType.ToString();

        if (_costText != null)
            _costText.text = BuildCostString(_costs);

        if (_countText != null)
            _countText.text = $"x{_totalUnits}";

        UpdateInteractableState();
    }

    /* ------------------------------------------------------------------ */
    /*  Button Logic                                                      */
    /* ------------------------------------------------------------------ */
    /// <summary>
    /// Called when the player clicks this button.
    /// Tries to spend the aggregated costs and then requests spawn.
    /// </summary>
    private void OnClick()
    {
        if (_resourceManager == null || _armyManager == null)
        {
            Debug.LogWarning("UnitSpawnButton: Missing managers.");
            return;
        }

        // 1) Atomic check + spend
        if (!_resourceManager.SpendCosts(_costs))
        {
            // Optional: play feedback (shake, SFX, etc.)
            Debug.Log("Not enough resources!");
            return;
        }

        // 2) Spawn via barrack (preferred) or fallback to ArmyManager
        if (_barrack != null)
        {
            _barrack.SpawnUnit(_armyType);
        }
        else
        {
            Debug.LogWarning("UnitSpawnButton: No Barrack assigned; please hook up spawn call.");
        }

        // 3) Refresh interactable immediately (event will also fire)
        UpdateInteractableState();
    }

    /// <summary>
    /// Updates button interactable based on current resource amounts.
    /// </summary>
    private void UpdateInteractableState()
    {
        if (_button == null || _resourceManager == null)
            return;

        _button.interactable = _resourceManager.CanAffordCosts(_costs);
    }

    /// <summary>
    /// Resource change event handler.
    /// </summary>
    /// <param name="type">Changed resource enum.</param>
    /// <param name="newValue">New amount.</param>
    private void HandleResourceChanged(ResourceList type, int newValue)
    {
        UpdateInteractableState();
    }

    /* ------------------------------------------------------------------ */
    /*  Helpers                                                           */
    /* ------------------------------------------------------------------ */
    /// <summary>
    /// Builds readable cost text like "Bread 10 / Wood 5".
    /// </summary>
    /// <param name="costs">Cost list.</param>
    /// <returns>Formatted string.</returns>
    private string BuildCostString(List<ResourceCost> costs)
    {
        if (costs == null || costs.Count == 0)
            return "Free";

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < costs.Count; i++)
        {
            var c = costs[i];
            sb.Append($"{c.ResourceType} {c.Amount}");
            if (i < costs.Count - 1)
                sb.Append(" / ");
        }
        return sb.ToString();
    }
}
