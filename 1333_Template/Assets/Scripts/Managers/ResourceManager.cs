using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages all game resources defined in ResourceTypeSO collections.
/// Tracks counts for each ResourceDataSO and provides methods to add, spend,
/// and query resources using either ResourceDataSO or ResourceList enum.
/// Displays a debug list of resources in the Inspector when in the Editor.
/// </summary>
public class ResourceManager : MonoBehaviour
{
    [Header("All Resource Types")]
    [Tooltip("List of ResourceTypeSO assets to initialize resource entries.")]
    [SerializeField] private List<ResourceTypeSO> _resourceTypeSOs = new();

    [Header("Resource Panel UI")]
    [Tooltip("Panel which shows all resources icon and number")]
    [SerializeField] private ResourcePanelUI _resourcePanelUI;

    // Internal dictionary mapping each ResourceDataSO to its current count.
    private Dictionary<ResourceDataSO, int> _resources;
    // Lookup map from enum value to ResourceDataSO asset for enum-based methods.
    private Dictionary<ResourceList, ResourceDataSO> _enumLookup;

    public event System.Action<ResourceList, int> OnResourceChanged;  

#if UNITY_EDITOR
    [Header("Debug: Resource Dictionary")]
    [Tooltip("Read-only list of resources and their counts for debugging.")]
    [SerializeField] private List<string> _debugResourceList = new List<string>();

#endif
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha0))
            AddDebugResources();
    }

    /// <summary>
    /// Debug: add 999 of each resource when pressing 0 key.
    /// </summary>
    private void AddDebugResources()
    {
        // Copy keys to avoid modifying collection during iteration
        var dataList = new List<ResourceDataSO>(_resources.Keys);

        foreach (var data in dataList)
            AddResource(data, 999);

        Debug.Log("ResourceManager: Debug added 999 to all resources");
    }

    /// <summary>
    /// Initializes the manager by creating entries for every ResourceDataSO
    /// in each listed ResourceTypeSO and builds enum lookup.
    /// Must be called once at game start.
    /// </summary>
    public void Initialize()
    {
        _resources = new Dictionary<ResourceDataSO, int>();
        _enumLookup = new Dictionary<ResourceList, ResourceDataSO>();

        foreach (var typeSO in _resourceTypeSOs)
        {
            foreach (var data in typeSO.Resources)
            {
                if (data == null)
                    continue;

                // Initialize resource count
                if (!_resources.ContainsKey(data))
                    _resources[data] = 0;

                // Populate enum lookup
                var key = data.ResourceType;
                if (!_enumLookup.ContainsKey(key))
                    _enumLookup[key] = data;
            }
        }

        _resourcePanelUI.RefreshAll();

#if UNITY_EDITOR
        UpdateDebugList();
#endif
    }

    // helper
    private void FireChanged(ResourceList type, int newValue)
    {
        OnResourceChanged?.Invoke(type, newValue);
    }

    /// <summary>
    /// Returns true if all costs can be affordable.
    /// </summary>
    public bool CanAffordCosts(List<ResourceCost> costs)
    {
        foreach (var cost in costs)
        {
            if (TryGetResourceCount(cost.ResourceType) < cost.Amount)
                return false;
        }
        return true;
    }

    /// <summary>
    /// If the cost is sufficient, deduct all of it and return true; if insufficient, return false. 
    /// </summary>
    public bool SpendCosts(List<ResourceCost> costs)
    {
        if (!CanAffordCosts(costs))
            return false;

        foreach (var cost in costs)
            TrySpendResource(cost.ResourceType, cost.Amount);

        return true;
    }

    /// <summary>
    /// Adds the specified amount of the given ResourceDataSO.
    /// </summary>
    /// <param name="data">The ResourceDataSO representing the type to add.</param>
    /// <param name="amount">The amount of units to add.</param>
    public void AddResource(ResourceDataSO data, int amount)
    {
        if (data == null)
            return;

        if (!_resources.ContainsKey(data))
            _resources[data] = 0;

        _resources[data] += amount;
        FireChanged(data.ResourceType, _resources[data]);
        _resourcePanelUI.RefreshAll();
        //Debug.Log($"ResourceManager: Added {amount}x {data.DisplayName}. New total: {_resources[data]}");

#if UNITY_EDITOR
        UpdateDebugList();
#endif
    }

    /// <summary>
    /// Adds the specified amount of the resource identified by enum.
    /// </summary>
    /// <param name="type">The ResourceList enum identifying the resource.</param>
    /// <param name="amount">The amount of units to add.</param>
    public void TryAddResource(ResourceList type, int amount)
    {
        if (_enumLookup.TryGetValue(type, out var data))
            AddResource(data, amount);
    }

    /// <summary>
    /// Attempts to spend the specified amount of the given ResourceDataSO.
    /// Returns true if successful; false if insufficient.
    /// </summary>
    /// <param name="data">The ResourceDataSO representing the type to spend.</param>
    /// <param name="amount">The amount of units to spend.</param>
    /// <returns>True if the spend was successful; otherwise false.</returns>
    public bool SpendResource(ResourceDataSO data, int amount)
    {
        if (data == null || !_resources.ContainsKey(data) || _resources[data] < amount)
            return false;

        _resources[data] -= amount;
        FireChanged(data.ResourceType, _resources[data]);
        _resourcePanelUI.RefreshAll();
        Debug.Log($"ResourceManager: Spent {amount}x {data.DisplayName}. Remaining: {_resources[data]}");

#if UNITY_EDITOR
        UpdateDebugList();
#endif
        return true;
    }

    /// <summary>
    /// Attempts to spend the specified amount of the resource identified by enum.
    /// Returns true if successful; false if insufficient.
    /// </summary>
    /// <param name="type">The ResourceList enum identifying the resource.</param>
    /// <param name="amount">The amount of units to spend.</param>
    /// <returns>True if the spend was successful; otherwise false.</returns>
    public bool TrySpendResource(ResourceList type, int amount)
    {
        if (_enumLookup.TryGetValue(type, out var data))
            return SpendResource(data, amount);
        return false;
    }

    /// <summary>
    /// Gets the current count for the specified ResourceDataSO.
    /// </summary>
    /// <param name="data">The ResourceDataSO to query.</param>
    /// <returns>Current count of the resource, or zero if not tracked.</returns>
    public int GetResourceCount(ResourceDataSO data)
    {
        if (data == null)
            return 0;

        return _resources.TryGetValue(data, out var count) ? count : 0;
    }

    /// <summary>
    /// Gets the current count for the resource identified by enum.
    /// </summary>
    /// <param name="type">The ResourceList enum identifying the resource.</param>
    /// <returns>Current count, or zero if not tracked.</returns>
    public int TryGetResourceCount(ResourceList type)
    {
        if (_enumLookup.TryGetValue(type, out var data))
            return GetResourceCount(data);
        return 0;
    }

    /// <summary>
    /// Clears all resource counts, lookup maps, UI and event subscriptions
    /// so that Initialize() can be called again for a fresh start.
    /// </summary>
    public void ResetResources()
    {
        // 1) Clear existing resource data
        _resources.Clear();
        _enumLookup.Clear();

        // 2) Remove all listeners to avoid duplicate callbacks
        OnResourceChanged = null;

        // 3) Update UI to reflect cleared state
        _resourcePanelUI.RefreshAll();

#if UNITY_EDITOR
        // 4) Clear debug list in the Inspector
        _debugResourceList.Clear();
#endif
    }

    /// <summary>
    /// Provides a read-only view of all resource counts keyed by ResourceDataSO.
    /// </summary>
    public IReadOnlyDictionary<ResourceDataSO, int> GetAllResources() => _resources;

#if UNITY_EDITOR
    /// <summary>
    /// Updates the debug list shown in the Inspector.
    /// </summary>
    private void UpdateDebugList()
    {
        _debugResourceList.Clear();
        foreach (var kv in _resources)
        {
            var name = kv.Key != null ? kv.Key.DisplayName : "Unknown";
            _debugResourceList.Add($"{name}: {kv.Value}");
        }
    }

    private void OnValidate()
    {
        if (_resources != null)
            UpdateDebugList();
    }
#endif
}
