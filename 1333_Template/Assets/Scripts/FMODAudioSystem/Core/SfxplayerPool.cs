// SfxPlayerPool.cs
using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using System.Collections;

/// <summary>
/// Manages pooled <see cref="SfxPlayer"/> channels and re-uses FMOD
/// <see cref="EventInstance"/> objects.  All voice priority & stealing are
/// delegated to FMOD (via per-event *Max Instances* settings).
/// </summary>
public class SfxPlayerPool : MonoBehaviour
{
    /* ------------------------------------------------------------------ */
    /*  Handle                                                            */
    /* ------------------------------------------------------------------ */

    // Struct representing a handle for a single SFX channel
    public readonly struct SfxHandle
    {
        public static readonly SfxHandle Invalid = new(-1);
        public int Id { get; }
        internal SfxHandle(int id) => Id = id;
        public bool IsValid() => Id >= 0;
    }

    /* ------------------------------------------------------------------ */
    /*  Inspector                                                         */
    /* ------------------------------------------------------------------ */

    [Header("Pool Settings")]
    [SerializeField, Min(1)] private int _maxChannels = 128;

    [Header("Release Settings")]
    [Tooltip("How many EventInstances to release per frame when shutting down")]
    [SerializeField, Min(1)] private int _batchSize = 64;

    private int _releaseBatchesThisFrame = 0; // debug
    private bool _releaseScheduled = false;

    /* ------------------------------------------------------------------ */
    /*  Player & Instance pools                                           */
    /* ------------------------------------------------------------------ */

    // Pool of SfxPlayer channels
    private readonly List<SfxPlayer> _players = new();
    private readonly Queue<SfxPlayer> _idle = new();
    private readonly HashSet<SfxPlayer> _idleSet = new();
    private readonly List<SfxPlayer> _active = new();

    // Pool for FMOD EventInstance objects by event reference
    private readonly Dictionary<EventReference, Stack<EventInstance>> _instancePool =
        new(EventReferenceComparer.Instance);

    /* ------------------------------------------------------------------ */
    /*  Lifecycle                                                        */
    /* ------------------------------------------------------------------ */

    private void Awake()
    {
        // Pre-instantiate all SfxPlayers and fill idle pool
        for (int i = 0; i < _maxChannels; ++i)
        {
            var p = new SfxPlayer(this) { Id = i };
            _players.Add(p);
            EnqueueIdle(p);
        }
    }

    private void OnDisable()
    {
        // Stop all players and clear active bookkeeping
        foreach (var p in _players)
            p.Stop(true);

        _active.Clear();

        // Release all instances gracefully when shutting down
        if (Application.isPlaying && !_releaseScheduled)
        {
            _releaseScheduled = true;
            StartGradualRelease();
        }
        else if (!Application.isPlaying)
        {
            ReleaseAllImmediate();
        }
    }

    /* ------------------------------------------------------------------ */
    /*  Update                                                           */
    /* ------------------------------------------------------------------ */

    private void Update()
    {
        // Update all active players, recycle if finished
        for (int i = _active.Count - 1; i >= 0; --i)
        {
            var p = _active[i];
            if (p.Update())
            {
                _active.RemoveAt(i);
                EnqueueIdle(p);
            }
        }
    }

    /* ------------------------------------------------------------------ */
    /*  Public Playback API                                              */
    /* ------------------------------------------------------------------ */

    // Try to play a 3D sound at a position
    public SfxHandle TryPlay3D(EventReference ev, Vector3 pos)
    {
        return InternalPlay(ev, pos, null, spatial: true);
    }

    // Try to play a 3D sound attached to a transform
    public SfxHandle TryPlayAttached(EventReference ev, Transform follow)
    {
        if (follow == null) return SfxHandle.Invalid;
        return InternalPlay(ev, follow.position, follow, spatial: true);
    }

    // Try to play a 2D sound
    public SfxHandle TryPlay2D(EventReference ev)
    {
        return InternalPlay(ev, Vector3.zero, null, spatial: false);
    }

    // Stop a sound using its handle
    public void Stop(SfxHandle handle, bool immediate = false)
    {
        if (!handle.IsValid()) return;

        var p = _players[handle.Id];
        p.Stop(immediate);

        _active.Remove(p);
        EnqueueIdle(p);
    }

    // Set a float parameter for a playing SFX
    public void SetParameter(SfxHandle handle, string name, float value, bool ignoreSeekSpeed = false)
    {
        if (!handle.IsValid()) return;
        _players[handle.Id].SetParameter(name, value, ignoreSeekSpeed);
    }

    // Set a label parameter for a playing SFX
    public void SetParameterLabel(SfxHandle handle, string name, string label)
    {
        if (!handle.IsValid()) return;
        _players[handle.Id].SetParameterByLabel(name, label);
    }

    /// <summary>
    /// Wrapper that routes to a player and applies a label parameter before playback.
    /// </summary>
    public SfxHandle Play3DWithLabelParameter(
        Vector3 position,
        EventReference reference,
        string parameterName,
        string label)
    {
        var player = AllocatePlayer();
        player.Play3DWithLabelParameter(reference, position, parameterName, label);
        return new SfxHandle(player.Id);
    }

    // Stop all players and clear pools (used on shutdown)
    public void Shutdown()
    {
        foreach (var p in _players)
            p.Stop(true);

        _active.Clear();
        _idle.Clear();
        _idleSet.Clear();
        foreach (var p in _players)
            EnqueueIdle(p);
    }

    /* ------------------------------------------------------------------ */
    /*  Internal Logic                                                    */
    /* ------------------------------------------------------------------ */

    // Internal play helper, allocates a player, starts playback, manages active/idle pools
    private SfxHandle InternalPlay(EventReference ev, Vector3 pos, Transform follow, bool spatial)
    {
        var player = AllocatePlayer();
        if (player == null) return SfxHandle.Invalid;

        if (spatial)
            player.Play3D(ev, pos, follow);
        else
            player.Play2D(ev);

        if (player.IsPlaying)
        {
            _active.Add(player);
            return new SfxHandle(player.Id);
        }

        EnqueueIdle(player);   // playback failed, recycle immediately
        return SfxHandle.Invalid;
    }

    /// <summary>
    /// Returns an idle player if available; otherwise null.
    /// FMOD handles voice stealing via Max Instances, so we do not force it here.
    /// </summary>
    private SfxPlayer AllocatePlayer()
    {
        if (_idle.Count > 0)
        {
            var idle = _idle.Dequeue();
            _idleSet.Remove(idle);
            return idle;
        }

        // No idle player -> Make new player
        var p = new SfxPlayer(this) { Id = _players.Count };
        _players.Add(p);
        return p;
    }

    // Add player to the idle pool
    private void EnqueueIdle(SfxPlayer player)
    {
        if (_idleSet.Add(player))
            _idle.Enqueue(player);
    }

    /* ------------------------------------------------------------------ */
    /* Helpers                                                            */
    /* ------------------------------------------------------------------ */

    // Get an existing FMOD instance from pool or create a new one
    internal EventInstance GetOrCreateInstance(EventReference ev)
    {
        if (_instancePool.TryGetValue(ev, out var stack) && stack.Count > 0)
            return stack.Pop();

        return RuntimeManager.CreateInstance(ev);
    }

    // Recycle an instance for reuse later
    internal void RecycleInstance(EventReference ev, EventInstance inst)
    {
        if (!_instancePool.TryGetValue(ev, out var stack))
        {
            stack = new Stack<EventInstance>();
            _instancePool.Add(ev, stack);
        }

        inst.stop(STOP_MODE.IMMEDIATE);
        inst.set3DAttributes(default);
        stack.Push(inst);
    }

    // Start gradual release of all instances (in play mode)
    private void StartGradualRelease() => CoroutineRelay.Instance.Run(ReleaseRoutine());

    // Coroutine: releases instances in batches to avoid frame spikes
    private IEnumerator ReleaseRoutine()
    {
        Debug.Log("StartGradualRelease initiated.");
        while (true)
        {
            bool didWork = false;
            foreach (var stack in _instancePool.Values)
            {
                if (stack.Count == 0) continue;

                int countThisFrame = Mathf.Min(_batchSize, stack.Count);
                for (int i = 0; i < countThisFrame; ++i)
                {
                    var inst = stack.Pop();
                    if (inst.isValid()) inst.release();
                }
                _releaseBatchesThisFrame += countThisFrame;
                didWork = true;
            }

            Debug.Log($"ReleaseRoutine frame: released {_releaseBatchesThisFrame} instances so far. Remaining total: {TotalRemainingInstances()}");

            _releaseBatchesThisFrame = 0;

            if (!didWork) break;

            yield return null;
        }
        _instancePool.Clear();
        Debug.Log("Gradual release complete.");
    }

    // Helper: get total number of pooled instances remaining
    private int TotalRemainingInstances()
    {
        int sum = 0;
        foreach (var stack in _instancePool.Values)
            sum += stack.Count;
        return sum;
    }

    // Immediately release all pooled instances (in edit mode)
    private void ReleaseAllImmediate()
    {
        foreach (var stack in _instancePool.Values)
        {
            while (stack.Count > 0)
            {
                var inst = stack.Pop();
                if (inst.isValid()) inst.release();
            }
        }
        _instancePool.Clear();
    }

    /* ------------------------------------------------------------------ */
    /*  Equality comparer for EventReference keys                         */
    /* ------------------------------------------------------------------ */

    // Equality comparer for EventReference to use as dictionary key
    private sealed class EventReferenceComparer : IEqualityComparer<EventReference>
    {
        public static readonly EventReferenceComparer Instance = new();
        public bool Equals(EventReference x, EventReference y) => x.Guid.Equals(y.Guid);
        public int GetHashCode(EventReference obj) => obj.Guid.GetHashCode();
    }
}
