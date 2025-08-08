using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using static SfxPlayerPool;

/// <summary>
/// Builds lake clusters from <see cref="GridManager"/> water tiles, spawns one
/// FMOD Event instance per lake, and constrains each instance to its own lake.  
/// ▸ When the player camera is over a lake, the lake’s anchor slides directly
///   beneath the camera (Y = 0).  
/// ▸ When the camera leaves, the anchor remains at the last tile inside that
///   lake, so the audio continues to emanate from the water.  
/// ▸ Multiple lakes can play simultaneously—each with its own FMOD instance.  
/// The “HowFar” parameter equals the distance from the camera to the lake’s
/// anchor (clamped 0-70).
/// </summary>
public class LakeAudioManager : MonoBehaviour
{
    /* ------------------------------------------------------------------ */
    /*  Nested types                                                      */
    /* ------------------------------------------------------------------ */

    /// <summary>Runtime data container for one lake cluster.</summary>
    private sealed class LakeAudio
    {
        public Transform anchor;                 // Runtime 3D emitter
        public SfxHandle handle;                 // FMOD instance handle
        public readonly List<Vector3> tiles = new();
    }

    /* ------------------------------------------------------------------ */
    /*  Inspector                                                         */
    /* ------------------------------------------------------------------ */

    [Header("Dependencies")]
    [SerializeField] private GridManager _gridManager = null;

    [Header("Terrain")]
    [Tooltip("TerrainType asset that represents Water tiles.")]
    [SerializeField] private TerrainType _waterTerrain = null;

    [Header("FMOD")]
    [SerializeField] private string _howFarParam = "HowFar";
    [SerializeField] private float _maxDistance = 70f;

    /* ------------------------------------------------------------------ */
    /*  Internal                                                          */
    /* ------------------------------------------------------------------ */

    private readonly Dictionary<int, LakeAudio> _lakes = new();
    private int[,] _lakeIdMap;
    private int _lakeCount;
    private bool _initialized;

    /* =========================== Unity ================================= */

    /// <summary>
    /// Updates anchors and FMOD parameters each frame.
    /// </summary>
    private void Update()
    {
        if (!_initialized) return;

        Vector3 camPos = Camera.main.transform.position;

        foreach (LakeAudio lake in _lakes.Values)
        {
            if (!lake.handle.Equals(SfxHandle.Invalid))
            {
                // Find nearest tile in this lake.
                Vector3 nearest = lake.tiles[0];
                float bestSqr = (camPos - nearest).sqrMagnitude;

                for (int i = 1; i < lake.tiles.Count; i++)
                {
                    float sqr = (camPos - lake.tiles[i]).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        nearest = lake.tiles[i];
                    }
                }

                lake.anchor.position = new Vector3(nearest.x, 0f, nearest.z);

                float dist = Mathf.Clamp(Mathf.Sqrt(bestSqr), 0f, _maxDistance);
                AudioManager.Instance.SetSfxParameter(lake.handle, _howFarParam, dist);
            }
        }
    }

    /// <summary>
    /// Ensures all FMOD instances are stopped when this component disables.
    /// </summary>
    private void OnDisable()
    {
        StopAllLakes();
    }

    /* ------------------------------------------------------------------ */
    /*  Public API                                                         */
    /* ------------------------------------------------------------------ */

    /// <summary>
    /// Builds lake clusters.  
    /// Call this after <see cref="GridManager"/> has finished its setup.
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;

        BuildLakeClusters();
        _initialized = true;
    }

    /// <summary>
    /// Resets the manager to a clean state (e.g., when returning to menu).  
    /// Stops all audio, destroys anchors, and clears cached data.
    /// </summary>
    public void ResetManager()
    {
        StopAllLakes();

        foreach (LakeAudio lake in _lakes.Values)
            if (lake.anchor != null) Destroy(lake.anchor.gameObject);

        _lakes.Clear();
        _lakeIdMap = null;
        _lakeCount = 0;
        _initialized = false;
    }

    /// <summary>
    /// Returns the lake identifier at a world position, or –1 if none.
    /// </summary>
    /// <param name="worldPos">World position.</param>
    /// <returns>lakeId or –1.</returns>
    public int GetLakeId(Vector3 worldPos)
    {
        if (_gridManager.GetNodeFromWorldPosition(worldPos, out int x, out int y))
            return _lakeIdMap[x, y];
        return -1;
    }

    /// <summary>
    /// Ensures the specified lake is playing (lazy-initialised instance).
    /// </summary>
    /// <param name="lakeId">Identifier from <see cref="GetLakeId"/>.</param>
    public void EnsureLakePlaying(int lakeId)
    {
        if (!_initialized || lakeId < 0) return;
        if (!_lakes.TryGetValue(lakeId, out LakeAudio lake)) return;

        if (lake.handle.Equals(SfxHandle.Invalid))
            lake.handle = AudioManager.Instance.PlaySfxAttached(lake.anchor, FMODEvents.Instance.WaterLapping);
    }

    /* ------------------------------------------------------------------ */
    /*  Helpers                                                           */
    /* ------------------------------------------------------------------ */

    /// <summary>
    /// Groups contiguous water tiles into lakes via flood-fill.
    /// </summary>
    private void BuildLakeClusters()
    {
        int w = _gridManager.width;
        int h = _gridManager.height;

        _lakeIdMap = new int[w, h];
        for (int ix = 0; ix < w; ix++)
            for (int iy = 0; iy < h; iy++)
                _lakeIdMap[ix, iy] = -1;

        bool[,] visited = new bool[w, h];
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (visited[x, y]) continue;
                if (_gridManager.GetNodeTerrain(x, y) != _waterTerrain) continue;

                int lakeId = _lakeCount++;
                LakeAudio lake = new LakeAudio
                {
                    anchor = new GameObject($"LakeAnchor_{lakeId}").transform,
                    handle = SfxHandle.Invalid
                };
                _lakes.Add(lakeId, lake);

                Queue<Vector2Int> q = new();
                q.Enqueue(new Vector2Int(x, y));
                visited[x, y] = true;

                while (q.Count > 0)
                {
                    Vector2Int n = q.Dequeue();
                    _lakeIdMap[n.x, n.y] = lakeId;

                    Vector3 wp = _gridManager.GetNodeWorldPosition(n.x, n.y);
                    wp.y = 0f;
                    lake.tiles.Add(wp);

                    foreach (Vector2Int d in dirs)
                    {
                        int nx = n.x + d.x;
                        int ny = n.y + d.y;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        if (visited[nx, ny]) continue;
                        if (_gridManager.GetNodeTerrain(nx, ny) != _waterTerrain) continue;

                        visited[nx, ny] = true;
                        q.Enqueue(new Vector2Int(nx, ny));
                    }
                }

                if (lake.tiles.Count > 0)
                    lake.anchor.position = lake.tiles[0];
            }
        }
    }

    /// <summary>
    /// Stops all active lake audio instances.
    /// </summary>
    private void StopAllLakes()
    {
        foreach (LakeAudio lake in _lakes.Values)
        {
            if (lake == null) continue;

            if (!lake.handle.Equals(SfxHandle.Invalid) && AudioManager.Instance != null)
            {
                AudioManager.Instance.StopSfx(lake.handle, false);
                lake.handle = SfxHandle.Invalid;
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Draw anchors and lake tiles for each lake for debugging in the Scene view.

        if (_lakes == null) return;

        foreach (var pair in _lakes)
        {
            int lakeId = pair.Key;
            var lake = pair.Value;
            if (lake == null) continue;

            // Draw anchor as a yellow sphere
            if (lake.anchor != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(lake.anchor.position, 0.5f);

                // Draw lakeId as a label above the anchor
#if UNITY_EDITOR
                UnityEditor.Handles.color = Color.yellow;
                UnityEditor.Handles.Label(lake.anchor.position + Vector3.up * 1.0f, $"Lake {lakeId}");
#endif
            }

            // Draw all lake tiles as cyan spheres
            if (lake.tiles != null)
            {
                Gizmos.color = Color.cyan;
                foreach (var pos in lake.tiles)
                {
                    Gizmos.DrawSphere(pos + Vector3.up * 0.05f, 0.2f);
                }
            }
        }
    }
#endif
}
