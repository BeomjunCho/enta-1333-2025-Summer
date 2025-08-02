using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Lightweight spatial hash for fast neighbor queries in a fixed-size grid.
/// Maps world positions to 2D grid buckets for O(1) add/remove/query.
/// </summary>
public class SpatialHash
{
    private readonly float _cellSize;
    private readonly Dictionary<Vector2Int, List<UnitBase>> _buckets = new();

#if UNITY_EDITOR
    private const float _printInterval = 5f;
    private double _lastPrintTime;
#endif

    /// <summary>
    /// Creates a spatial hash with the given cell size (world units).
    /// </summary>
    public SpatialHash(float cellSize)
    {
        _cellSize = cellSize;

#if UNITY_EDITOR
        if (EditorApplication.isPlaying)
        {
            _lastPrintTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
        }
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
    }

#if UNITY_EDITOR
    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            _lastPrintTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= OnEditorUpdate;
        }
    }

    private void OnEditorUpdate()
    {
        if (!EditorApplication.isPlaying) return;
        if (EditorApplication.timeSinceStartup - _lastPrintTime < _printInterval) return;
        _lastPrintTime = EditorApplication.timeSinceStartup;

        int bucketCount = _buckets.Count;
        int unitTotal = 0;
        foreach (var list in _buckets.Values) unitTotal += list.Count;
        float avg = bucketCount > 0 ? (float)unitTotal / bucketCount : 0f;
        Debug.Log($"[SpatialHash] Buckets:{bucketCount} Units:{unitTotal} Avg/Bucket:{avg:F2}");
    }
#endif

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    /// <summary>Returns the hash key for a world position.</summary>
    public Vector2Int GetHashFast(Vector3 worldPos) =>
        new(Mathf.FloorToInt(worldPos.x / _cellSize),
            Mathf.FloorToInt(worldPos.z / _cellSize));

    /// <summary>Adds a unit to its current bucket.</summary>
    public void Add(UnitBase u) => AddInternal(u, GetHashFast(u.transform.position));

    /// <summary>Removes a unit from its current bucket.</summary>
    public void Remove(UnitBase u) => Remove(u, GetHashFast(u.transform.position));

    /// <summary>Removes a unit from a specific bucket key.</summary>
    public void Remove(UnitBase u, Vector2Int key)
    {
        if (_buckets.TryGetValue(key, out var list))
        {
            list.Remove(u);
            if (list.Count == 0) _buckets.Remove(key);
        }
    }

    /// <summary>Clears every entry.</summary>
    public void Clear()
    {
        foreach (var l in _buckets.Values) l.Clear();
        _buckets.Clear();
    }

    /// <summary>
    /// Updates a unit's bucket when it moves.
    /// Pass the **old** position *before* the Transform moved.
    /// </summary>
    /// <param name="u">Unit that moved.</param>
    /// <param name="oldPos">Previous world position.</param>
    public void Update(UnitBase u, Vector3 oldPos)
    {
        Vector2Int from = GetHashFast(oldPos);
        Vector2Int to = GetHashFast(u.transform.position);
        if (from == to) return;          // Same bucket, nothing to do
        Remove(u, from);
        AddInternal(u, to);
    }

    /// <summary>
    /// Returns units in buckets covering the given range (square region).
    /// </summary>
    public IEnumerable<UnitBase> Query(Vector3 pos, float range)
    {
        int r = Mathf.CeilToInt(range / _cellSize);
        Vector2Int center = GetHashFast(pos);

        for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
                if (_buckets.TryGetValue(new Vector2Int(center.x + x, center.y + y), out var list))
                    foreach (var u in list) yield return u;
    }

    // ------------------------------------------------------------------
    // Internal helpers
    // ------------------------------------------------------------------
    private void AddInternal(UnitBase u, Vector2Int key)
    {
        if (!_buckets.TryGetValue(key, out var list))
        {
            list = new List<UnitBase>(4);
            _buckets[key] = list;
        }
        if (!list.Contains(u))
            list.Add(u);
    }
}
