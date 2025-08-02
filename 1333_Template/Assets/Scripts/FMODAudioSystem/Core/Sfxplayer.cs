// SfxPlayer.cs
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

/// <summary>
/// Single SFX channel that owns one FMOD <see cref="EventInstance"/>.
/// Obtains / returns instances via its parent <see cref="SfxPlayerPool"/>.
/// FMOD itself handles voice priority & max-instance stealing.
/// </summary>
public sealed class SfxPlayer
{
    private const float POSITION_EPS = 0.1f;
    private const float POSITION_EPS_SQR = POSITION_EPS * POSITION_EPS;

    internal int Id { get; set; }                 // assigned by pool
    private readonly SfxPlayerPool _owner;

    private EventInstance _instance;
    private EventReference _reference;
    private Transform _follow;
    private Vector3 _lastPos;
    private bool _isSpatial;
    private bool _isPlaying;
    private float _startTime;              // still useful for debugging

    public bool IsPlaying => _isPlaying;
    public float Age() => Time.time - _startTime;

    public SfxPlayer(SfxPlayerPool owner) => _owner = owner;

    /* ------------------------------------------------------------------ */
    /*  Playback                                                           */
    /* ------------------------------------------------------------------ */

    /// <summary>Play 3D sound at a world position, optionally attached.</summary>
    public void Play3D(EventReference reference,
                       Vector3 position,
                       Transform follow = null)
    {
        Stop(true);

        _reference = reference;
        _instance = _owner.GetOrCreateInstance(reference);

        _follow = follow;
        _lastPos = follow != null ? follow.position : position;
        _isSpatial = true;
        _startTime = Time.time;

        _instance.set3DAttributes(RuntimeUtils.To3DAttributes(_lastPos));
        TryStartInstance();
    }

    /// <summary>Play 2D (non-positional) sound.</summary>
    public void Play2D(EventReference reference)
    {
        Stop(true);

        _reference = reference;
        _instance = _owner.GetOrCreateInstance(reference);

        _follow = null;
        _isSpatial = false;
        _startTime = Time.time;

        TryStartInstance();
    }

    private void TryStartInstance()
    {
        var res = _instance.start();
        if (res == FMOD.RESULT.OK)
        {
            RuntimeManager.StudioSystem.flushCommands();
            _instance.getPlaybackState(out PLAYBACK_STATE st);
            if (st != PLAYBACK_STATE.STOPPED)
            {
                _isPlaying = true;
                return;
            }
        }

        _instance.stop(STOP_MODE.IMMEDIATE);
        _owner.RecycleInstance(_reference, _instance);
        _isPlaying = false;
    }

    /* ------------------------------------------------------------------ */
    /*  Update / Stop                                                      */
    /* ------------------------------------------------------------------ */

    public bool Update()
    {
        if (!_isPlaying) return false;

        if (_isSpatial && _follow != null)
        {
            Vector3 cur = _follow.position;
            if ((cur - _lastPos).sqrMagnitude >= POSITION_EPS_SQR)
            {
                _instance.set3DAttributes(RuntimeUtils.To3DAttributes(cur));
                _lastPos = cur;
            }
        }

        _instance.getPlaybackState(out PLAYBACK_STATE st);
        if (st == PLAYBACK_STATE.STOPPED)
        {
            Stop(true);
            return true;
        }
        return false;
    }

    public void Stop(bool immediate = false)
    {
        if (!_instance.isValid()) return;

        _instance.stop(immediate ? STOP_MODE.IMMEDIATE : STOP_MODE.ALLOWFADEOUT);
        _owner.RecycleInstance(_reference, _instance);

        _isPlaying = false;
        _follow = null;
    }
}
