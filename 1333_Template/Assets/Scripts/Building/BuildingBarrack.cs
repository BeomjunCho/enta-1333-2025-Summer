// BuildingBarrack.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static SfxPlayerPool;

/// <summary>
/// Barrack building that spawns various ArmyTypes and re-forms units around the banner.
/// </summary>
public class BuildingBarrack : BuildingBase
{
    [Header("Spawn Settings")]
    [SerializeField] private Transform _spawnPoint = null;
    [SerializeField] private float _spawnInterval = 1f;

    [Header("Formation Settings")]
    [SerializeField] private float _formationDelay = 0.5f;

    [Header("Selectable Army Types")]
    [Tooltip("ArmyTypes that this barrack can produce.")]
    [SerializeField] private List<ArmyType> _spawnableTypes = new();   

    [Header("Renderers for Selection")]
    [SerializeField] private Renderer[] _selectionRenderers;

    private ArmyManager _armyManager;
    private ResourceManager _resourceManager;
    private readonly List<UnitBase> _spawnedUnits = new();
    private SfxHandle _barrakEnv = SfxHandle.Invalid;

    /// <summary>Read-only access for UI script.</summary>
    public IReadOnlyList<ArmyType> SpawnableTypes => _spawnableTypes;

    /// <summary>Inject managers and subscribe to banner event.</summary>
    public void Initialize(ArmyManager armyManager, ResourceManager resourceManager, GridManager gridManager)
    {
        _armyManager = armyManager;
        _resourceManager = resourceManager;
        _gridManager = gridManager;
        Banner.BannerMoved += OnBannerMoved;
    }
    
    private void Start()
    {
        _barrakEnv = AudioManager.Instance.PlaySfx3D(this.transform.position, FMODEvents.Instance.BarrackEnv);
    }
    private void OnDestroy()
    {
        Banner.BannerMoved -= OnBannerMoved;

        if (Application.isPlaying == false) return;
        if (AudioManager.Instance != null)
            AudioManager.Instance.StopSfx(_barrakEnv);
    }

    /// <summary>Entry point called by UI to spawn the chosen ArmyType.</summary>
    public void SpawnUnit(ArmyType type)
    {
        StartCoroutine(SpawnAndFormWave(type));
    }

    /// <summary>Optional default method (Hot key spawning).</summary>
    public void SpawnUnit() => SpawnUnit(_spawnableTypes.Count > 0 ? _spawnableTypes[0] : ArmyType.Spearman);

    // -------------------- Internal --------------------
    private IEnumerator SpawnAndFormWave(ArmyType type)
    {
        if (_armyManager == null || _spawnPoint == null || _gridManager == null)
            yield break;

        var spawned = new List<UnitBase>();
        yield return StartCoroutine(
            _armyManager.SpawnArmyAndCollect(
                type,
                team,
                _spawnPoint.position,
                _spawnInterval,
                spawned));

        yield return new WaitForSeconds(_formationDelay);

        _spawnedUnits.Clear();
        _spawnedUnits.AddRange(spawned);
        IssueFormationOrders();
    }

    /// <summary>Issue MoveTo orders for non-selected units.</summary>
    private void IssueFormationOrders()
    {
        Banner banner = Object.FindAnyObjectByType<Banner>();
        Vector3 centerPos = banner != null ? banner.transform.position : _spawnPoint.position;

        GridNode centerNode = _gridManager.GetNodeFromWorldPosition(centerPos);
        List<GridNode> nodes = _gridManager.FindNearestFreeNodes(centerNode, _spawnedUnits.Count);

        for (int i = 0; i < _spawnedUnits.Count; i++)
        {
            UnitBase unit = _spawnedUnits[i];
            if (unit.IsSelected) continue;

            GridNode target = i < nodes.Count ? nodes[i] : centerNode;
            unit.SetReservedDestination(target);
            unit.MoveTo(target);
        }
    }
    public override void DestroySelf()
    {
        if (_gridManager != null)
        {
            for (int dx = 0; dx < _footprint.x; dx++)
                for (int dy = 0; dy < _footprint.y; dy++)
                    _gridManager.SetWalkable(_baseIdx.x + dx, _baseIdx.y + dy, true);
        }

        Banner.BannerMoved -= OnBannerMoved;
        Destroy(gameObject);
    }


    private void OnBannerMoved(Vector3 _) => IssueFormationOrders();

    // -------------------- ISelectable --------------------
    public override void OnSelected()
    {
        ShowHpBar();
        foreach (Renderer r in _selectionRenderers)
            if (r != null) r.material.color = Color.gray;
    }

    public override void OnDeselected()
    {
        HideHpBar();
        ApplyTeamMaterial();
    }
}
