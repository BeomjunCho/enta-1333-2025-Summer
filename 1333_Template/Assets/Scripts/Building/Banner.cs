// Banner.cs
using UnityEngine;
using UnityEngine.InputSystem;
using static SfxPlayerPool;

/// <summary>
/// Banner acts as a mobile formation point. Drag to enter placement mode:
/// while holding the left mouse button, it follows the mouse cursor on the ground plane
/// and snaps to the nearest grid node. Releasing the button places it permanently at that grid node,
/// preserving its original rotation and marking that node as non-walkable.
/// Also occupies its initial grid cell at game start.
/// Fires a static event whenever it is placed.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Banner : MonoBehaviour, ISelectable
{
    [Header("Dependencies")]
    [Tooltip("Reference to the GridManager for snapping to grid.")]
    [SerializeField] private GridManager _gridManager;

    private Camera _mainCamera;
    private bool _isDragging = false;
    private Quaternion _originalRotation;
    private Plane _groundPlane;
    private GridNode _occupiedNode; // currently occupied grid node

    /// <summary>True if any banner is being dragged right now.</summary>
    public static bool IsAnyDragging { get; private set; } = false;

    /// <summary>
    /// Fired whenever this banner is dropped into place.
    /// The Vector3 argument is the new world position.
    /// </summary>
    public static event System.Action<Vector3> BannerMoved;

    private SfxHandle _flagFlappingSfxHandle = SfxHandle.Invalid;

    private void Awake()
    {
        _mainCamera = Camera.main;
        _originalRotation = transform.rotation;
        _groundPlane = new Plane(Vector3.up, Vector3.zero);
    }
    private void Start()
    {
        _flagFlappingSfxHandle = AudioManager.Instance.PlaySfxAttached(
            transform,
            FMODEvents.Instance.FlagFlapping);
    }

    private void OnDestroy()
    {
        AudioManager.Instance.StopSfx(_flagFlappingSfxHandle);
    }

    public void Initialize(GridManager gridManager)
    {
        _gridManager = gridManager;
        if(_mainCamera == null) _mainCamera = Camera.main;

        // First-time occupy after DI
        if (_occupiedNode == null && _gridManager != null)
        {
            GridNode startNode = _gridManager.GetNodeFromWorldPosition(transform.position);
            OccupyNode(startNode);
        }
    }

    private void OnMouseDown()
    {
        _isDragging = true;
        IsAnyDragging = true;
    }

    private void OnMouseUp()
    {
        if (!_isDragging) return;

        // Snap to nearest valid node
        GridNode node = PlaceAtCursor();
        if (node != null)
            OccupyNode(node);

        // End drag state
        _isDragging = false;
        IsAnyDragging = false;

        // Notify listeners of new banner position
        BannerMoved?.Invoke(transform.position);
    }

    private void Update()
    {
        if (!_isDragging) return;

        // Follow mouse on ground plane
        Ray ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (_groundPlane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            GridNode node = _gridManager.GetNodeFromWorldPosition(hitPoint);
            if (node != null && node.walkable)
            {
                transform.position = node.worldPosition;
                transform.rotation = _originalRotation;
            }
        }
    }

    private GridNode PlaceAtCursor()
    {
        Ray ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (_groundPlane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            GridNode node = _gridManager.GetNodeFromWorldPosition(hitPoint);
            if (node != null && node.walkable)
            {
                transform.position = node.worldPosition;
                transform.rotation = _originalRotation;
                return node;
            }
        }
        return null;
    }

    private void OccupyNode(GridNode node)
    {
        if (_occupiedNode != null)
            SetNodeWalkable(_occupiedNode, true);

        _occupiedNode = node;
        SetNodeWalkable(node, false);
    }

    private void SetNodeWalkable(GridNode node, bool walkable)
    {
        int x = Mathf.RoundToInt(node.worldPosition.x / _gridManager.GridSettings.NodeSize);
        int y = Mathf.RoundToInt(
            _gridManager.GridSettings.UseXZPlane
                ? node.worldPosition.z / _gridManager.GridSettings.NodeSize
                : node.worldPosition.y / _gridManager.GridSettings.NodeSize
        );
        _gridManager.SetWalkable(x, y, walkable);
    }

    // ISelectable implementation (no UI for banners)
    public void OnSelected() { }
    public void OnDeselected() { }
}
