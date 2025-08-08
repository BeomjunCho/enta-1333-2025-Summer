using UnityEngine;

/// <summary>
/// Harvestable rock resource. Worker units will “attack?this to mine stone.
/// Registers itself with the UnitManager on enable and unregisters on disable.
/// </summary>
public class EnvRock : MonoBehaviour, IDamageable
{
    [Header("Grid Footprint (cells)")]
    [SerializeField] private int _width = 1;
    [SerializeField] private int _height = 1;

    [SerializeField] private ResourceDataSO _rock;

    [Header("Hit Points")]
    [SerializeField] private int _maxHp = 60;

    [SerializeField] private int _resourceAmount = 10;
    public int Width => _width;
    public int Height => _height;

    private GridManager _gridManager;
    private int _startX, _startY;

    private int _hp;

    private UnitManager _unitManager;
    private ResourceManager _resourceManager;

    private void Awake()
    {
        _hp = _maxHp;
    }

    // called from spawner to inject grid info
    public void SetGridInfo(GridManager gridManager, int startX, int startY)
    {
        _gridManager = gridManager;
        _startX = startX;
        _startY = startY;
    }

    public void Initialize(UnitManager unitManager, ResourceManager resourceManager)
    {
        _unitManager = unitManager;
        if (unitManager == null) Debug.LogWarning("EnvTree: Unit Manager is null.");
        _unitManager?.RegisterResource(this);
        _resourceManager = resourceManager;
    }

    private void OnDisable()
    {
        // unregister from unit manager
        _unitManager?.UnregisterResource(this);
        _resourceManager.AddResource(_rock, _resourceAmount);
        // release occupied grid cells
        if (_gridManager != null)
        {
            for (int dx = 0; dx < _width; dx++)
            {
                for (int dy = 0; dy < _height; dy++)
                {
                    _gridManager.SetWalkable(_startX + dx, _startY + dy, true);
                }
            }
        }
    }

    // IDamageable implementation
    public Team Team => Team.Neutral;
    public bool IsAlive => _hp > 0;
    public Transform Tr => transform;

    public void TakeDamage(int amount)
    {
        // <summary>
        // Apply damage only if still alive. Prevent double-destroy exceptions.
        // </summary>

        if (_hp <= 0)
            return;  // already destroyed or dying, skip all

        _hp -= Mathf.Abs(amount);
        if (_hp <= 0)
            Destroy(gameObject);  // will trigger OnDisable to release grid, unregister
    }
}
