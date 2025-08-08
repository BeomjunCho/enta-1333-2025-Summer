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

    internal int Id { get; set; }              // assigned by pool
    private readonly SfxPlayerPool _owner;

    private EventInstance _instance;
    private EventReference _reference;
    private Transform _follow;
    private Vector3 _lastPos;
    private bool _isSpatial;
    private bool _isPlaying;
    private float _startTime;                  // debug helper

    public bool IsPlaying => _isPlaying;
    public float Age() => Time.time - _startTime;

    public SfxPlayer(SfxPlayerPool owner) => _owner = owner;

    /* ------------------------------------------------------------------ */
    /*  Playback                                                           */
    /* ------------------------------------------------------------------ */

    /// <summary>Play 3D sound at a world position, optionally attached.</summary>
    public void Play3D(EventReference reference, Vector3 position, Transform follow = null)
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

    // Try to start the FMOD event instance, recycle if it fails
    private void TryStartInstance()
    {
        var res = _instance.start();
        if (res == FMOD.RESULT.OK)
        {
            _instance.getPlaybackState(out PLAYBACK_STATE st);
            if (st != PLAYBACK_STATE.STOPPED)
            {
                _isPlaying = true;
                return;
            }
        }

        // failed -> recycle immediately
        _instance.stop(STOP_MODE.IMMEDIATE);
        _owner.RecycleInstance(_reference, _instance);
        _isPlaying = false;
    }

    /* ------------------------------------------------------------------ */
    /*  Parameter helpers                                                  */
    /* ------------------------------------------------------------------ */

    // Set float parameter by name
    internal void SetParameter(string name, float value, bool ignoreSeekSpeed = false)
    {
        if (!_instance.isValid()) return;
        _instance.setParameterByName(name, value, ignoreSeekSpeed);
    }

    // Set parameter by label (enum/label style)
    internal void SetParameterByLabel(string name, string label)
    {
        if (!_instance.isValid()) return;
        _instance.setParameterByNameWithLabel(name, label);
    }

    /// <summary>
    /// Play a 3D one-shot with a label parameter applied before <c>start()</c>.
    /// </summary>
    internal void Play3DWithLabelParameter(EventReference clipRef,
                                           Vector3 position,
                                           string paramName,
                                           string label)
    {
        _reference = clipRef;
        _instance = RuntimeManager.CreateInstance(_reference);

        _instance.set3DAttributes(RuntimeUtils.To3DAttributes(position));
        _instance.setParameterByNameWithLabel(paramName, label);   // before start
        _instance.start();
        _instance.release();

        _isPlaying = true;
    }

    /* ------------------------------------------------------------------ */
    /*  Update / Stop                                                      */
    /* ------------------------------------------------------------------ */

    /// <summary>
    /// Called each frame by the pool.  
    /// Returns <c>true</c> when playback has finished this frame.
    /// </summary>
    public bool Update()
    {
        if (!_isPlaying) return false;

        // Follow target transform when spatial
        if (_isSpatial && _follow != null)
        {
            Vector3 cur = _follow.position;
            if ((cur - _lastPos).sqrMagnitude >= POSITION_EPS_SQR)
            {
                _instance.set3DAttributes(RuntimeUtils.To3DAttributes(cur));
                _lastPos = cur;
            }
        }

        // Check playback state
        _instance.getPlaybackState(out PLAYBACK_STATE st);
        if (st == PLAYBACK_STATE.STOPPED)
        {
            Stop(true);
            return true;
        }
        return false;
    }

    /// <summary>Stop playback and return this player to the pool.</summary>
    public void Stop(bool immediate = false)
    {
        if (!_instance.isValid()) return;

        _instance.stop(immediate ? STOP_MODE.IMMEDIATE : STOP_MODE.ALLOWFADEOUT);
        _owner.RecycleInstance(_reference, _instance);

        _isPlaying = false;
        _follow = null;
    }
}
