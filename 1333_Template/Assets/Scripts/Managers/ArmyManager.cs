using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IdleAI = IdleReturnToCenter;

/// <summary>
/// Identifiers for each army composition asset.
/// </summary>
public enum ArmyType
{
    PlayerArmy,
    EnemyArmy,
    Spearman,
    Archer,
    CrossbowMan,
    RoyalKnight,
    Mage,
    HighMage,
    ElderHighMage,
    Commander,
    Worker,
    Wave1,
    Wave2,
    Wave3,
    Wave4,
    Wave5
}

/// <summary>
/// Associates an ArmyType enum value with its ArmyCompositionSO asset.
/// </summary>
[System.Serializable]
public struct ArmyMapping
{
    public ArmyType type;
    public ArmyCompositionSO composition;
}

/// <summary>
/// Responsible for spawning units based on ArmyCompositionSO assets.
/// Supports interval-based spawning by ArmyType, and will distribute units
/// across nearby grid nodes to avoid overlap.
/// </summary>
public class ArmyManager : MonoBehaviour
{
    [Header("Army Mappings")]
    [Tooltip("Map each ArmyType to its ArmyCompositionSO asset.")]
    [SerializeField] private List<ArmyMapping> _armyMappings = new List<ArmyMapping>();

    [Header("Hot-key Spawning (Debug)")]
    [SerializeField] private bool _enableHotkeySpawn = true;    
    [SerializeField] private Team _enemyTeam = Team.Enemy; 

    // Runtime lookup from ArmyType to its composition asset
    private Dictionary<ArmyType, ArmyCompositionSO> _compositionLookup;

    // Injected dependencies
    private GridManager _gridManager;
    private UnitManager _unitManager;
    private AStarPathfinder _pathfinder;

    public event System.Action<UnitBase> UnitSpawned; // Unit spawn event

    private void Awake()
    {
        _compositionLookup = new Dictionary<ArmyType, ArmyCompositionSO>();
        foreach (var mapping in _armyMappings)
        {
            if (!_compositionLookup.ContainsKey(mapping.type))
                _compositionLookup.Add(mapping.type, mapping.composition);
            else
                Debug.LogWarning($"ArmyManager: Duplicate mapping for {mapping.type}");
        }
    }

    /// <summary>
    /// Debug helper: press Alpha1 to spawn one EnemyArmy wave at a random node.
    /// </summary>
    private void Update()
    {
        if (!_enableHotkeySpawn || _gridManager == null) return;

        if (Input.GetKeyDown(KeyCode.Alpha6))
            SpawnEnemyArmyAtRandomNode();
    }

    /// <summary>
    /// Initializes the ArmyManager with required managers.
    /// Must be called before any spawn methods.
    /// </summary>
    public void Initialize(GridManager gridManager, UnitManager unitManager)
    {
        _gridManager = gridManager;
        _unitManager = unitManager;
        _pathfinder = new AStarPathfinder(_gridManager);
    }

    /// <summary>
    /// Spawns all units of the given type, registers & initializes them,
    /// and collects them into the provided list, yielding between each spawn.
    /// </summary>
    /// <param name="type">Which ArmyType to spawn.</param>
    /// <param name="team">Which Team they belong to.</param>
    /// <param name="spawnPosition">World‐space origin of the spawn.</param>
    /// <param name="delay">Seconds between each unit instantiation.</param>
    /// <param name="outUnits">List to fill with the spawned UnitBase instances.</param>
    public IEnumerator SpawnArmyAndCollect(
        ArmyType type,
        Team team,
        Vector3 spawnPosition,
        float delay,
        List<UnitBase> outUnits)
    {
        // 1) Look up composition
        if (!_compositionLookup.TryGetValue(type, out var composition) || composition == null)
        {
            Debug.LogError($"ArmyManager: No composition registered for {type}");
            yield break;
        }

        // 2) Determine total count and candidate nodes
        int totalCount = GetCompositionCount(type);
        GridNode centerNode = _gridManager.GetNodeFromWorldPosition(spawnPosition);
        List<GridNode> spawnNodes = _gridManager.FindNearestFreeNodes(centerNode, totalCount);

        Debug.Log($"[ArmyManager] Spawning {totalCount} x {type} at {spawnPosition}");

        // 3) Loop through each entry and spawn
        int index = 0;
        foreach (var entry in composition.unitEntries)
        {
            for (int i = 0; i < entry.count; i++)
            {
                // pick a free node or fallback to spawnPosition
                Vector3 pos = (index < spawnNodes.Count)
                    ? spawnNodes[index].worldPosition
                    : spawnPosition;

                GameObject unitGO = Instantiate(entry.unitTypePrefab.unitPrefab, pos, Quaternion.identity);
                UnitBase unit = unitGO.GetComponent<UnitBase>();
                if (unit != null)
                {
                    _unitManager.RegisterUnit(unit);
                    unit.Initialize(
                        entry.unitTypePrefab.unitType,
                        _gridManager,
                        _unitManager,
                        _pathfinder,
                        team);

                    if (team == Team.Enemy && unit.GetComponent<IdleAI>() == null)
                        unit.gameObject.AddComponent<IdleAI>();

                    outUnits.Add(unit);
                }
                else
                {
                    Debug.LogWarning($"ArmyManager: '{unitGO.name}' missing UnitBase component.");
                    Destroy(unitGO);
                }

                index++;
                // yield between spawns
                yield return new WaitForSeconds(delay);
            }
        }

        Debug.Log($"[ArmyManager] Finished SpawnArmyAndCollect for {type}");
    }

    /// <summary>
    /// Coroutine that actually instantiates each unit, distributes them across free grid nodes,
    /// and registers/initializes them with the pathfinder.
    /// </summary>
    private IEnumerator SpawnArmyCoroutine(
        ArmyType type,
        ArmyCompositionSO composition,
        Team team,
        Vector3 spawnPosition,
        float delay)
    {
        // Determine how many units in total we need to spawn
        int totalCount = GetCompositionCount(type);

        // Find the grid node at the barrack’s spawn point
        GridNode centerNode = _gridManager.GetNodeFromWorldPosition(spawnPosition);

        // Get up to totalCount nearest free nodes (walkable & not reserved)
        List<GridNode> spawnNodes = _gridManager.FindNearestFreeNodes(centerNode, totalCount);

        Debug.Log($"[ArmyManager] Distributing {totalCount} units around {centerNode.name}");

        int index = 0;
        // Loop through each entry in the composition
        foreach (var entry in composition.unitEntries)
        {
            for (int i = 0; i < entry.count; i++)
            {
                // Choose either the next free node or fallback to the center
                Vector3 pos = (index < spawnNodes.Count)
                    ? spawnNodes[index].worldPosition
                    : spawnPosition;

                // Instantiate the unit prefab at the chosen position
                GameObject unitGO = Instantiate(
                    entry.unitTypePrefab.unitPrefab,
                    pos,
                    Quaternion.identity);

                // Register and initialize the new unit
                UnitBase unitComp = unitGO.GetComponent<UnitBase>();
                UnitSpawned?.Invoke(unitComp);              // Invoke unit spawned event
                if (unitComp != null)
                {
                    _unitManager.RegisterUnit(unitComp);
                    unitComp.Initialize(
                        entry.unitTypePrefab.unitType,
                        _gridManager,
                        _unitManager,
                        _pathfinder,
                        team);

                    if (team == Team.Enemy && unitComp.GetComponent<IdleAI>() == null)
                        unitComp.gameObject.AddComponent<IdleAI>();
                }
                else
                {
                    Debug.LogWarning($"ArmyManager: '{unitGO.name}' missing UnitBase component.");
                    Destroy(unitGO);
                }

                index++;
                yield return new WaitForSeconds(delay);
            }
        }

        Debug.Log($"[ArmyManager] Finished spawning wave of {type}");
    }

    /// <summary>
    /// Returns the total number of units defined in the given ArmyType composition.
    /// </summary>
    public int GetCompositionCount(ArmyType type)
    {
        if (!_compositionLookup.TryGetValue(type, out var composition) || composition == null)
            return 0;

        int total = 0;
        foreach (var entry in composition.unitEntries)
            total += entry.count;
        return total;
    }

    /// <summary>
    /// Spawns units of the given ArmyType one by one, waiting 'delay' seconds between spawns,
    /// and positions each unit at the nearest free grid node around spawnPosition.
    /// </summary>
    /// <param name="type">Which ArmyType to spawn.</param>
    /// <param name="team">Team affiliation.</param>
    /// <param name="spawnPosition">Center world-space point for distribution.</param>
    /// <param name="delay">Seconds to wait between each unit spawn.</param>
    public void SpawnArmyByType(ArmyType type, Team team, Vector3 spawnPosition, float delay)
    {
        if (!_compositionLookup.TryGetValue(type, out var composition) || composition == null)
        {
            Debug.LogError($"ArmyManager: No composition registered for {type}");
            return;
        }
        StartCoroutine(SpawnArmyCoroutine(type, composition, team, spawnPosition, delay));
    }

    //--------------------- Helper method
    /// <summary>
    /// Picks a random walkable & unreserved node and spawns EnemyArmy there.
    /// </summary>
    private void SpawnEnemyArmyAtRandomNode()
    {
        GridNode node = GetRandomFreeNode();
        if (node == null)
        {
            Debug.LogWarning("ArmyManager: No free node found for EnemyArmy spawn.");
            return;
        }

        // Spawn delay = 0.1f
        SpawnArmyByType(ArmyType.Spearman, _enemyTeam, node.worldPosition, 0.1f);

        Debug.Log($"[ArmyManager] Hot-key spawn EnemyArmy at ({node.worldPosition.x}, {node.worldPosition.y})");
    }

    /// <summary>
    /// Returns a random walkable & unreserved node within the grid.
    /// Tries up to maxAttempts before giving up.
    /// </summary>
    private GridNode GetRandomFreeNode(int maxAttempts = 50)
    {
        if (_gridManager == null || !_gridManager.isInitialized)
            return null;

        int maxX = _gridManager.GridSettings.GridSizeX;   
        int maxY = _gridManager.GridSettings.GridSizeY;   

        for (int i = 0; i < maxAttempts; i++)
        {
            int rx = Random.Range(0, maxX);
            int ry = Random.Range(0, maxY);
            GridNode node = _gridManager.GetNode(rx, ry);

            if (node != null && node.walkable && !_gridManager.IsNodeReserved(node))
                return node;
        }
        return null;
    }

    /// <summary>
    /// Calculates the total aggregated resource costs for the given ArmyType by summing
    /// each UnitTypeSO.spawnCosts multiplied by its count in the ArmyCompositionSO.
    /// </summary>
    /// <param name="type">Army composition identifier.</param>
    /// <returns>New list of ResourceCost aggregated by resource type.</returns>
    public List<ResourceCost> GetArmyTotalCosts(ArmyType type)
    {
        // Lookup
        if (_compositionLookup == null || !_compositionLookup.TryGetValue(type, out var composition) || composition == null)
            return new List<ResourceCost>();

        // Aggregate using dictionary
        Dictionary<ResourceList, int> totals = new Dictionary<ResourceList, int>();

        foreach (var entry in composition.unitEntries)
        {
            if (entry.unitTypePrefab == null || entry.unitTypePrefab.unitType.spawnCosts == null)
                continue;

            // Multiply each unit's spawn cost by count
            int count = entry.count;
            foreach (var c in entry.unitTypePrefab.unitType.spawnCosts)
            {
                ResourceList key = c.ResourceType;
                int add = c.Amount * count;

                if (totals.TryGetValue(key, out int current))
                    totals[key] = current + add;
                else
                    totals.Add(key, add);
            }
        }

        // Convert back to list
        List<ResourceCost> list = new List<ResourceCost>();
        foreach (var kv in totals)
        {
            ResourceCost rc = new ResourceCost
            {
                ResourceType = kv.Key,
                Amount = kv.Value
            };
            list.Add(rc);
        }

        return list;
    }

    /// <summary>
    /// Resets internal state so that Initialize() can be called again for a new game.
    /// Stops any ongoing spawn coroutines, clears injected dependencies, and
    /// removes any UnitSpawned subscribers.
    /// </summary>
    public void ResetArmy()
    {
        // 1) Stop any in-progress spawning coroutines
        StopAllCoroutines();

        // 2) Clear injected dependencies
        _gridManager = null;
        _unitManager = null;
        _pathfinder = null;

        // 3) Clear event subscriptions to avoid duplicate callbacks
        UnitSpawned = null;
    }
}

