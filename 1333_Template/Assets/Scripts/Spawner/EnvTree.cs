using UnityEngine;

/// <summary>
/// Harvestable tree resource. Worker units will “attack?this to gather wood.
/// Registers itself with the UnitManager on enable and unregisters on disable.
/// </summary>
public class EnvTree : MonoBehaviour, IDamageable
{
    [Header("Grid Footprint (cells)")]
    [SerializeField] private int _width = 1;
    [SerializeField] private int _height = 1;

    [SerializeField] private ResourceDataSO _wood;

    [Header("Hit Points")]
    [SerializeField] private int _maxHp = 40;

    [SerializeField] private int _resourceAmount = 10;
    private int _hp;
    public int Width => _width;
    public int Height => _height;

    private GridManager _gridManager;
    private int _startX, _startY;


    private UnitManager _unitManager;
    private ResourceManager _resourceManager;

    private void Awake()
    {
        _hp = _maxHp;
    }

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
        _unitManager?.UnregisterResource(this);
        _resourceManager.AddResource(_wood, _resourceAmount);
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
