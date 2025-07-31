using System;
using UnityEngine;

/// <summary>
/// Abstract base class for all building objects in the game.
/// Manages team visuals, health, grid footprint, and implements damage handling.
/// Provides interface for unit attacks and selection UI.
/// </summary>
[RequireComponent(typeof(Renderer))]
public abstract class BuildingBase : MonoBehaviour, ISelectable, IDamageable
{
    // ======= Team and Data =======
    // Team affiliation for this building
    [Header("Team & Data")]
    public Team team;
    // Material to use for each team (by index)
    public Material[] teamMaterials;
    // ScriptableObject containing stats and footprint
    public BuildingDataSO buildingData;

    // ======= Cached References =======
    // Renderer used to apply material and fetch bounds
    protected Renderer _renderer;
    // Reference to the grid manager for placement and navigation
    protected GridManager _gridManager;
    // Reference to the unit manager for registration/unregistration
    protected UnitManager _unitManager;

    // ======= Grid Footprint =======
    // Grid index of the building's bottom-left corner
    protected Vector2Int _baseIdx;
    // Width and height (in grid tiles) of the building's current orientation
    protected Vector2Int _footprint;

    // ======= Health =======
    // Current and maximum health values
    public int CurrentHealth { get; private set; }
    public int MaxHealth { get; private set; }

    // Reference to the UI health bar component
    private HealthBarUI _hpBar;

    // ======= IDamageable Implementation =======
    // Team for damage filtering
    public Team Team => team;
    // Is the building still alive (health > 0)?
    public bool IsAlive => CurrentHealth > 0;
    // Cached transform for position queries
    public Transform Tr => transform;

    // ======= MonoBehaviour Lifecycle =======
    /// <summary>
    /// Fetch component references and initialize health at spawn.
    /// </summary>
    protected virtual void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _hpBar = GetComponentInChildren<HealthBarUI>(true);
        _hpBar?.gameObject.SetActive(false);

        InitializeHealth();
    }

    /// <summary>
    /// Register the building to the unit manager on enable.
    /// </summary>
    private void OnEnable()
    {
        if (_unitManager != null)
            _unitManager.RegisterBuilding(this);
    }

    /// <summary>
    /// Unregister from the unit manager on disable (if not already unregistered).
    /// </summary>
    private void OnDisable()
    {
        if (_unitManager != null)
            _unitManager.UnregisterBuilding(this);
    }

    // ======= Health and Damage =======
    /// <summary>
    /// Apply incoming damage, update health bar, and destroy if health drops to zero.
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (!IsAlive) return;

        CurrentHealth -= amount;
        _hpBar?.SetRatio(CurrentHealth / (float)MaxHealth);

        if (CurrentHealth <= 0)
            DestroySelf();
    }

    /// <summary>
    /// Sets maximum and current health from building data.
    /// </summary>
    private void InitializeHealth()
    {
        MaxHealth = buildingData.Health;
        CurrentHealth = MaxHealth;
    }

    // ======= Grid & Placement =======
    /// <summary>
    /// Called immediately after spawning to inject placement info and register building.
    /// </summary>
    public void SetupPlacement(GridManager gm,
                               Vector2Int baseIdx,
                               Vector2Int footprint,
                               UnitManager um)
    {
        _gridManager = gm;
        _baseIdx = baseIdx;
        _footprint = footprint;
        _unitManager = um;

        // Register building as active
        _unitManager.RegisterBuilding(this);
    }

    /// <summary>
    /// Finds the closest point on the building's grid footprint perimeter (used for unit attacks).
    /// If grid info is missing, falls back to renderer bounds.
    /// </summary>
    public Vector3 GetClosestEdgePoint(Vector3 from)
    {
        if (this == null || this.Equals(null)) return from;

        if (_gridManager != null && _gridManager.isInitialized)
        {
            float size = _gridManager.GridSettings.NodeSize;
            bool xz = _gridManager.GridSettings.UseXZPlane;

            // World position of the bottom-left corner
            Vector3 min = _gridManager.IdxToWorld(_baseIdx, false);

            // World position of the top-right corner (exclusive, outer edge)
            Vector3 max = _gridManager.IdxToWorld(_baseIdx + _footprint, false);

            // Clamp target position to perimeter for attack targeting
            if (xz)
            {
                float cx = Mathf.Clamp(from.x, min.x, max.x);
                float cz = Mathf.Clamp(from.z, min.z, max.z);
                return new Vector3(cx, transform.position.y, cz);
            }
            else
            {
                float cx = Mathf.Clamp(from.x, min.x, max.x);
                float cy = Mathf.Clamp(from.y, min.y, max.y);
                return new Vector3(cx, cy, transform.position.z);
            }
        }

        // If grid info missing, use the renderer's bounds
        Bounds b = _renderer.bounds;
        Vector3 p = b.ClosestPoint(from);
        p.y = transform.position.y;
        return p;
    }

    // ======= Visuals =======
    /// <summary>
    /// Applies the correct team material based on the team property.
    /// </summary>
    public virtual void ApplyTeamMaterial()
    {
        if (_renderer == null || _renderer.Equals(null)) return;

        int idx = (int)team;
        if (teamMaterials != null && idx >= 0 && idx < teamMaterials.Length)
            _renderer.material = teamMaterials[idx];
    }

    // ======= Destruction =======
    /// <summary>
    /// Frees all occupied grid tiles and unregisters from the unit manager.
    /// Safely destroys the GameObject.
    /// </summary>
    public virtual void DestroySelf()
    {
        // Free all grid tiles occupied by the building
        if (_gridManager != null)
        {
            for (int dx = 0; dx < _footprint.x; dx++)
                for (int dy = 0; dy < _footprint.y; dy++)
                    _gridManager.SetWalkable(_baseIdx.x + dx,
                                             _baseIdx.y + dy, true);
        }

        if (_unitManager != null)
            _unitManager.UnregisterBuilding(this);

        Destroy(gameObject);
    }

    // ======= Selection UI =======
    /// <summary>
    /// Called when the building is selected. Must be implemented by subclasses.
    /// </summary>
    public abstract void OnSelected();

    /// <summary>
    /// Called when the building is deselected. Must be implemented by subclasses.
    /// </summary>
    public abstract void OnDeselected();

    /// <summary>
    /// Shows the building's health bar UI.
    /// </summary>
    protected void ShowHpBar()
    {
        if (_hpBar != null)
            _hpBar.gameObject.SetActive(true);
    }

    /// <summary>
    /// Hides the building's health bar UI.
    /// </summary>
    protected void HideHpBar()
    {
        if (_hpBar != null)
            _hpBar.gameObject.SetActive(false);
    }
}
