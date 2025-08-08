// BuildingResource.cs
using System.Collections;
using UnityEngine;
using static SfxPlayerPool;

/// <summary>
/// Represents a building that produces a chosen resource over time.
/// </summary>
public class BuildingResource : BuildingBase
{
    [Header("Renderers for Selection")]
    [Tooltip("Assign specific mesh renderers to tint on selection")]
    [SerializeField] private Renderer[] _selectionRenderers;

    [Header("Resource Production")]
    [Tooltip("Which ScriptableObject resource this building produces")]
    [SerializeField] private ResourceDataSO _resourceType;
    [Tooltip("Amount of resource produced each interval")]
    [SerializeField] private int _productionAmount = 1;
    [Tooltip("Seconds between production ticks")]
    [SerializeField] private float _productionInterval = 1f;

    private ResourceManager _resourceManager;
    private float _productionTimer;

    public ResourceDataSO ResourceType => _resourceType;
    public int ProductionAmount => _productionAmount;
    public float ProductionInterval => _productionInterval;

    protected override void Awake()
    {
        base.Awake();
        if (_selectionRenderers == null || _selectionRenderers.Length == 0)
            _selectionRenderers = GetComponentsInChildren<Renderer>();
    }

    private SfxHandle _resourceBuildingEnv = SfxHandle.Invalid;
    private void Start()
    {
        switch (_resourceType.ResourceType)
        {
            case (ResourceList.Wood):
                _resourceBuildingEnv = AudioManager.Instance.PlaySfx3D(this.transform.position, FMODEvents.Instance.LumberMillEnv);
                break;
            case (ResourceList.Horse):
                _resourceBuildingEnv = AudioManager.Instance.PlaySfx3D(this.transform.position, FMODEvents.Instance.StableEnv);
                break;
            case (ResourceList.Rock):
                _resourceBuildingEnv = AudioManager.Instance.PlaySfx3D(this.transform.position, FMODEvents.Instance.WorkShopEnv);
                break;
            case (ResourceList.Iron):
                _resourceBuildingEnv = AudioManager.Instance.PlaySfx3D(this.transform.position, FMODEvents.Instance.BlackSmithEnv);
                break;
            case (ResourceList.Bread):
                _resourceBuildingEnv = AudioManager.Instance.PlaySfx3D(this.transform.position, FMODEvents.Instance.FarmEnv);
                break;
            default:
                _resourceBuildingEnv = AudioManager.Instance.PlaySfx3D(this.transform.position, FMODEvents.Instance.BlackSmithEnv);
                break;
        }
           
    }
    private void OnDestroy()
    {
        if (Application.isPlaying == false) return;
        if (AudioManager.Instance != null)
            AudioManager.Instance.StopSfx(_resourceBuildingEnv);
    }

    protected override void Update()
    {
        base.Update();
        if (_resourceManager == null || _resourceType == null)
            return;

        _productionTimer += Time.deltaTime;
        if (_productionTimer >= _productionInterval)
        {
            _productionTimer -= _productionInterval;
            _resourceManager.AddResource(_resourceType, _productionAmount);
        }
    }

    /// <summary>
    /// Injects the ResourceManager after instantiation.
    /// Must be called by BuildingPlacementManager.
    /// </summary>
    public void Initialize(ResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
    }

    public override void OnSelected()
    {
        ShowHpBar();
        foreach (var r in _selectionRenderers)
            if (r != null)
                r.material.color = Color.gray;
    }

    public override void OnDeselected()
    {
        HideHpBar();
        ApplyTeamMaterial();
    }
}
