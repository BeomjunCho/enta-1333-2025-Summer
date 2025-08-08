using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A gate building that can open and close, toggling the walkability
/// of its central passage cells on the grid, and supports selection highlighting.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class BuildingGate : BuildingBase
{
    [SerializeField] private SkinnedMeshRenderer[] _skinnedRenderers;
    [SerializeField] private Animator _animator;
    [SerializeField] private bool _isWood;

    public enum GateState { Closed, Opening, Open, Closing }
    private GateState _currentState = GateState.Closed;

    private Vector2Int _placementBase;
    private Vector2Int _placementFootprint;
    private Vector2Int[] _centerOffsets;

    private static readonly int OpenTrigger = Animator.StringToHash("Open");
    private static readonly int CloseTrigger = Animator.StringToHash("Close");

    protected override void Update()
    {
        base.Update();
        if (Input.GetKeyDown(KeyCode.V)) OpenGate();
        if (Input.GetKeyDown(KeyCode.B)) CloseGate();
    }

    /// <summary>
    /// Cache base Awake logic and ensure renderer/animator reference.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        if (_skinnedRenderers == null || _skinnedRenderers.Length == 0)
            _skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();
    }

    /// <summary>
    /// Apply the team material to all renderers.
    /// </summary>
    public override void ApplyTeamMaterial()
    {
        base.ApplyTeamMaterial();
        var mat = teamMaterials[(int)team];
        foreach (var smr in _skinnedRenderers)
            if (smr != null)
                smr.material = mat;
    }

    /// <summary>
    /// Initialize placement data including the grid reference, footprint dimensions,
    /// and compute offsets for the central passage cells along width or height.
    /// </summary>
    /// <param name="baseIndices">The lower-left grid cell indices of the gate.</param>
    /// <param name="footprint">Width × height in grid cells (already rotated).</param>
    /// <param name="gridManager">Reference to the grid manager.</param>
    public void InitializePlacement(Vector2Int baseIndices, Vector2Int footprint, GridManager gridManager)
    {
        _placementBase = baseIndices;
        _placementFootprint = footprint;
        _gridManager = gridManager;

        // Determine if gate is rotated (footprint.x differs from default width)
        bool rotated = footprint.x != buildingData.SizeX;
        var offsets = new List<Vector2Int>();

        if (!rotated)
        {
            // Default (horizontal): open four central rows across full width
            int half = footprint.x / 2;
            for (int y = 0; y < footprint.y; y++)
            {
                offsets.Add(new Vector2Int(half - 2, y));
                offsets.Add(new Vector2Int(half - 1, y));
                offsets.Add(new Vector2Int(half, y));
                offsets.Add(new Vector2Int(half + 1, y));
            }
        }
        else
        {
            // Rotated (vertical): open four central columns across full height
            int half = footprint.y / 2;
            for (int x = 0; x < footprint.x; x++)
            {
                offsets.Add(new Vector2Int(x, half - 2));
                offsets.Add(new Vector2Int(x, half - 1));
                offsets.Add(new Vector2Int(x, half));
                offsets.Add(new Vector2Int(x, half + 1));
            }
        }

        _centerOffsets = offsets.ToArray();
    }

    /// <summary>
    /// Trigger the opening animation and toggle walkability immediately.
    /// </summary>
    public void OpenGate()
    {
        if (_currentState == GateState.Opening || _currentState == GateState.Open)
            return;
        _currentState = GateState.Opening;
        _animator?.SetTrigger(OpenTrigger);
        if (_isWood)
        {
            AudioManager.Instance.PlaySfx3DWithLabelParameter(this.transform.position, FMODEvents.Instance.WoodGateOpenClose, "Door", "Open");
        }
        else
        {
            AudioManager.Instance.PlaySfx3DWithLabelParameter(this.transform.position, FMODEvents.Instance.StoneGateOpenClose, "Door", "Open");
        }
        OnGateGridOpened();
    }

    /// <summary>
    /// Trigger the closing animation and toggle walkability immediately.
    /// </summary>
    public void CloseGate()
    {
        if (_currentState == GateState.Closing || _currentState == GateState.Closed)
            return;
        _currentState = GateState.Closing;
        _animator?.SetTrigger(CloseTrigger);
        if (_isWood)
        {
            AudioManager.Instance.PlaySfx3DWithLabelParameter(this.transform.position, FMODEvents.Instance.WoodGateOpenClose, "Door", "Close");
        }
        else
        {
            AudioManager.Instance.PlaySfx3DWithLabelParameter(this.transform.position, FMODEvents.Instance.StoneGateOpenClose, "Door", "Close");
        }
        OnGateGridClosed();
    }

    /// <summary>
    /// Marks the central passage cells as walkable.
    /// </summary>
    public void OnGateGridOpened()
    {
        _currentState = GateState.Open;
        if (_gridManager == null) return;
        foreach (var offset in _centerOffsets)
        {
            int x = _placementBase.x + offset.x;
            int y = _placementBase.y + offset.y;
            _gridManager.SetWalkable(x, y, true);
            Debug.Log($"[Gate] Opened cell ({x},{y}) → walkable");
        }
    }

    /// <summary>
    /// Marks the central passage cells as non-walkable.
    /// </summary>
    public void OnGateGridClosed()
    {
        _currentState = GateState.Closed;
        if (_gridManager == null) return;
        foreach (var offset in _centerOffsets)
        {
            int x = _placementBase.x + offset.x;
            int y = _placementBase.y + offset.y;
            _gridManager.SetWalkable(x, y, false);
            Debug.Log($"[Gate] Closed cell ({x},{y}) → blocked");
        }
    }

    /// <summary>
    /// Highlight selection state by tinting the gate gray.
    /// </summary>
    public override void OnSelected()
    {
        ShowHpBar();
        foreach (var smr in _skinnedRenderers)
            if (smr != null)
                smr.material.color = Color.gray;
    }

    /// <summary>
    /// Restore the original team materials when deselected.
    /// </summary>
    public override void OnDeselected()
    {
        HideHpBar();
        ApplyTeamMaterial();
    }
}
