using UnityEngine;
using static SfxPlayerPool;
using System.Collections;

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

    [Header("Audio")]
    [SerializeField] private float _audibleRadius = 18f;
    [SerializeField] private float _checkInterval = 0.8f;

    private int _hp;
    public int Width => _width;
    public int Height => _height;

    private GridManager _gridManager;
    private int _startX, _startY;

    private UnitManager _unitManager;
    private ResourceManager _resourceManager;


    private SfxHandle _birdSingSfxHandle = SfxHandle.Invalid;
    private Transform _listener;
    private void Awake()
    {
        _hp = _maxHp;
    }
    private void Start()
    {
        _listener = Camera.main.transform;
    }
    private void OnEnable()
    {
        StartCoroutine(AmbientRoutine());
    }

    private IEnumerator AmbientRoutine()
    {
        float sqrRadius = _audibleRadius * _audibleRadius;

        while (true)
        {
            if (_listener == null)
            {
                yield return null;
                continue;
            }

            bool audible = (transform.position - _listener.position).sqrMagnitude <= sqrRadius;

            if (audible && _birdSingSfxHandle.Equals(SfxHandle.Invalid))
            {
                _birdSingSfxHandle = AudioManager.Instance.PlaySfx3D(transform.position, FMODEvents.Instance.BirdSing);
            }
            else if (!audible && !_birdSingSfxHandle.Equals(SfxHandle.Invalid))
            {
                AudioManager.Instance.StopSfx(_birdSingSfxHandle);
                _birdSingSfxHandle = SfxHandle.Invalid;
            }

            yield return new WaitForSeconds(_checkInterval);
        }
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

    private void OnDestroy()
    {
        if (Application.isPlaying == false) return;
        if (!_birdSingSfxHandle.Equals(SfxHandle.Invalid) && AudioManager.Instance != null)
            AudioManager.Instance.StopSfx(_birdSingSfxHandle);
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
