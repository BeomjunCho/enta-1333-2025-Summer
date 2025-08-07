// EnemyWaveSpawner.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Directions from which waves can approach.
/// </summary>
public enum SpawnDirection
{
    North,
    South,
    East,
    West,
    Northeast,
    Northwest,
    Southeast,
    Southwest
}

/// <summary>
/// Inspector-friendly wave definition that bundles ArmyType and SpawnDirection.
/// </summary>
[Serializable]
public struct WaveSetting
{
    public ArmyType waveType;
    public SpawnDirection direction;
}

/// <summary>
/// Spawns configured enemy waves and orders units toward the grid center.
/// Supports manual triggers and automated looping on a fixed interval.
/// </summary>
public class EnemyWaveSpawner : MonoBehaviour
{
    /* ------------------------------------------------------------------ */
    /*  Inspector                                                         */
    /* ------------------------------------------------------------------ */

    [Header("References")]
    [SerializeField] private ArmyManager _armyManager = null;
    [SerializeField] private GridManager _gridManager = null;
    [SerializeField] private UIManager _uiManager = null;

    [Header("Wave Settings (max 5)")]
    [SerializeField] private WaveSetting _wave1;
    [SerializeField] private WaveSetting _wave2;
    [SerializeField] private WaveSetting _wave3;
    [SerializeField] private WaveSetting _wave4;
    [SerializeField] private WaveSetting _wave5;

    [Header("Spawn Settings")]
    [Tooltip("Seconds between individual unit spawns within a wave.")]
    [SerializeField] private float _unitSpawnDelay = 0.25f;

    [Header("Auto Waves")]
    [Tooltip("Seconds between waves when auto-running. First wave spawns immediately.")]
    [SerializeField] private float _autoWaveInterval = 10f;

    [Header("Edge Offset")]
    [Tooltip("How many grid cells outside the edge to place the initial spawn point (visual padding).")]
    [SerializeField] private int _edgeOffset = 1;

    /* ------------------------------------------------------------------ */
    /*  Wave State & Events                                               */
    /* ------------------------------------------------------------------ */

    public bool IsOnFinalWave => _currentWaveIdx == _waves.Count - 1;
    public int CurrentWave => _currentWaveIdx + 1;
    public int RemainingWaves => Mathf.Max(0, _waves.Count - CurrentWave);
    public float TimeToNextWave { get; private set; }

    public event Action OnWaveChanged;
    public event Action<float> OnCountdownUpdated;

    /* ------------------------------------------------------------------ */
    /*  Runtime                                                           */
    /* ------------------------------------------------------------------ */

    private readonly List<WaveSetting> _waves = new();
    private int _currentWaveIdx = -1;
    private Coroutine _autoRoutine;
    private Coroutine _countdownRoutine;
    private bool _autoRunning;

    /* ============================== Unity ============================= */

    private void Awake()
    {
        if (_armyManager == null || _gridManager == null)
        {
            Debug.LogError("[EnemyWaveSpawner] Missing references – disabled.");
            enabled = false;
            return;
        }

        // Build internal wave list (ignore entries with waveType == None)
        _waves.AddRange(new[] { _wave1, _wave2, _wave3, _wave4, _wave5 });
        _waves.RemoveAll(w => w.waveType == default);
    }

    private void Update()
    {
        // Manual debug triggers (1-5)
        if (Input.GetKeyDown(KeyCode.Alpha1)) StartWaveByIndex(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) StartWaveByIndex(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) StartWaveByIndex(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) StartWaveByIndex(3);
        if (Input.GetKeyDown(KeyCode.Alpha5)) StartWaveByIndex(4);
    }

    private void OnDisable()
    {
        StopAutoWaves();
    }

    /* ============================ Public API ========================= */

    /// <summary>
    /// Starts auto wave loop; spawns wave #1 after initial delay,
    /// then repeats until all waves are spawned or stopped.
    /// </summary>
    public void StartAutoWaves()
    {
        if (!enabled) return;
        StopAutoWaves();
        _currentWaveIdx = -1;
        _autoRunning = true;
        _autoRoutine = StartCoroutine(AutoWaveLoop());
    }

    /// <summary>Stops the auto wave loop.</summary>
    public void StopAutoWaves()
    {
        _autoRunning = false;

        if (_autoRoutine != null)
        {
            StopCoroutine(_autoRoutine);
            _autoRoutine = null;
        }

        if (_countdownRoutine != null)
        {
            StopCoroutine(_countdownRoutine);
            _countdownRoutine = null;
        }
    }

    /// <summary>
    /// Starts the next configured wave. Returns false when none remain.
    /// </summary>
    public bool StartNextWave()
    {
        _currentWaveIdx++;
        if (_currentWaveIdx >= _waves.Count)
            return false;

        TriggerWaveStarted();
        StartCoroutine(SpawnWaveRoutine(_waves[_currentWaveIdx]));
        return true;
    }

    /// <summary>
    /// Starts a specific wave index (0-based). Returns false if invalid or spawned.
    /// </summary>
    public bool StartWaveByIndex(int index)
    {
        if (index < 0 || index >= _waves.Count || index <= _currentWaveIdx)
            return false;

        _currentWaveIdx = index;
        TriggerWaveStarted();
        StartCoroutine(SpawnWaveRoutine(_waves[_currentWaveIdx]));
        return true;
    }

    /// <summary>
    /// Fully resets spawner state for a fresh run.
    /// </summary>
    public void ResetWaves()
    {
        StopAutoWaves();
        StopAllCoroutines();
        _currentWaveIdx = -1;
        TimeToNextWave = 0f;
    }

    /* ===================== Internal Coroutines ======================= */

    private IEnumerator AutoWaveLoop()
    {
        // Countdown before first wave
        if (_countdownRoutine != null)
            StopCoroutine(_countdownRoutine);
        _countdownRoutine = StartCoroutine(CountdownRoutine(_autoWaveInterval));

        float timer = 0f;
        while (_autoRunning && timer < _autoWaveInterval)
        {
            timer += Time.deltaTime;
            yield return null;
        }
        if (!_autoRunning) yield break;

        // Spawn first wave
        StartNextWave();

        // Remaining waves
        while (_autoRunning && _currentWaveIdx < _waves.Count - 1)
        {
            if (_countdownRoutine != null)
                StopCoroutine(_countdownRoutine);
            _countdownRoutine = StartCoroutine(CountdownRoutine(_autoWaveInterval));

            float wait = 0f;
            while (_autoRunning && wait < _autoWaveInterval)
            {
                wait += Time.deltaTime;
                yield return null;
            }
            if (!_autoRunning) yield break;

            StartNextWave();
        }

        _autoRunning = false;
    }

    private IEnumerator CountdownRoutine(float duration)
    {
        TimeToNextWave = duration;
        OnCountdownUpdated?.Invoke(TimeToNextWave);

        while (TimeToNextWave > 0f && _autoRunning)
        {
            TimeToNextWave -= Time.deltaTime;
            if (TimeToNextWave < 0f) TimeToNextWave = 0f;
            OnCountdownUpdated?.Invoke(TimeToNextWave);
            yield return null;
        }
    }

    /// <summary>
    /// Spawns one wave and orders units to march toward the center.
    /// </summary>
    private IEnumerator SpawnWaveRoutine(WaveSetting setting)
    {
        int waveNum = _currentWaveIdx + 1;
        _uiManager?.ShowWavePopup(waveNum, setting.direction);

        // Calculate random spawn position based on direction
        Vector3 spawnPos = GetRandomSpawnPosition(setting.direction);

        var spawned = new List<UnitBase>();

        yield return StartCoroutine(
            _armyManager.SpawnArmyAndCollect(
                setting.waveType,
                Team.Enemy,
                spawnPos,
                _unitSpawnDelay,
                spawned));

        // Compute center node and assign destinations
        Vector2Int centerIdx = new(
            _gridManager.GridSettings.GridSizeX / 2,
            _gridManager.GridSettings.GridSizeY / 2);
        GridNode centerNode = _gridManager.GetNode(centerIdx.x, centerIdx.y);

        List<GridNode> targets = _gridManager.FindNearestFreeNodes(
            centerNode,
            spawned.Count);

        for (int i = 0; i < spawned.Count; i++)
        {
            UnitBase u = spawned[i];
            GridNode dest = i < targets.Count ? targets[i] : centerNode;

            u.SetReservedDestination(dest);
            u.MoveTo(dest);
        }

        Debug.Log($"[EnemyWaveSpawner] Wave #{waveNum} – {spawned.Count} units spawned from {setting.direction}.");
    }

    /* ============================= Helpers =========================== */

    /// <summary>
    /// Returns a random world position on the edge of the grid according to the direction.
    /// </summary>
    private Vector3 GetRandomSpawnPosition(SpawnDirection dir)
    {
        int width = _gridManager.GridSettings.GridSizeX;
        int height = _gridManager.GridSettings.GridSizeY;

        // Choose grid indices on the edge
        int x = 0, y = 0;

        switch (dir)
        {
            case SpawnDirection.North:
                x = UnityEngine.Random.Range(0, width);
                y = height - 1 + _edgeOffset;
                break;
            case SpawnDirection.South:
                x = UnityEngine.Random.Range(0, width);
                y = -_edgeOffset;
                break;
            case SpawnDirection.East:
                x = width - 1 + _edgeOffset;
                y = UnityEngine.Random.Range(0, height);
                break;
            case SpawnDirection.West:
                x = -_edgeOffset;
                y = UnityEngine.Random.Range(0, height);
                break;
            case SpawnDirection.Northeast:
                x = width - 1 + _edgeOffset;
                y = height - 1 + _edgeOffset;
                break;
            case SpawnDirection.Northwest:
                x = -_edgeOffset;
                y = height - 1 + _edgeOffset;
                break;
            case SpawnDirection.Southeast:
                x = width - 1 + _edgeOffset;
                y = -_edgeOffset;
                break;
            case SpawnDirection.Southwest:
                x = -_edgeOffset;
                y = -_edgeOffset;
                break;
        }

        // Clamp indices that fall inside grid so GetNodeWorldPosition works
        int cx = Mathf.Clamp(x, 0, width - 1);
        int cy = Mathf.Clamp(y, 0, height - 1);

        Vector3 world = _gridManager.GetNodeWorldPosition(cx, cy);

        // Apply offset along the outward normal so units spawn just outside the map
        Vector3 offset = Vector3.zero;
        if (x < 0) offset += Vector3.left * _gridManager.GridSettings.NodeSize;
        if (x >= width) offset += Vector3.right * _gridManager.GridSettings.NodeSize;
        if (y < 0) offset += Vector3.back * _gridManager.GridSettings.NodeSize;
        if (y >= height) offset += Vector3.forward * _gridManager.GridSettings.NodeSize;

        return world + offset;
    }

    /// <summary>
    /// Triggers wave-changed event and resets countdown display.
    /// </summary>
    private void TriggerWaveStarted()
    {
        OnWaveChanged?.Invoke();

        if (_autoRunning && !IsOnFinalWave)
        {
            TimeToNextWave = _autoWaveInterval;
            OnCountdownUpdated?.Invoke(TimeToNextWave);
        }
        else
        {
            TimeToNextWave = 0f;
            OnCountdownUpdated?.Invoke(TimeToNextWave);
        }
    }
}
