#if UNITY_EDITOR
using UnityEditor;                            // add at top of file
#endif
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Creates and manages a grid of GridNode objects.
/// If a map texture is supplied, each pixel colour maps to a TerrainType,
/// otherwise the grid is filled randomly (legacy behaviour).
/// </summary>
public class GridManager : MonoBehaviour
{
    // =========================== Inspector Fields ==============================

    [Header("Grid Settings")]
    [SerializeField] private GridSettings _gridSettings = null; // Grid configuration asset

    [Header("Environment")]
    [SerializeField] private EnvironmentSpawner _spawner = null; // Environment spawner

    [Header("Terrain Types (index-matching)")]
    [Tooltip("0 Grass, 1 Sand, 2 Water, 3 Road, 4 Forest, 5 Rock, 6 Lava")]
    [SerializeField] private TerrainType[] _terrainTypes = null; // List of all possible terrain types

    [Header("Optional map texture (painted in Aseprite)")]
    [SerializeField] private Texture2D _mapTexture = null; // Optional map image for pixel-to-terrain mapping

    [Tooltip("Exact colours in the same order as _terrainTypes")]
    [SerializeField] private List<Color32> _colorTable = new();   // Colour mapping table, order matches _terrainTypes

    [Header("Visual Prefabs")]
    [Tooltip("One prefab per TerrainType index (0-6). Size must be 1? unit.")]
    [SerializeField] private GameObject[] _terrainPrefabs = null; // Prefab for rendering terrain tiles

    [Header("Visual Prefabs")]
    [Tooltip("One prefab per TerrainType index (0-6). Size must be 1? unit.")]
    [SerializeField] private GameObject[] _simpleTerrainPrefabs = null; // Alternative simple visuals

    [Tooltip("Optional parent to keep the hierarchy tidy.")]
    [SerializeField] private Transform _visualRoot = null; // Parent for organizing tile visuals in hierarchy

    [Header("Lake Audio Manager")]
    [SerializeField] private LakeAudioManager _lakeAudioManager;

    // =========================== Debug/Config Flags ============================

    public bool UseGridMap = true; // If true, uses map texture for grid creation
    public bool UseSimpleTexture = false; // If true, uses alternative visuals

    /// <summary>
    /// Exposes grid-wide settings (node size, grid size, plane).
    /// </summary>
    public GridSettings GridSettings => _gridSettings;

    // =========================== Runtime Fields ================================

    private GridNode[,] _gridNodes; // 2D array of all grid nodes
    private readonly HashSet<GridNode> _reservedNodes = new(); // Nodes reserved by units or buildings
    private bool _showGizmos = false; // If true, shows grid debug visualization
    public bool isInitialized { get; private set; } // If true, grid is ready to use

    // =========================== Unity Lifecycle ===============================

    private void Awake()
    {
#if UNITY_EDITOR
        ValidateTextureSRGB();
#endif
        // Defer actual grid construction to InitializeGrid()
    }

#if UNITY_EDITOR
    /// <summary>
    /// Warns in editor if the terrain texture is still in sRGB mode.
    /// </summary>
    private void ValidateTextureSRGB()
    {
        if (_mapTexture == null) return;

        string path = AssetDatabase.GetAssetPath(_mapTexture);
        if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.sRGBTexture)
        {
            Debug.LogWarning(
                $"[GridManager] Map texture \"{_mapTexture.name}\" has “sRGB (Color Texture)?enabled. " +
                "Disable it in the Inspector for exact colour matching.");
        }
    }
#endif


    private void Update()
    {
        // Toggle debug gizmos when pressing X key
        if (Input.GetKeyDown(KeyCode.X)) _showGizmos = !_showGizmos;
    }

    // =========================== Grid Creation ================================

    /// <summary>
    /// Builds the grid, spawns terrain visuals and marks the manager as initialized.
    /// Safe to call multiple times; subsequent calls are ignored.
    /// </summary>
    public void InitializeGrid()
    {
        if (isInitialized) return;

        PopulateDefaultColors();
        BuildGridNodes();

        if (UseGridMap)
        {
            if (UseSimpleTexture)
            {
                SpawnSimpleTerrainVisuals();
            }
            else
            {
                SpawnTerrainVisuals();
            }
        }

        isInitialized = true;

        _spawner.InitializeEnvironment();
        _lakeAudioManager.Initialize();
    }

    /// <summary>
    /// Chooses the correct generation path (texture vs. random) and
    /// fills the _gridNodes array accordingly.
    /// </summary>
    private void BuildGridNodes()
    {
        // Using map texture?
        if (UseGridMap && _mapTexture != null)
        {
            InitializeGridFromTexture();   
        }
        else
        {
            InitializeRandomGrid();        
        }
    }

    /// <summary>
    /// Creates the grid by reading pixel data from _mapTexture.
    /// Each pixel color is mapped to a terrain type via _colorTable.
    /// </summary>
    private void InitializeGridFromTexture()
    {
        int sizeX = _gridSettings.GridSizeX;
        int sizeY = _gridSettings.GridSizeY;
        _gridNodes = new GridNode[sizeX, sizeY];

        Color32[] pixels = _mapTexture.GetPixels32();
        int texWidth = _mapTexture.width;
        int texHeight = _mapTexture.height;

        // helper that ignores alpha and allows ? RGB difference
        bool Matches(Color32 a, Color32 b) =>
            Mathf.Abs(a.r - b.r) <= 1 &&
            Mathf.Abs(a.g - b.g) <= 1 &&
            Mathf.Abs(a.b - b.b) <= 1;

        for (int y = 0; y < sizeY; y++)
        {
            for (int x = 0; x < sizeX; x++)
            {
                int px = Mathf.Clamp(x, 0, texWidth - 1);
                int py = Mathf.Clamp(y, 0, texHeight - 1);
                Color32 pix = pixels[py * texWidth + px];

                // find the matching colour in the table
                int id = 0;                                   // default to grass
                for (int i = 0; i < _colorTable.Count; i++)
                {
                    if (Matches(pix, _colorTable[i]))
                    {
                        id = i;
                        break;
                    }
                }

                TerrainType terrain = _terrainTypes[
                    Mathf.Clamp(id, 0, _terrainTypes.Length - 1)];

                Vector3 world = _gridSettings.UseXZPlane
                    ? new Vector3(x, 0f, y) * _gridSettings.NodeSize
                    : new Vector3(x, y, 0f) * _gridSettings.NodeSize;

                _gridNodes[x, y] = new GridNode
                {
                    name = $"{terrain.TerrainName}_{x}_{y}",
                    worldPosition = world,
                    terrainType = terrain,
                    walkable = terrain.Walkable,
                    weight = terrain.MovementCost
                };
            }
        }
        isInitialized = true;
    }

    /// <summary>
    /// Fills the color table with default colors for each terrain type.
    /// </summary>
    private void PopulateDefaultColors()
    {
        _colorTable = new List<Color32>
        {
        new Color32(0x4C, 0xAF, 0x50, 0xFF), // Grass
        new Color32(0xE4, 0xC0, 0x7A, 0xFF), // Sand
        new Color32(0x29, 0x62, 0xFF, 0xFF), // Water
        new Color32(0x6D, 0x4C, 0x41, 0xFF), // Road
        new Color32(0x3E, 0x6B, 0x2F, 0xFF), // Forest
        new Color32(0x8B, 0x8B, 0x8B, 0xFF), // Rock
        new Color32(0xFF, 0x57, 0x22, 0xFF)  // Lava
        };
    }

    /// <summary>
    /// Instantiates terrain visual prefabs for each tile using _terrainPrefabs.
    /// </summary>
    private void SpawnTerrainVisuals()
    {
        if (_terrainPrefabs == null || _terrainPrefabs.Length == 0) return;
        if (!isInitialized) return;

        // clear old visuals
        if (_visualRoot != null)
            foreach (Transform c in _visualRoot) Destroy(c.gameObject);

        float half = _gridSettings.NodeSize * 0.5f; // lift cube so it sits on ground

        int sizeX = _gridSettings.GridSizeX;
        int sizeY = _gridSettings.GridSizeY;

        for (int y = 0; y < sizeY; y++)
            for (int x = 0; x < sizeX; x++)
            {
                TerrainType terrain = _gridNodes[x, y].terrainType;

                // find index of this terrain in the _terrainTypes array
                int id = System.Array.IndexOf(_terrainTypes, terrain);
                if (id < 0 || id >= _terrainPrefabs.Length) continue;

                GameObject prefab = _terrainPrefabs[id];
                if (prefab == null) continue;

                Vector3 pos = _gridNodes[x, y].worldPosition + Vector3.down * 0.5f;
                Instantiate(prefab, pos, Quaternion.identity, _visualRoot);
            }
    }

    /// <summary>
    /// Instantiates alternative simple visuals using _simpleTerrainPrefabs.
    /// </summary>
    private void SpawnSimpleTerrainVisuals()
    {
        if (_simpleTerrainPrefabs == null || _simpleTerrainPrefabs.Length == 0) return;
        if (!isInitialized) return;

        // clear old visuals
        if (_visualRoot != null)
            foreach (Transform c in _visualRoot) Destroy(c.gameObject);

        float half = _gridSettings.NodeSize * 0.5f; // lift cube so it sits on ground

        int sizeX = _gridSettings.GridSizeX;
        int sizeY = _gridSettings.GridSizeY;

        for (int y = 0; y < sizeY; y++)
            for (int x = 0; x < sizeX; x++)
            {
                TerrainType terrain = _gridNodes[x, y].terrainType;

                // find index of this terrain in the _terrainTypes array
                int id = System.Array.IndexOf(_terrainTypes, terrain);
                if (id < 0 || id >= _simpleTerrainPrefabs.Length) continue;

                GameObject prefab = _simpleTerrainPrefabs[id];
                if (prefab == null) continue;

                Vector3 pos = _gridNodes[x, y].worldPosition + Vector3.down;
                Instantiate(prefab, pos, Quaternion.identity, _visualRoot);
            }
    }


    /// <summary>
    /// Fallback method that fills the grid with random walkable terrain types.
    /// Only walkable types are considered.
    /// </summary>
    private void InitializeRandomGrid()
    {
        int sx = _gridSettings.GridSizeX;
        int sy = _gridSettings.GridSizeY;
        _gridNodes = new GridNode[sx, sy];

        // collect walkable terrain types once
        List<TerrainType> walkable = new();
        foreach (var t in _terrainTypes)
            if (t.Walkable) walkable.Add(t);

        for (int x = 0; x < sx; x++)
            for (int y = 0; y < sy; y++)
            {
                TerrainType terrain = walkable[Random.Range(0, walkable.Count)];

                Vector3 world = _gridSettings.UseXZPlane
                    ? new Vector3(x, 0f, y) * _gridSettings.NodeSize
                    : new Vector3(x, y, 0f) * _gridSettings.NodeSize;

                _gridNodes[x, y] = new GridNode
                {
                    name = $"{terrain.TerrainName}_{x}_{y}",
                    worldPosition = world,
                    terrainType = terrain,
                    walkable = true,             // always walkable
                    weight = terrain.MovementCost
                };
            }
    }

    // =========================== Reservation System ============================

    /// <summary>
    /// Marks a node as reserved (for units/buildings, prevents pathing).
    /// </summary>
    public void ReserveNode(GridNode n)
    {
        if (n != null) _reservedNodes.Add(n);
    }
    /// <summary>
    /// Removes a node from the reserved set.
    /// </summary>
    public void UnreserveNode(GridNode n)
    {
        if (n != null) _reservedNodes.Remove(n);
    }
    /// <summary>
    /// Checks if a node is currently reserved.
    /// </summary>
    public bool IsNodeReserved(GridNode n) => _reservedNodes.Contains(n);

    public void ClearAllReservations() => _reservedNodes.Clear();

    // =========================== Node Utilities ================================

    /// <summary>
    /// Gets the node at a specific grid coordinate.
    /// </summary>
    public GridNode GetNode(int x, int y)
    {
        if (!isInitialized) InitializeGrid();
        if (x < 0 || x >= _gridSettings.GridSizeX ||
            y < 0 || y >= _gridSettings.GridSizeY) return null;
        return _gridNodes[x, y];
    }

    /// <summary>
    /// Returns the node closest to a given world-space position.
    /// </summary>
    public GridNode GetNodeFromWorldPosition(Vector3 pos)
    {
        float s = _gridSettings.NodeSize;
        int x = Mathf.RoundToInt(pos.x / s);
        int y = Mathf.RoundToInt(_gridSettings.UseXZPlane ? pos.z / s : pos.y / s);
        return GetNode(Mathf.Clamp(x, 0, _gridSettings.GridSizeX - 1),
                       Mathf.Clamp(y, 0, _gridSettings.GridSizeY - 1));
    }

    /// <summary>
    /// Sets the walkability of the specified node.
    /// </summary>
    public void SetWalkable(int x, int y, bool walk)
    {
        if (!isInitialized) InitializeGrid();
        if (x < 0 || x >= _gridSettings.GridSizeX ||
            y < 0 || y >= _gridSettings.GridSizeY) return;
        _gridNodes[x, y].walkable = walk;
    }

    /// <summary>
    /// Gets all 4 orthogonal neighbor nodes (up, down, left, right).
    /// </summary>
    public IEnumerable<GridNode> GetNeighbors(GridNode node)
    {
        int x = Mathf.RoundToInt(node.worldPosition.x / _gridSettings.NodeSize);
        int y = Mathf.RoundToInt(_gridSettings.UseXZPlane ? node.worldPosition.z / _gridSettings.NodeSize
                                                          : node.worldPosition.y / _gridSettings.NodeSize);

        if (y + 1 < _gridSettings.GridSizeY) yield return GetNode(x, y + 1);
        if (y - 1 >= 0) yield return GetNode(x, y - 1);
        if (x + 1 < _gridSettings.GridSizeX) yield return GetNode(x + 1, y);
        if (x - 1 >= 0) yield return GetNode(x - 1, y);
    }

    /// <summary>
    /// Finds the nearest available (walkable and unreserved) nodes around the center node.
    /// Returns up to the requested count.
    /// </summary>
    public List<GridNode> FindNearestFreeNodes(GridNode center, int count)
    {
        List<GridNode> result = new List<GridNode>();
        HashSet<GridNode> visited = new HashSet<GridNode>();
        Queue<GridNode> q = new Queue<GridNode>();

        q.Enqueue(center);
        visited.Add(center);

        while (q.Count > 0 && result.Count < count)
        {
            GridNode n = q.Dequeue();
            if (n.walkable && !IsNodeReserved(n)) result.Add(n);

            foreach (GridNode nb in GetNeighbors(n))
                if (visited.Add(nb)) q.Enqueue(nb);
        }
        return result;
    }

    /// <summary>
    /// Converts a grid coordinate to a world position (optionally centered).
    /// </summary>
    public Vector3 IdxToWorld(Vector2Int idx, bool center = false)
    {
        float size = _gridSettings.NodeSize;
        float half = center ? size * 0.5f : 0f;

        if (_gridSettings.UseXZPlane)
            return new Vector3(idx.x * size + half,
                               0f,
                               idx.y * size + half);

        // XY plane
        return new Vector3(idx.x * size + half,
                           idx.y * size + half,
                           0f);
    }

    /// <summary>
    /// Overload for direct x, y grid coordinates.
    /// </summary>
    public Vector3 IdxToWorld(int x, int y, bool center = false) =>
        IdxToWorld(new Vector2Int(x, y), center);

    /// <summary>
    /// Clears current grid data and visuals so that InitializeGrid() can run again.
    /// </summary>
    public void ResetGrid()
    {
        _lakeAudioManager.ResetManager();
        // 1) Destroy all environment objects while grid is still valid
        if (_spawner != null)
            _spawner.ResetEnvironment();

        // 2) Destroy all visual tile GameObjects under _visualRoot
        if (_visualRoot != null)
        {
            for (int i = _visualRoot.childCount - 1; i >= 0; i--)
            {
                var child = _visualRoot.GetChild(i).gameObject;
#if UNITY_EDITOR
                DestroyImmediate(child);
#else
            Destroy(child);
#endif
            }
        }

        // 3) Clear reservation state
        _reservedNodes.Clear();

        // 4) Now clear the underlying grid data
        _gridNodes = null;

        // 5) Allow InitializeGrid() to rebuild everything
        isInitialized = false;
    }

    /// Public API for LakeAudioManager ///

    /// <summary>
    /// Total number of columns in the grid (X dimension).
    /// </summary>
    public int width => _gridSettings.GridSizeX;

    /// <summary>
    /// Total number of rows in the grid (Y or Z dimension depending on plane).
    /// </summary>
    public int height => _gridSettings.GridSizeY;

    /// <summary>
    /// Gets the <see cref="TerrainType"/> at a grid coordinate.
    /// </summary>
    /// <param name="x">Grid X index.</param>
    /// <param name="y">Grid Y index.</param>
    /// <returns>The terrain type, or <c>null</c> if out of range.</returns>
    public TerrainType GetNodeTerrain(int x, int y)
    {
        GridNode n = GetNode(x, y);
        return n != null ? n.terrainType : null;
    }

    /// <summary>
    /// Converts a world-space position to grid indices.
    /// </summary>
    /// <param name="pos">World position (XZ or XY plane).</param>
    /// <param name="x">Resulting grid X index.</param>
    /// <param name="y">Resulting grid Y index.</param>
    /// <returns><c>true</c> if the position maps inside the grid bounds.</returns>
    public bool GetNodeFromWorldPosition(Vector3 pos, out int x, out int y)
    {
        float size = _gridSettings.NodeSize;
        x = Mathf.RoundToInt(pos.x / size);
        y = Mathf.RoundToInt(_gridSettings.UseXZPlane ? pos.z / size : pos.y / size);

        bool inRange = x >= 0 && x < _gridSettings.GridSizeX &&
                       y >= 0 && y < _gridSettings.GridSizeY;

        x = Mathf.Clamp(x, 0, _gridSettings.GridSizeX - 1);
        y = Mathf.Clamp(y, 0, _gridSettings.GridSizeY - 1);

        return inRange;
    }

    /// <summary>
    /// Returns the world-space position of a node¡¯s lower-left corner.
    /// </summary>
    /// <param name="x">Grid X index.</param>
    /// <param name="y">Grid Y index.</param>
    /// <returns>World position of that grid cell.</returns>
    public Vector3 GetNodeWorldPosition(int x, int y) =>
        IdxToWorld(x, y, false);

    // =========================== Gizmos (Debug Visualization) ==================
    /// <summary>
    /// Draws colored gizmos in the editor for each node to show walkable/unwalkable cells.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!_showGizmos || !isInitialized || _gridNodes == null) return;

        float size = _gridSettings.NodeSize * 0.9f;
        for (int x = 0; x < _gridSettings.GridSizeX; x++)
            for (int y = 0; y < _gridSettings.GridSizeY; y++)
            {
                GridNode n = _gridNodes[x, y];
                Vector3 center = n.worldPosition;
                if (!n.walkable)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawCube(center, Vector3.one * size);
                }
                else
                {
                    Gizmos.color = n.GizmoColor;
                    Gizmos.DrawWireCube(center, Vector3.one * size);
                }
            }
    }
}
