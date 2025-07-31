using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight spatial hash for fast proximity queries of IDamageable resources.
/// Mirrors the logic of your existing SpatialHash but stores IDamageable instead of UnitBase.
/// </summary>
public class ResourceSpatialHash
{
    private readonly float _cellSize;
    private readonly Dictionary<Vector2Int, List<IDamageable>> _buckets = new();

    /// <summary>
    /// Create a new resource spatial hash with the given cell size.
    /// </summary>
    public ResourceSpatialHash(float cellSize)
    {
        _cellSize = cellSize;
    }

    /// <summary>
    /// Compute the bucket key for a world position.
    /// </summary>
    private Vector2Int GetKey(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt(worldPos.x / _cellSize);
        int y = Mathf.FloorToInt(worldPos.z / _cellSize);
        return new Vector2Int(x, y);
    }

    /// <summary>
    /// Add a resource to the hash at its current position.
    /// </summary>
    public void Add(IDamageable resource)
    {
        var key = GetKey(resource.Tr.position);
        if (!_buckets.TryGetValue(key, out var list))
        {
            list = new List<IDamageable>();
            _buckets[key] = list;
        }
        list.Add(resource);
    }

    /// <summary>
    /// Remove a resource from the hash (using its last known position).
    /// </summary>
    public void Remove(IDamageable resource)
    {
        var key = GetKey(resource.Tr.position);
        if (_buckets.TryGetValue(key, out var list))
        {
            list.Remove(resource);
            if (list.Count == 0)
                _buckets.Remove(key);
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
    /// Enumerate all resources in buckets that cover at least the given range around pos.
    /// Distance check must be done by the caller.
    /// </summary>
    public IEnumerable<IDamageable> Query(Vector3 pos, float range)
    {
        int r = Mathf.CeilToInt(range / _cellSize);
        var center = GetKey(pos);

        for (int dy = -r; dy <= r; dy++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                var key = new Vector2Int(center.x + dx, center.y + dy);
                if (_buckets.TryGetValue(key, out var list))
                {
                    foreach (var res in list)
                        yield return res;
                }
            }
        }
    }
}
