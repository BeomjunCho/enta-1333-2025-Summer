using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages all units and buildings in the scene.
/// Other managers can query these lists, e.g., SelectionManager.
/// This class is no longer a singleton; inject it through GameManager.
/// </summary>
public class UnitManager : MonoBehaviour
{
    // Internal set of all registered units (fast add/remove, no duplicates).
    private readonly HashSet<UnitBase> _allUnits = new(256);

    // Internal set of all registered buildings.
    private readonly HashSet<BuildingBase> _allBuildings = new(64);

    // LayerMask used for line-of-sight blocking (e.g., walls).
    [SerializeField] private LayerMask _visionBlockMask;

    // Expose units as read-only collection to external scripts.
    public IReadOnlyCollection<UnitBase> AllUnits => _allUnits;

    // Expose buildings as read-only collection.
    public IReadOnlyCollection<BuildingBase> AllBuildings => _allBuildings;

    // Size of each spatial hash cell (in world units).
    [SerializeField] private float _spatialCellSize = 1.5f;

    // SpatialHash instance for efficient proximity queries.
    private SpatialHash _spatial;
    public SpatialHash Spatial => _spatial;

    // ---------- Spatial Hash for Resources ----------
    private ResourceSpatialHash _resourceSpatial;

    /// <summary>Raised after a unit is removed from the registry.</summary>
    public event Action<UnitBase> OnUnitUnregistered;

    // DEBUG SETTINGS
    [Header("Debug")]
    [Tooltip("When true, the registry is printed every frame in Update.")]
    [SerializeField] private bool _logRegistryEveryFrame = false;

    [Tooltip("Seconds between logs. Set to 0 to log every frame.")]
    [SerializeField] private float _logInterval = 0.5f;

    // Timer to track logging intervals.
    private float _logTimer = 0f;

    [SerializeField] private Team _playerTeam = Team.Player;
    private const string _inGameScene = "InGame";

    // MonoBehaviour Awake is called when the script instance is being loaded.
    private void Awake()
    {
        // Create the spatial hash with the specified cell size.
        _spatial = new SpatialHash(_spatialCellSize);

        // Initialize resource hash with same cell size
        _resourceSpatial = new ResourceSpatialHash(_spatialCellSize);
    }

    // MonoBehaviour Update is called once per frame.
    private void Update()
    {
        // Only log registry if debug flag is on.
        if (!_logRegistryEveryFrame) return;

        // Accumulate time and log when interval is reached.
        _logTimer += Time.deltaTime;
        if (_logTimer < _logInterval) return;
        _logTimer = 0f;

        // Print the current registry contents to the console.
        PrintRegistryDebug();
    }

    /// <summary>
    /// Checks current scene + hostile-unit presence and sets FMOD MusicState.
    /// </summary>
    private void RefreshMusicState()
    {
        // Skip if it is not InGame scene
        if (SceneManager.GetActiveScene().name != _inGameScene) return;

        // Check enemies in list
        bool enemyAlive = _allUnits.Any(u =>
            u != null && u.IsAlive && u.UnitTeam != _playerTeam);

        // Parameter change
        AudioManager.Instance.SetMusicState(
            enemyAlive ? MusicState.Battle : MusicState.InGame);
    }

    /// <summary>
    /// Writes the current contents of _allUnits and _allBuildings to the console.
    /// </summary>
    public void PrintRegistryDebug()
    {
        const string header = "UnitManager Registry Dump";
        var sb = new System.Text.StringBuilder(header).AppendLine();

        // List all units with their name, team, and state.
        sb.AppendLine($"Units ({_allUnits.Count})");
        foreach (UnitBase u in _allUnits)
        {
            if (u == null) continue;
            sb.AppendLine($" - {u.name} | Team: {u.UnitTeam} | State: {u.CurrentState}");
        }

        // List all buildings with their name, team, and health.
        sb.AppendLine($"\nBuildings ({_allBuildings.Count})");
        foreach (BuildingBase b in _allBuildings)
        {
            if (b == null) continue;
            sb.AppendLine($" - {b.Tr.name} | Team: {b.Team} | HP: {b.CurrentHealth}");
        }

        // Log the assembled string and ping this object in console.
        Debug.Log(sb.ToString(), this);
    }

    /// <summary>Call this when a new unit spawns in the scene.</summary>
    public void RegisterUnit(UnitBase unit)
    {
        if (unit != null)
            _allUnits.Add(unit); // HashSet.Add ignores duplicates.
        _spatial.Add(unit);

        RefreshMusicState();
    }

    /// <summary>Call this when a unit dies and is destroyed.</summary>
    public void UnregisterUnit(UnitBase unit)
    {
#if UNITY_EDITOR && DEBUG_LOG_UNREGISTER
    Debug.Log($"Unregister {unit}");
#endif
        if (_allUnits.Remove(unit))
        {
            _spatial.Remove(unit);
            OnUnitUnregistered?.Invoke(unit);
            RefreshMusicState();
        }
    }

    /// <summary>
    /// Returns true when no obstacle collider exists between the two points.
    /// Casts a thin ray at eye height (0.5 meters).
    /// </summary>
    private bool HasLineOfSight(Vector3 a, Vector3 b)
    {
        const float eyeHeight = 0.5f;
        Vector3 from = a + Vector3.up * eyeHeight;
        Vector3 to = b + Vector3.up * eyeHeight;

        // Linecast returns true if something is hit. Invert for clear sight.
        return !Physics.Linecast(from, to, _visionBlockMask);
    }

    /// <summary>Call this when a building is created.</summary>
    public void RegisterBuilding(BuildingBase b)
    {
        if (b != null)
            _allBuildings.Add(b);
    }

    /// <summary>Call this when a building is destroyed.</summary>
    public void UnregisterBuilding(BuildingBase b)
    {
        _allBuildings.Remove(b);
    }

    /// <summary>
    /// Returns the closest attackable point on a target: for buildings use edge, for units use pivot.
    /// </summary>
    private Vector3 GetDamageablePos(IDamageable dmg, Vector3 from)
    {
        if (dmg is BuildingBase bb)
            return bb.GetClosestEdgePoint(from);
        return dmg.Tr.position;
    }

    /// <summary>
    /// Finds the nearest hostile damageable (unit or building) within range using the provided list.
    /// Garrisoned units can ignore line-of-sight blocking.
    /// </summary>
    private IDamageable FindNearest(
        UnitBase seeker,
        float range,
        IEnumerable<IDamageable> list)
    {
        Team seekerTeam = seeker.UnitTeam;
        Vector3 seekerPos = seeker.transform.position;
        bool skipLOS = seeker.GetComponent<UnitCombat>()?.IsGarrisoned ?? false;

        float bestDist = float.MaxValue;
        IDamageable pick = null;

        foreach (IDamageable t in list)
        {
            if (t == null || !t.IsAlive || t.Team == seekerTeam)
                continue;

            Vector3 tgtPos = GetDamageablePos(t, seekerPos);
            float d = Vector3.Distance(seekerPos, tgtPos);
            if (d > range || d >= bestDist)
                continue;

            if (t is UnitBase && !skipLOS)
            {
                if (!HasLineOfSight(seekerPos, tgtPos))
                    continue;
            }

            bestDist = d;
            pick = t;
        }

        return pick;
    }

    /// <summary>
    /// Returns the nearest hostile unit inside <paramref name="range"/>.
    /// Uses a two-phase filter: distance (cheap) ¡æ line-of-sight (expensive) on a
    /// small candidate set, eliminating dozens of per-frame raycasts seen before.
    /// </summary>
    /// <param name="seeker">The querying unit.</param>
    /// <param name="range">Maximum search radius.</param>
    /// <returns>Closest visible enemy unit, or <c>null</c> if none.</returns>
    public UnitBase FindNearestEnemyUnit(UnitBase seeker, float range)
    {
        if (_spatial == null) return null;

        Team seekerTeam = seeker.Team;
        Vector3 seekerPos = seeker.transform.position;
        float rangeSqr = range * range;

        // --- phase 1: find up to three closest enemies (distance only) -----------
        UnitBase[] picks = { null, null, null };
        float[] dists = { rangeSqr, rangeSqr, rangeSqr };

        foreach (UnitBase t in _spatial.Query(seekerPos, range))
        {
            if (t == null || t == seeker || !t.IsAlive || t.Team == seekerTeam)
                continue;

            float d2 = (t.transform.position - seekerPos).sqrMagnitude;
            if (d2 >= dists[2]) continue;              // Not within top-3

            // Insert into sorted slot
            if (d2 < dists[0])
            {
                dists[2] = dists[1]; picks[2] = picks[1];
                dists[1] = dists[0]; picks[1] = picks[0];
                dists[0] = d2; picks[0] = t;
            }
            else if (d2 < dists[1])
            {
                dists[2] = dists[1]; picks[2] = picks[1];
                dists[1] = d2; picks[1] = t;
            }
            else
            {
                dists[2] = d2; picks[2] = t;
            }
        }

        // --- phase 2: expensive LOS check (closest first) ------------------------
        for (int i = 0; i < picks.Length; i++)
        {
            if (picks[i] == null) continue;
            if (HasLineOfSight(seekerPos, picks[i].transform.position))
                return picks[i];
        }

        return null; // No visible enemy within range
    }

    /// <summary>
    /// Returns nearest hostile building inside 'range'.
    /// </summary>
    public IDamageable FindNearestEnemyBuilding(UnitBase seeker, float range)
    {
        // Use the generic FindNearest method on the buildings set.
        return FindNearest(seeker, range, _allBuildings);
    }

    // ---------- Resource Registration ----------
    public void RegisterResource(IDamageable resource)
    {
        _resourceSpatial.Add(resource);
    }

    public void UnregisterResource(IDamageable resource)
    {
        _resourceSpatial.Remove(resource);
    }

    /// <summary>
    /// Finds the nearest alive resource within the given range.
    /// Returns null if none found.
    /// </summary>
    public IDamageable FindNearestResource(Vector3 from, float range)
    {
        IDamageable best = null;
        float bestSqr = range * range;

        foreach (var r in _resourceSpatial.Query(from, range))
        {
            if (!r.IsAlive) continue;
            float d2 = (r.Tr.position - from).sqrMagnitude;
            if (d2 < bestSqr)
            {
                bestSqr = d2;
                best = r;
            }
        }
        return best;
    }

    public void ResetUnits()
    {
        foreach (var u in _allUnits.ToArray())
            if (u != null) Destroy(u.gameObject);

        _allUnits.Clear();
        _spatial.Clear();
        _resourceSpatial.Clear();
    }
}
