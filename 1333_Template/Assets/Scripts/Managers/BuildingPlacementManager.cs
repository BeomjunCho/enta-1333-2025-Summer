// BuildingPlacementManager.cs
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// Manages the building placement process in the game: preview rendering, rotation,
/// placement validation, building instantiation, and manager injection into new buildings.
/// Handles player input for placing and cancelling buildings.
/// </summary>
public class BuildingPlacementManager : MonoBehaviour
{
    [Header("BuildingPlacementUI")]
    [SerializeField] private BuildingPlacementUI _bpUI;
    // ======= Ghost Preview Materials =======
    [Header("Ghost Preview Materials")]
    [Tooltip("Semi-transparent green material for valid placement")]
    [SerializeField] private Material _ghostValidMaterial;
    [Tooltip("Semi-transparent red material for invalid placement")]
    [SerializeField] private Material _ghostInvalidMaterial;

    [SerializeField] private BuildingPlacementUIHelper _uiHelper;

    // ======= Internal State =======
    // Reference to the main camera
    private Camera _mainCamera;
    // The current building data being placed
    private BuildingDataSO _currentBuildingData;
    // The current preview (ghost) GameObject
    private GameObject _previewInstance;
    // The preview's base rotation (for reset)
    private Quaternion _previewBaseRotation;
    // The preview's current Y-axis rotation in degrees
    private int _currentYRotation = 0;

    // ======= Injected Managers =======
    // Handles game resources (cost checks, resource assignment)
    private ResourceManager _resourceManager;
    // Handles army/unit spawning (used for barracks etc.)
    private ArmyManager _armyManager;
    // Handles the grid and tile occupancy
    private GridManager _gridManager;
    // Handles unit registration for the game
    private UnitManager _unitManager;

    /// <summary>
    /// Dependency injection for resource, army, grid, and unit managers.
    /// Called at game start by the GameManager.
    /// </summary>
    public void Initialize(ResourceManager resourceManager, ArmyManager armyManager, GridManager gridManager, UnitManager unitManager, Camera camera)
    {
        _resourceManager = resourceManager;
        _armyManager = armyManager;
        _gridManager = gridManager;
        _unitManager = unitManager;
        _mainCamera = camera;
        _bpUI.InitializeBuildingPlacementUI();
        // Error logs for missing dependencies
        if (_gridManager == null)
            Debug.LogError("BuildingPlacementManager: GridManager is not assigned.");
        if (_armyManager == null)
            Debug.LogError("BuildingPlacementManager: ArmyManager is not assigned.");
        if (_resourceManager == null)
            Debug.LogError("BuildingPlacementManager: ResourceManager is not assigned.");
        if (_unitManager == null)
            Debug.LogError("BuildingPlacementManager: UnitManager is not assigned.");
        if (_mainCamera == null)
            Debug.LogError("BuildingPlacementManager: MainCamera not found.");
    }

    /// <summary>
    /// Shows or hides the entire Building-Placement UI.
    /// Call this from scene-load callbacks.
    /// </summary>
    public void ToggleUI(bool visible)
    {
        if (_bpUI != null)
            _bpUI.gameObject.SetActive(visible);
    }

    /// <summary>
    /// Handles per-frame player input, preview updates, rotation,
    /// placement/cancel actions, and UI pointer blocking.
    /// </summary>
    private void Update()
    {
        // No active preview: nothing to update
        if (_previewInstance == null)
        {
            _uiHelper.Hide();
            return;
        }
        // Show building ui helper and follow mouse
        _uiHelper.Show();
        Vector2 mousePos = Mouse.current.position.ReadValue();
        _uiHelper.UpdatePosition(mousePos);

        // Rotate preview on middle mouse button
        if (Mouse.current.middleButton.wasPressedThisFrame)
        {
            _currentYRotation = (_currentYRotation + 90) % 360;
            ApplyRotation(_previewInstance.transform, _previewBaseRotation, _currentYRotation);
        }

        // Always update preview position based on mouse
        if (TryGetMouseWorldPosition(out Vector3 hitPoint))
        {
            bool canPlace = CanPlace(_currentBuildingData, hitPoint, _currentYRotation, out Vector3 snapPos);
            _previewInstance.transform.position = snapPos;
            ApplyGhostMaterial(_previewInstance, canPlace ? _ghostValidMaterial : _ghostInvalidMaterial);

            // On valid left-click, place building if not clicking on UI
            if (canPlace && Mouse.current.leftButton.wasPressedThisFrame && !IsPointerOverUI())
            {
                PlaceRealBuilding(hitPoint);
                return;
            }
        }

        // Cancel placement on right-click, unless over UI
        if (Mouse.current.rightButton.wasPressedThisFrame && !IsPointerOverUI())
        {
            CancelPlacement();
        }
    }

    /// <summary>
    /// Starts building placement for the given building data.
    /// Destroys any existing preview.
    /// </summary>
    public void StartPlacement(BuildingDataSO buildingData)
    {
        if (_previewInstance != null)
            Destroy(_previewInstance);

        _currentBuildingData = buildingData;
        _currentYRotation = 0;
        CreatePreview();
    }

    /// <summary>
    /// Cancels the current placement and removes the preview.
    /// </summary>
    private void CancelPlacement()
    {
        if (_previewInstance != null)
            Destroy(_previewInstance);

        _previewInstance = null;
        _currentYRotation = 0;
    }

    /// <summary>
    /// Instantiates the real building at the given position and injects all required dependencies.
    /// Sets up team, manager references, grid occupancy, and calls the correct building initialization logic.
    /// Destroys the preview and spawns a new one (for chain placement).
    /// </summary>
    private void PlaceRealBuilding(Vector3 worldPosition)
    {
        if (!_resourceManager.SpendCosts(_currentBuildingData.Costs))
        {
            Debug.Log("Not enough resources to build " + _currentBuildingData.BuildingName);
            CancelPlacement(); 
            return;
        }

        var realGO = Instantiate(_currentBuildingData.BuildingPrefab);
        var realBase = realGO.GetComponent<BuildingBase>();
        realBase.buildingData = _currentBuildingData;
        realBase.team = Team.Player;
        realBase.ApplyTeamMaterial();

        // Set the rotation to match the preview's current orientation
        realGO.transform.rotation = _previewInstance.transform.rotation;

        Vector2Int baseIdx = GetBaseIndices(worldPosition);
        Vector2Int footprint = GetRotatedSize(_currentBuildingData, _currentYRotation);

        // Specialized initialization for gates
        if (realBase is BuildingGate gate)
            gate.InitializePlacement(baseIdx, footprint, _gridManager);

        // Injection for barracks (army and resource managers)
        if (realBase is BuildingBarrack barrack)
            barrack.Initialize(_armyManager, _resourceManager, _gridManager);

        // Resource-producing buildings receive a resource manager reference
        if (realBase is BuildingResource br && _resourceManager != null)
        {
            br.Initialize(_resourceManager);
        }

        // Snap the final position to the grid
        Vector3 snapPos = CalculateSnapPosition(baseIdx, _currentBuildingData, _currentYRotation);
        realGO.transform.position = snapPos;
        MarkAreaOccupied(baseIdx, footprint, false);

        // Register building info for later use (e.g. demolition)
        realBase.SetupPlacement(_gridManager, baseIdx, footprint, _unitManager);

        Destroy(_previewInstance);
        _previewInstance = null;
        CreatePreview();
    }

    /// <summary>
    /// Instantiates a ghost preview model and applies initial material/rotation.
    /// </summary>
    private void CreatePreview()
    {
        _previewInstance = Instantiate(_currentBuildingData.BuildingModel);
        _previewBaseRotation = _previewInstance.transform.rotation;
        ApplyRotation(_previewInstance.transform, _previewBaseRotation, _currentYRotation);
        ApplyGhostMaterial(_previewInstance, _ghostInvalidMaterial);
    }

    /// <summary>
    /// Checks if the mouse pointer is currently over any UI element (ignoring objects tagged with "UIIgnore").
    /// </summary>
    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
            return false;

        var pointerData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (var res in results)
            if (!res.gameObject.CompareTag("UIIgnore"))
                return true;

        return false;
    }

    /// <summary>
    /// Applies a rotation offset to the target transform, based on its base rotation and desired Y angle.
    /// </summary>
    private void ApplyRotation(Transform target, Quaternion baseRot, int yRot)
    {
        Vector3 e = baseRot.eulerAngles;
        target.rotation = Quaternion.Euler(e.x, e.y + yRot, e.z);
    }

    /// <summary>
    /// Recursively applies the given material to all renderers in the instance's children.
    /// Used for ghost preview coloring.
    /// </summary>
    private void ApplyGhostMaterial(GameObject instance, Material mat)
    {
        foreach (var r in instance.GetComponentsInChildren<Renderer>())
            r.material = mat;
    }

    /// <summary>
    /// Gets the world position on the ground plane from the current mouse position.
    /// </summary>
    private bool TryGetMouseWorldPosition(out Vector3 pos)
    {
        Ray ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane groundPlane = new(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out float d))
        {
            pos = ray.GetPoint(d);
            return true;
        }
        pos = Vector3.zero;
        return false;
    }

    /// <summary>
    /// Determines if the building can be placed at the requested position/rotation,
    /// returning both placement validity and the correct snap position.
    /// </summary>
    private bool CanPlace(BuildingDataSO data, Vector3 worldPos, int rot, out Vector3 snap)
    {
        Vector2Int idx = GetBaseIndices(worldPos);
        snap = CalculateSnapPosition(idx, data, rot);
        return IsAreaWalkable(idx, GetRotatedSize(data, rot));
    }

    /// <summary>
    /// Converts a world position to the grid indices of the building's bottom-left tile.
    /// </summary>
    private Vector2Int GetBaseIndices(Vector3 worldPos)
    {
        var node = _gridManager.GetNodeFromWorldPosition(worldPos);
        float sz = _gridManager.GridSettings.NodeSize;
        return new Vector2Int(
            Mathf.RoundToInt(node.worldPosition.x / sz),
            Mathf.RoundToInt(node.worldPosition.z / sz)
        );
    }

    /// <summary>
    /// Calculates the snapped grid-aligned position for a building at the given indices, size, and rotation.
    /// </summary>
    private Vector3 CalculateSnapPosition(Vector2Int idx, BuildingDataSO data, int rot)
    {
        float sz = _gridManager.GridSettings.NodeSize;
        var fp = GetRotatedSize(data, rot);
        float w = fp.x * sz, d = fp.y * sz;
        float y = _gridManager.GetNodeFromWorldPosition(
            new Vector3(idx.x * sz, 0, idx.y * sz)
        ).worldPosition.y;
        return new Vector3(
            idx.x * sz + (w - sz) * 0.5f,
            y,
            idx.y * sz + (d - sz) * 0.5f
        );
    }

    /// <summary>
    /// Returns the correct grid footprint (size) for the given rotation.
    /// </summary>
    private Vector2Int GetRotatedSize(BuildingDataSO data, int rot) =>
        (rot % 180 == 0)
            ? new Vector2Int(data.SizeX, data.SizeZ)
            : new Vector2Int(data.SizeZ, data.SizeX);

    /// <summary>
    /// Checks if every grid cell in the building's footprint is walkable (not blocked or out of bounds).
    /// </summary>
    private bool IsAreaWalkable(Vector2Int idx, Vector2Int fp)
    {
        for (int x = 0; x < fp.x; x++)
            for (int y = 0; y < fp.y; y++)
            {
                var n = _gridManager.GetNode(idx.x + x, idx.y + y);
                if (n == null || !n.walkable)
                    return false;
            }
        return true;
    }

    /// <summary>
    /// Marks a rectangular area on the grid as occupied or walkable, depending on the 'walkable' flag.
    /// Used when placing or removing buildings.
    /// </summary>
    private void MarkAreaOccupied(Vector2Int idx, Vector2Int fp, bool walkable)
    {
        for (int x = 0; x < fp.x; x++)
            for (int y = 0; y < fp.y; y++)
                _gridManager.SetWalkable(idx.x + x, idx.y + y, walkable);
    }

    /// <summary>
    /// Cancels any in-progress placement, hides the placement UI,
    /// and clears injected references so Initialize() can run again.
    /// </summary>
    public void ResetPlacement()
    {
        // 1) Cancel any active ghost preview
        if (_previewInstance != null)
            Destroy(_previewInstance);
        _previewInstance = null;
        _currentBuildingData = null;
        _currentYRotation = 0;

        // 2) Hide placement UI
        ToggleUI(false);

        // 3) Clear injected manager and camera references
        _mainCamera = null;
        _gridManager = null;
        _unitManager = null;
        _armyManager = null;
        _resourceManager = null;
    }
}
