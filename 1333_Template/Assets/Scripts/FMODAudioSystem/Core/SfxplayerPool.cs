// SfxPlayerPool.cs
using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

/// <summary>
/// Manages pooled <see cref="SfxPlayer"/> channels and re-uses FMOD
/// <see cref="EventInstance"/> objects.  All voice priority & stealing are
/// delegated to FMOD (via per-event *Max Instances* settings).
/// </summary>
public class SfxPlayerPool : MonoBehaviour
{
    /* ------------------------------------------------------------------ */
    /*  Handle                                                             */
    /* ------------------------------------------------------------------ */

    public readonly struct SfxHandle
    {
        public static readonly SfxHandle Invalid = new(-1);
        public int Id { get; }
        internal SfxHandle(int id) => Id = id;
        public bool IsValid() => Id >= 0;
    }

    /* ------------------------------------------------------------------ */
    /*  Inspector                                                          */
    /* ------------------------------------------------------------------ */

    [Header("Pool Settings")]
    [SerializeField, Min(1)] private int _maxChannels = 128;

    /* ------------------------------------------------------------------ */
    /*  Player & Instance pools                                            */
    /* ------------------------------------------------------------------ */

    private readonly List<SfxPlayer> _players = new();
    private readonly Queue<SfxPlayer> _idle = new();
    private readonly HashSet<SfxPlayer> _idleSet = new();
    private readonly List<SfxPlayer> _active = new();
    private readonly Dictionary<EventReference, Stack<EventInstance>> _instancePool =
        new(EventReferenceComparer.Instance);

    /* ------------------------------------------------------------------ */
    /*  Lifecycle                                                          */
    /* ------------------------------------------------------------------ */

    private void Awake()
    {
        for (int i = 0; i < _maxChannels; ++i)
        {
            var p = new SfxPlayer(this) { Id = i };
            _players.Add(p);
            EnqueueIdle(p);
        }
    }

    private void OnDestroy()
    {
        foreach (var stack in _instancePool.Values)
            while (stack.Count > 0)
            {
                var inst = stack.Pop();
                if (inst.isValid()) inst.release();
            }
    }

    /* ------------------------------------------------------------------ */
    /*  Update                                                             */
    /* ------------------------------------------------------------------ */

    private void Update()
    {
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
    /*  Public Playback API                                                */
    /* ------------------------------------------------------------------ */

    public SfxHandle TryPlay3D(EventReference ev,
                               Vector3 pos)
    {
        return InternalPlay(ev, pos, null, spatial: true);
    }

    public SfxHandle TryPlayAttached(EventReference ev,
                                     Transform follow)
    {
        if (follow == null) return SfxHandle.Invalid;
        return InternalPlay(ev, follow.position, follow, spatial: true);
    }

    public SfxHandle TryPlay2D(EventReference ev)
    {
        return InternalPlay(ev, Vector3.zero, null, spatial: false);
    }

    public void Stop(SfxHandle handle, bool immediate = false)
    {
        if (!handle.IsValid()) return;

        var p = _players[handle.Id];
        p.Stop(immediate);

        _active.Remove(p);
        EnqueueIdle(p);
    }

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
    /*  Internal Logic                                                     */
    /* ------------------------------------------------------------------ */

    private SfxHandle InternalPlay(EventReference ev,
                                   Vector3 pos,
                                   Transform follow,
                                   bool spatial)
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

        EnqueueIdle(player);   // playback failed ¡æ recycle immediately
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

    private void EnqueueIdle(SfxPlayer player)
    {
        if (_idleSet.Add(player))
            _idle.Enqueue(player);
    }

    /* ------------------------------------------------------------------ */
    /*  Instance Cache Helpers                                             */
    /* ------------------------------------------------------------------ */

    internal EventInstance GetOrCreateInstance(EventReference ev)
    {
        if (_instancePool.TryGetValue(ev, out var stack) && stack.Count > 0)
            return stack.Pop();

        return RuntimeManager.CreateInstance(ev);
    }

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

    /* ------------------------------------------------------------------ */
    /*  Equality comparer for EventReference keys                          */
    /* ------------------------------------------------------------------ */

    private sealed class EventReferenceComparer : IEqualityComparer<EventReference>
    {
        public static readonly EventReferenceComparer Instance = new();
        public bool Equals(EventReference x, EventReference y) => x.Guid.Equals(y.Guid);
        public int GetHashCode(EventReference obj) => obj.Guid.GetHashCode();
    }
}
