using System.Collections;
using UnityEngine;

/// <summary>
/// Abstract base for all unit types in the game (e.g., Archer, Commander).
/// Stores unit health, team, selection state, and core logic.
/// Handles animation and selection UI.
/// Delegates movement and pathfinding to the UnitMovement component.
/// </summary>
public abstract class UnitBase : MonoBehaviour, ISelectable, IDamageable
{
    // ---------- Serialized references ----------
    // Reference to the ScriptableObject describing stats/appearance for this unit.
    [Header("References")]
    [Tooltip("ScriptableObject that defines stats and visuals for this unit.")]
    [SerializeField] protected UnitTypeSO _unitType = null;

    // Handles Animator parameters based on state changes.
    [Tooltip("Component that triggers Animator parameters when state changes.")]
    [SerializeField] protected UnitAnimHandler _animHandler = null;

    // Controls pathfinding and movement for this unit.
    [Tooltip("Component responsible for pathfinding and translation.")]
    [SerializeField] private UnitMovement _movement = null;

    // ---------- Runtime data ----------
    // Stores current health points.
    protected float _currentHp;

    // Current state (Idle, Moving, Attacking, Dead, etc.)
    protected UnitState _state = UnitState.Idle;

    // Which team the unit belongs to.
    protected Team _team;

    // Reference to the global unit manager.
    protected UnitManager _unitManager;

    protected WorkerResourceGather _wRG;

    // Invoked when any unit dies.
    public static event System.Action<UnitBase> UnitDestroyed;

    // Collider used for selection by mouse/touch.
    private Collider _selectCollider;

    // Whether the unit is currently selected by the player.
    public bool IsSelected { get; private set; }

    // ---------- Properties ----------
    // Returns the width of the unit in grid cells.
    public virtual int Width => (_unitType != null) ? _unitType.Width : 1;

    // Returns the height of the unit in grid cells.
    public virtual int Height => (_unitType != null) ? _unitType.Height : 1;

    // Returns the current state (Idle, Moving, etc.).
    public UnitState CurrentState => _state;

    // Returns the team.
    public Team UnitTeam => _team;

    // Returns the unit's data asset.
    public UnitTypeSO UnitType => _unitType;

    // Returns current HP value.
    public float CurrentHp => _currentHp;

    // ---------- IDamageable interface ----------
    // Team for damage filtering.
    public Team Team => _team;

    // Is the unit alive.
    public bool IsAlive => _state != UnitState.Dead;

    // Cached transform reference for performance.
    public Transform Tr => transform;

    // Health bar UI component reference.
    private HealthBarUI _hpBar;

    // Used for spatial hash grid tracking.
    private Vector2Int _lastHash;

    // ---------- MonoBehaviour ----------
    /// <summary>
    /// Ensures all required components are present and disables health bar by default.
    /// </summary>
    protected virtual void Awake()
    {
        // Attach UnitMovement if missing.
        if (_movement == null && !TryGetComponent(out _movement))
            _movement = gameObject.AddComponent<UnitMovement>();

        // Cache collider for selection.
        _selectCollider = GetComponent<Collider>();

        // Find and hide health bar by default.
        _hpBar = GetComponentInChildren<HealthBarUI>(true);
        _hpBar?.gameObject.SetActive(false);
    }

    /// <summary>
    /// Show/Hide Health bar by pressing G/H key.
    /// </summary>
    protected virtual void Update()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            ShowHealthBar();
        }
        if (Input.GetKeyDown(KeyCode.H))
        {
            HideHealthBar();
        }
    }

    // ---------- Spatial Hash Hooks ----------
    /// <summary>
    /// Updates the unit's spatial hash entry if it moved to a new cell.
    /// Used for efficient spatial queries.
    /// </summary>
    public void RefreshSpatialHashEntry()
    {
        if (_unitManager == null) return;

        Vector2Int newHash = _unitManager.Spatial.GetHashFast(transform.position);
        if (newHash == _lastHash) return;  // If not moved to a new bucket, skip.

        // Remove from previous hash bucket.
        _unitManager.Spatial.Remove(this, _lastHash);

        // Add to new hash bucket.
        _unitManager.Spatial.Add(this);

        // Cache new bucket key.
        _lastHash = newHash;
    }

    // ---------- Initialization ----------
    /// <summary>
    /// Called right after unit creation to inject references and configure initial state.
    /// </summary>
    public virtual void Initialize(
        UnitTypeSO unitType,
        GridManager gridManager,
        UnitManager unitManager,
        AStarPathfinder pathfinder,
        Team team)
    {
        // Store all references and initial values.
        _unitType = unitType;
        _unitManager = unitManager;
        _team = team;

        _currentHp = unitType.MaxHp;
        _state = UnitState.Idle;

        // Set up movement and pathfinding.
        _movement.Init(gridManager, pathfinder, unitType.MoveSpeed, 360f, _unitManager);

        // Occupy current node on the grid.
        _movement.OccupyCurrentNode();

        // Set up combat component if present.
        UnitCombat combat = GetComponent<UnitCombat>();
        if (combat != null)
            combat.Init(unitManager, unitType);

        // Apply correct team material for visuals.
        Material teamMat = unitType.GetArmyMaterial(team);
        if (teamMat != null && TryGetComponent<UnitVisualController>(out var vc))
            vc.ApplyTeamMaterial(teamMat, unitType.IsMounted);

        // Register unit to the spatial hash.
        if (_unitManager != null)
            _unitManager.Spatial.Add(this);
        _lastHash = _unitManager.Spatial.GetHashFast(transform.position);

        if (_wRG == null && TryGetComponent<WorkerResourceGather>(out _wRG))
        {
            _wRG.Initialize(unitManager);
            Debug.Log("UnitBase: WRG is set in base and initialized");
        }
    }

    // ---------- Public API ----------
    /// <summary>
    /// Instructs the unit to move to the given grid node.
    /// </summary>
    public virtual void MoveTo(GridNode targetNode)
    {
        if (_movement == null || _movement.Equals(null)) return;
        _movement.MoveTo(targetNode);
    }

    /// <summary>
    /// Reserves a grid node as the unit's destination (prevents overlap).
    /// </summary>
    public void SetReservedDestination(GridNode node)
    {
        if (_movement != null)
            _movement.PlanAndReserveDestination(node);
    }

    /// <summary>
    /// Inflicts damage on the unit and updates the health bar.
    /// </summary>
    public virtual void TakeDamage(int rawDamage)
    {
        if (_state == UnitState.Dead) return;

        int defense = (_unitType != null) ? _unitType.Defense : 0;

        // Calculate net damage (minimum 1).
        int final = Mathf.Max(1, rawDamage - defense);

        _currentHp -= final;

        // Update health bar.
        _hpBar?.SetRatio(_currentHp / (float)_unitType.MaxHp);
        if (_currentHp <= 0f) Die();
    }

    /// <summary>
    /// Show the visibility of the health bar UI.
    /// </summary>
    public void ShowHealthBar()
    {
        if (_hpBar == null)
            return;

        _hpBar.gameObject.SetActive(true);
    }
    /// <summary>
    /// Hide the visibility of the health bar UI.
    /// </summary>
    public void HideHealthBar()
    {
        if (_hpBar == null)
            return;

        _hpBar.gameObject.SetActive(false);
    }


    /// <summary>
    /// Called when the unit becomes selected by the player.
    /// Shows selection UI and health bar.
    /// </summary>
    public virtual void OnSelected()
    {
        IsSelected = true;
        _hpBar?.gameObject.SetActive(true);
        _hpBar?.SetRatio(_currentHp / (float)_unitType.MaxHp);

        if (TryGetComponent<UnitVisualController>(out var vc))
            vc.ShowSelectionIndicator();
    }

    /// <summary>
    /// Called when the unit is deselected by the player.
    /// Hides selection UI and health bar.
    /// </summary>
    public virtual void OnDeselected()
    {
        if (this == null) return;   // If already destroyed, do nothing.
        IsSelected = false;
        _hpBar?.gameObject.SetActive(false);
        if (TryGetComponent<UnitVisualController>(out var vc))
            vc.HideSelectionIndicator();
    }

    // ---------- Internal helpers ----------
    /// <summary>
    /// Sets a new state and updates the animation handler.
    /// </summary>
    public void InternalChangeState(UnitState newState)
    {
        _state = newState;
        _animHandler?.OnStateChanged(_state);
    }

    /// <summary>
    /// Handles unit death logic: state, selection, spatial hash, and destruction.
    /// </summary>
    protected virtual void Die()
    {
        if (_state == UnitState.Dead) return;

        // Remove from spatial hash using last known key.
        if (_unitManager != null)
            _unitManager.Spatial.Remove(this, _lastHash);

        InternalChangeState(UnitState.Dead);

        OnDeselected();
        DisableSelectable();

        // Remove from spatial hash again as a failsafe.
        if (_unitManager != null)
            _unitManager.Spatial.Remove(this);

        // Notify listeners that the unit was destroyed.
        UnitDestroyed?.Invoke(this);

        // Release the grid node previously occupied.
        _movement?.ReleaseOccupiedNode();

        // Remove from unit manager registry.
        if (_unitManager != null)
            _unitManager.UnregisterUnit(this);

        AudioManager.Instance.PlaySfxAttached(this.transform, FMODEvents.Instance.UnitDead);

        // Destroy this unit after a delay (e.g. play death animation).
        StartCoroutine(DestroyAfterDelay(5f));
    }

    /// <summary>
    /// Coroutine to destroy the unit GameObject after a delay.
    /// </summary>
    private IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }

    /// <summary>
    /// Cleanup logic when the unit GameObject is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        // Remove from spatial hash to prevent memory leaks.
        if (_unitManager != null)
            _unitManager.Spatial.Remove(this);

        UnitDestroyed?.Invoke(this);
    }

    /// <summary>
    /// Disables the unit's collider to prevent further selection.
    /// </summary>
    private void DisableSelectable()
    {
        if (_selectCollider != null)
            _selectCollider.enabled = false;
    }
}
