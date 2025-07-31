using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Lightweight spatial hash for fast neighbor queries in a fixed-size grid.
/// Maps world positions to 2D grid buckets for O(1) add/remove and local area queries.
/// </summary>
public class SpatialHash
{
    // The width and height of each spatial cell (bucket)
    private readonly float _cellSize;
    // The main data structure: maps a grid coordinate (bucket) to a list of units in that cell
    private readonly Dictionary<Vector2Int, List<UnitBase>> _buckets = new();

#if UNITY_EDITOR
    // Editor logging: time interval between stats printouts (seconds)
    private const float _printInterval = 5f;
    // Tracks last time stats were printed in editor play mode
    private double _lastPrintTime;
#endif

    // ------------------------------------------------------------------
    // Constructor: sets cell size and hooks up editor-only logging
    // ------------------------------------------------------------------
    public SpatialHash(float cellSize)
    {
        _cellSize = cellSize;

#if UNITY_EDITOR
        // If already playing in editor, begin logging immediately
        if (EditorApplication.isPlaying)
        {
            _lastPrintTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
        }

        // Subscribe to play mode state changes for automatic log subscription/unsubscription
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
    }

    // ------------------------------------------------------------------
    // Editor-only: Logging helpers and callback subscription
    // ------------------------------------------------------------------
#if UNITY_EDITOR
    /// <summary>
    /// Called when play mode state changes. Adds/removes the log update callback.
    /// </summary>
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

    /// <summary>
    /// Called every editor frame during play mode; prints stats at regular intervals.
    /// </summary>
    private void OnEditorUpdate()
    {
        if (!EditorApplication.isPlaying) return;

        double now = EditorApplication.timeSinceStartup;
        if (now - _lastPrintTime < _printInterval) return;
        _lastPrintTime = now;

        int bucketCount = _buckets.Count;
        int unitTotal = 0;
        foreach (var list in _buckets.Values) unitTotal += list.Count;

        float avg = bucketCount > 0 ? (float)unitTotal / bucketCount : 0f;
        Debug.Log($"[SpatialHash] Buckets: {bucketCount} | Units: {unitTotal} | Avg/Bucket: {avg:F2}");
    }
#endif

    // ------------------------------------------------------------------
    // Public API: Hashing, add/remove, query
    // ------------------------------------------------------------------

    /// <summary>
    /// Computes the hash key (grid cell) for a world position.
    /// Does not access the buckets dictionary.
    /// </summary>
    public Vector2Int GetHashFast(Vector3 worldPos) =>
        new Vector2Int(
            Mathf.FloorToInt(worldPos.x / _cellSize),
            Mathf.FloorToInt(worldPos.z / _cellSize));

    /// <summary>
    /// Adds a unit to the correct bucket based on its world position.
    /// </summary>
    public void Add(UnitBase u)
    {
        Vector2Int h = GetHashFast(u.transform.position);
        if (!_buckets.TryGetValue(h, out var list))
        {
            list = new List<UnitBase>();
            _buckets[h] = list;
        }
        list.Add(u);
    }

    /// <summary>
    /// Removes a unit from the bucket matching its current world position.
    /// Cleans up empty buckets automatically.
    /// </summary>
    public void Remove(UnitBase u)
    {
        Vector2Int h = GetHashFast(u.transform.position);
        if (_buckets.TryGetValue(h, out var list))
        {
            list.Remove(u);
            if (list.Count == 0) _buckets.Remove(h);
        }
    }

    /// <summary>
    /// Removes a unit from a bucket specified by a known key (used for moving units).
    /// </summary>
    public void Remove(UnitBase u, Vector2Int bucketKey)
    {
        if (_buckets.TryGetValue(bucketKey, out var list))
        {
            list.Remove(u);
            if (list.Count == 0) _buckets.Remove(bucketKey);
        }
    }

    /// <summary>
    /// Removes every entry from the hash without changing settings.
    /// </summary>
    public void Clear()
    {
        foreach (var list in _buckets.Values)
            list.Clear();
        _buckets.Clear();
    }

    /// <summary>
    /// Returns all units in buckets within a square area centered on pos, covering at least the given range.
    /// This is a rough filter; individual distance checks must be done separately.
    /// </summary>
    public IEnumerable<UnitBase> Query(Vector3 pos, float range)
    {
        int r = Mathf.CeilToInt(range / _cellSize);
        Vector2Int c = GetHashFast(pos);

        for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
                if (_buckets.TryGetValue(new Vector2Int(c.x + x, c.y + y), out var list))
                    foreach (var u in list) yield return u;
    }
}
