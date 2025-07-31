// EnemyWaveSpawner.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns configured enemy waves (ArmyType) via ArmyManager and commands
/// them to march toward the grid center (player castle).
/// Supports manual triggers (number keys / API) and automated waves at a
/// fixed interval starting with wave #1 when requested by GameManager.
/// Exposes wave state and countdown events for HUD.
/// </summary>
public class EnemyWaveSpawner : MonoBehaviour
{
    /* ------------------------------------------------------------------ */
    /*  Inspector                                                          */
    /* ------------------------------------------------------------------ */
    [Header("References")]
    [SerializeField] private ArmyManager _armyManager = null;    // handles prefab + init
    [SerializeField] private GridManager _gridManager = null;    // path / node queries
    [SerializeField] private UIManager _uiManager;
    [Tooltip("World-space point used as the first search node for free spawn tiles.")]
    [SerializeField] private Transform _spawnOrigin = null;

    [Header("Wave Types")]
    [SerializeField] private ArmyType _wave1 = ArmyType.Wave1;
    [SerializeField] private ArmyType _wave2 = ArmyType.Wave2;
    [SerializeField] private ArmyType _wave3 = ArmyType.Wave3;
    [SerializeField] private ArmyType _wave4 = ArmyType.Wave4;
    [SerializeField] private ArmyType _wave5 = ArmyType.Wave5;

    [Header("Spawn Settings")]
    [Tooltip("Seconds between individual unit spawns within a wave.")]
    [SerializeField] private float _unitSpawnDelay = 0.25f;

    [Header("Auto Waves")]
    [Tooltip("Seconds between waves when auto-running. First wave spawns immediately.")]
    [SerializeField] private float _autoWaveInterval = 10f;

    /* ================================================================== */
    /*  Wave State + Events                                               */
    /* ================================================================== */

    /// <summary>True while currently on the final configured wave.</summary>
    public bool IsOnFinalWave => _currentWaveIdx == _waves.Count - 1;

    /// <summary>Current wave number (1-based). Returns 0 if none spawned yet.</summary>
    public int CurrentWave => _currentWaveIdx + 1;

    /// <summary>How many waves remain after the current one.</summary>
    public int RemainingWaves => Mathf.Max(0, _waves.Count - CurrentWave);

    /// <summary>Seconds remaining until the next wave (auto mode only).</summary>
    public float TimeToNextWave { get; private set; }

    /// <summary>Raised whenever a new wave starts (manual or auto).</summary>
    public event Action OnWaveChanged;

    /// <summary>Raised every frame during countdown to next auto wave.</summary>
    public event Action<float> OnCountdownUpdated;

    /* ------------------------------------------------------------------ */
    /*  Runtime                                                            */
    /* ------------------------------------------------------------------ */
    private readonly List<ArmyType> _waves = new();
    private int _currentWaveIdx = -1;
    private Coroutine _autoRoutine;
    private Coroutine _countdownRoutine;
    private bool _autoRunning;

    /* ================================================================== */
    /*  Unity lifecycle                                                   */
    /* ================================================================== */
    private void Awake()
    {
        if (_armyManager == null || _gridManager == null || _spawnOrigin == null)
        {
            Debug.LogError("[EnemyWaveSpawner] Missing references – disabled.");
            enabled = false;
            return;
        }

        // Build internal wave list (ignore 'None' entries)
        _waves.AddRange(new[] { _wave1, _wave2, _wave3, _wave4, _wave5 });
        _waves.RemoveAll(t => t == default);
    }

    private void Update()
    {
        // Manual debug triggers
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

    /* ================================================================== */
    /*  Public API                                                        */
    /* ================================================================== */

    /// <summary>
    /// Starts auto wave loop: spawns wave #1 immediately, then every interval
    /// until all configured waves have spawned or StopAutoWaves() called.
    /// Safe to call multiple times; restarts the loop.
    /// </summary>
    public void StartAutoWaves()
    {
        if (!enabled) return;
        StopAutoWaves();          // ensure single routine
        _currentWaveIdx = -1;     // reset so wave1 spawns
        _autoRunning = true;
        _autoRoutine = StartCoroutine(AutoWaveLoop());
    }

    /// <summary>Stops the auto wave loop (no further waves).</summary>
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
    /// Starts the next configured wave (returns false if no waves remain).
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
    /// Optionally trigger a specific wave index (0-based).
    /// Returns false if the index is invalid or already spawned.
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
    /// Resets wave spawner state so that waves can start fresh.
    /// Stops auto loop and any in-progress spawn/coundown coroutines, and resets the wave index.
    /// </summary>
    public void ResetWaves()
    {
        StopAutoWaves();      // also stops countdown
        StopAllCoroutines();  // stop any manual spawn routines
        _currentWaveIdx = -1; // next wave is #1
        TimeToNextWave = 0f;
    }

    /* ================================================================== */
    /*  Internal Coroutines                                               */
    /* ================================================================== */

    /// <summary>
    /// Auto loop: spawns first wave immediately then counts down for remaining waves.
    /// </summary>
    private IEnumerator AutoWaveLoop()
    {
        // Initial countdown before the first wave
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

        // Now spawn the first wave after delay
        StartNextWave();

        // Loop for remaining waves
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

    /// <summary>
    /// Countdown routine updates TimeToNextWave and fires event each frame.
    /// </summary>
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
    /// Spawns one wave and commands every unit to march toward the grid center.
    /// </summary>
    private IEnumerator SpawnWaveRoutine(ArmyType waveType)
    {
        _uiManager?.ShowWavePopup(_currentWaveIdx + 1);

        var spawned = new List<UnitBase>();

        // Spawn through ArmyManager and collect references
        yield return StartCoroutine(
            _armyManager.SpawnArmyAndCollect(
                waveType,
                Team.Enemy,
                _spawnOrigin.position,
                _unitSpawnDelay,
                spawned));

        // Compute center (player castle) and assign destinations
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

        Debug.Log($"[EnemyWaveSpawner] Wave #{_currentWaveIdx + 1} ({waveType}) – {spawned.Count} units marching to center.");
    }

    /// <summary>
    /// Invokes wave changed event and resets countdown HUD (immediate).
    /// </summary>
    private void TriggerWaveStarted()
    {
        OnWaveChanged?.Invoke();

        // If auto mode and not final wave, broadcast initial countdown value
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
