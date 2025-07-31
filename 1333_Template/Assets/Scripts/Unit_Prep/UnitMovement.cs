using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles pathfinding, rotation, and movement for units.
/// Supports a two-phase system: first reserves a target node, then performs pathfinding and moves there.
/// Ensures grid occupancy is synchronized with unit movement.
/// </summary>
[RequireComponent(typeof(UnitBase))]
public class UnitMovement : MonoBehaviour
{
    // ======= Injected references =======
    // Reference to the core unit script
    private UnitBase _unit;
    // Reference to the grid manager for node lookup and reservation
    private GridManager _grid;
    // Reference to the pathfinder for computing paths
    private AStarPathfinder _pathfinder;

    // ======= Movement settings =======
    // Movement speed in world units per second
    private float _moveSpeed = 5f;
    // Rotation speed in degrees per second
    private float _rotationSpeed = 360f;

    // ======= Path and reservation state =======
    // The computed path to follow (list of grid indices)
    private List<Vector2Int> _path;
    // Index of the next waypoint in the path
    private int _nextIdx;
    // Node that is currently reserved as the destination
    private GridNode _reservedDest;

    // Pause flag (used by combat logic to temporarily halt movement)
    private bool _isPaused = false;
    // The node the unit is currently occupying (used for grid logic)
    private GridNode _currentNode;
    // Public property to allow external queries for the unit's grid
    public GridManager Grid => _grid;

    // ======= Debug Gizmo control =======
    // Static flag to enable or disable path visualization
    public static bool ShowPathGizmos = false;

    // ======= Initialization =======
    /// <summary>
    /// Cache reference to the UnitBase script.
    /// </summary>
    private void Awake() => _unit = GetComponent<UnitBase>();

    /// <summary>
    /// Inject required references and set movement/rotation speed.
    /// </summary>
    public void Init(GridManager grid, AStarPathfinder pathfinder,
                     float moveSpeed, float rotationSpeed)
    {
        _grid = grid;
        _pathfinder = pathfinder;
        _moveSpeed = moveSpeed;
        _rotationSpeed = rotationSpeed;
    }

    /// <summary>
    /// Marks the current node as occupied and reserved.
    /// Should be called once after unit is spawned to block its starting tile.
    /// </summary>
    public void OccupyCurrentNode()
    {
        if (_grid == null) return;

        _currentNode = _grid.GetNodeFromWorldPosition(transform.position);
        if (_currentNode == null) return;

        _grid.ReserveNode(_currentNode);    // Prevent other units from reserving this cell
    }

    /// <summary>
    /// Pause the unit's movement (called by combat, for example during attack).
    /// </summary>
    public void Pause() { _isPaused = true; }

    /// <summary>
    /// Resume the unit's movement if it was paused.
    /// </summary>
    public void Resume() { _isPaused = false; }

    // ======= Two-phase Reservation System =======
    /// <summary>
    /// Plan and reserve a grid node for destination before movement starts.
    /// Prevents multiple units from targeting the same cell.
    /// </summary>
    public void PlanAndReserveDestination(GridNode node)
    {
        if (node == null || _grid == null) return;

        // Skip if the node is reserved by another unit (but not if we already have a reservation)
        if (_grid.IsNodeReserved(node) && _reservedDest != node)
            return;

        // Release any previous reservation if it exists
        if (_reservedDest != null && _reservedDest != node)
            _grid.UnreserveNode(_reservedDest);

        // Reserve the new node
        _grid.ReserveNode(node);
        _reservedDest = node;
    }

    // ======= Movement Start/Pathfinding =======
    /// <summary>
    /// Begins movement toward the target node.
    /// If pathfinding fails, restores grid to a valid state and returns unit to Idle.
    /// Ensures cells are properly reserved and freed to prevent overlap or deadlocks.
    /// </summary>
    public void MoveTo(GridNode targetNode)
    {
        if (_grid == null || _pathfinder == null || targetNode == null) return;

        _currentNode = null;

        // Bail out if target is already reserved by another unit
        if (_grid.IsNodeReserved(targetNode) && _reservedDest != targetNode)
            return;

        bool reservedByMe = (_reservedDest == targetNode);
        if (reservedByMe)
            _grid.UnreserveNode(targetNode);          // Temporarily free for pathfinding

        // Free the current tile so A* can path through it
        GridNode start = _grid.GetNodeFromWorldPosition(transform.position);
        int sx = Mathf.RoundToInt(start.worldPosition.x / _grid.GridSettings.NodeSize);
        int sy = Mathf.RoundToInt(start.worldPosition.z / _grid.GridSettings.NodeSize);
        _grid.SetWalkable(sx, sy, true);
        _grid.UnreserveNode(start);

        // Find a path using the A* algorithm
        _path = _pathfinder.FindPathWithNodes(start, targetNode, _unit.Width, _unit.Height);

        // If pathfinding fails, roll back reservations and revert to Idle state
        if (_path == null || _path.Count == 0)
        {
            if (reservedByMe) _grid.ReserveNode(targetNode); // Restore previous reservation

            // Re-block the tile we are currently standing on
            _grid.SetWalkable(sx, sy, false);
            _grid.ReserveNode(start);
            _currentNode = start;

            _path = null;
            _unit.InternalChangeState(UnitState.Idle);
            return;
        }

        // Path found: reserve the destination and begin movement
        _grid.ReserveNode(targetNode);
        _reservedDest = targetNode;

        _nextIdx = 0;
        _unit.InternalChangeState(UnitState.Moving);
    }

    // ======= Movement Update Loop =======
    /// <summary>
    /// Runs every frame: if unit is moving and not paused, continue stepping along the path.
    /// </summary>
    private void Update()
    {
        if (_unit.CurrentState == UnitState.Moving && !_isPaused)
            HandleMovement();
    }

    /// <summary>
    /// Handles rotation, movement, and path progression.
    /// Moves unit along the path node by node until destination is reached.
    /// </summary>
    private void HandleMovement()
    {
        if (_path == null || _nextIdx >= _path.Count) return;

        Vector2Int c = _path[_nextIdx];
        GridNode node = _grid.GetNode(c.x, c.y);
        Vector3 dest = node.worldPosition + Vector3.up * 0.1f; // Small vertical offset for visuals

        // Smoothly rotate unit toward the next destination node
        Vector3 dir = dest - transform.position; dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, look, _rotationSpeed * Time.deltaTime);
        }

        // Move unit toward the next node
        transform.position = Vector3.MoveTowards(
            transform.position, dest, _moveSpeed * Time.deltaTime);

        // If unit reached the next waypoint, progress to the following one
        if (Vector3.Distance(transform.position, dest) < 0.05f)
            _nextIdx++;

        // If destination reached, finish movement and update grid
        if (_nextIdx >= _path.Count)
            FinishMovement();
    }

    /// <summary>
    /// Called when path is complete. Claims the destination, updates occupancy, and sets Idle state.
    /// </summary>
    private void FinishMovement()
    {
        if (_reservedDest != null)
        {
            _grid.UnreserveNode(_reservedDest);

            Vector3 wp = _reservedDest.worldPosition;
            int tx = Mathf.RoundToInt(wp.x / _grid.GridSettings.NodeSize);
            int ty = Mathf.RoundToInt(wp.z / _grid.GridSettings.NodeSize);
            _grid.SetWalkable(tx, ty, false);
            _currentNode = _grid.GetNode(tx, ty); // Remember current node for later
            _unit.RefreshSpatialHashEntry();
            _reservedDest = null;
        }

        _path = null;
        _unit.InternalChangeState(UnitState.Idle);
    }


    /// <summary>
    /// Cancels any in-progress movement, frees reservations,
    /// reclaims the tile the unit currently occupies, and returns to Idle.
    /// Call this the moment combat starts while the unit is on the move.
    /// </summary>
    public void CancelMovement()
    {
        if (_grid == null) return;                         

        /* 1. Release reserved destination (if any) */
        if (_reservedDest != null)
        {
            _grid.UnreserveNode(_reservedDest);            
            _reservedDest = null;
        }

        /* 2. Clear current path data */
        _path?.Clear();                                   
        _path = null;
        _nextIdx = 0;

        /* 3. Occupy the grid node under the unit’s feet */
        GridNode node = _grid.GetNodeFromWorldPosition(transform.position);
        if (node != null)
        {
            float size = _grid.GridSettings.NodeSize;
            int tx = Mathf.RoundToInt(node.worldPosition.x / size);
            int ty = Mathf.RoundToInt(node.worldPosition.z / size);
            _grid.SetWalkable(tx, ty, false);             
            _currentNode = node;
        }

        /* 4. Switch state back to Idle */
        _unit.InternalChangeState(UnitState.Idle);
    }

    // ======= Grid Clean-up =======
    /// <summary>
    /// Frees any cells currently occupied or reserved by this unit.
    /// Releases both current cell and any planned destination.
    /// Ensures no "ghost" blocks remain if the unit is destroyed or interrupted.
    /// </summary>
    public void ReleaseOccupiedNode()
    {
        if (_grid == null) return;

        float size = _grid.GridSettings.NodeSize;
        bool useXZ = _grid.GridSettings.UseXZPlane;

        // 1) Clear the tile(s) the unit is currently standing on (supports multi-cell units)
        if (_currentNode != null)
        {
            int baseX = Mathf.RoundToInt(_currentNode.worldPosition.x / size);
            int baseY = Mathf.RoundToInt(
                useXZ ? _currentNode.worldPosition.z / size
                      : _currentNode.worldPosition.y / size);

            for (int dx = 0; dx < _unit.Width; dx++)
                for (int dy = 0; dy < _unit.Height; dy++)
                {
                    int gx = baseX + dx;
                    int gy = baseY + dy;

                    _grid.SetWalkable(gx, gy, true);
                    _grid.UnreserveNode(_grid.GetNode(gx, gy));
                }
            _currentNode = null;
        }

        // 2) Clear any reserved destination the unit hasn't reached
        if (_reservedDest != null)
        {
            _grid.UnreserveNode(_reservedDest);
            _reservedDest = null;
        }
    }

    /// <summary>
    /// Called when the unit is disabled (e.g. destroyed, killed).
    /// Ensures all reservations are properly released to prevent blocking.
    /// </summary>
    private void OnDisable()
    {
        if (_reservedDest != null && _grid != null)
            _grid.UnreserveNode(_reservedDest);
        ReleaseOccupiedNode();
    }

    // ======= Debug Path Gizmos =======
    /// <summary>
    /// Draws the planned path as lines and highlights the destination node for debugging.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!ShowPathGizmos || _grid == null) return;

        var path = _path;

        if (path == null || path.Count == 0) return;

        int last = path.Count - 1;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < last; i++)
        {
            Vector3 a = _grid.GetNode(path[i].x, path[i].y).worldPosition + Vector3.up * 0.1f;
            Vector3 b = _grid.GetNode(path[i + 1].x, path[i + 1].y).worldPosition + Vector3.up * 0.1f;
            Gizmos.DrawLine(a, b);
        }

        // Draw a cube at the final destination
        Gizmos.color = Color.green;
        Vector3 endPos = _grid.GetNode(path[last].x, path[last].y).worldPosition + Vector3.up * 0.1f;
        Gizmos.DrawCube(endPos, Vector3.one * (_grid.GridSettings.NodeSize * 0.8f));
    }
}
