using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using static SfxPlayerPool;

/// <summary>
/// Central facade that exposes high-level audio API
/// and delegates SFX voice allocation to SfxPlayerPool.
/// </summary>
public class AudioManager : Singleton<AudioManager>
{
    /* ------------------------------------------------------------------ */
    /*  Inspector                                                         */
    /* ------------------------------------------------------------------ */

    [Header("Volume")]
    [Range(0, 1)] public float masterVolume = 1f;
    [Range(0, 1)] public float musicVolume = 1f;
    [Range(0, 1)] public float ambienceVolume = 1f;
    [Range(0, 1)] public float sfxVolume = 1f;
    [Range(0, 1)] public float foleyVolume = 1f;
    [Range(0, 1)] public float dialougeVolume = 1f;
    [Range(0, 1)] public float uiVolume = 1f;

    [Header("Dependencies")]
    [SerializeField] private SfxPlayerPool _sfxPool = null;

    /* ------------------------------------------------------------------ */
    /*  Internal                                                          */
    /* ------------------------------------------------------------------ */

    private Bus _busMaster, _busMusic, _busAmb, _busSfx, _busFoley, _busDialogue, _busUI;

    private EventInstance _music;
    private EventInstance _ambience;
    private EventInstance _pauseSnapshot;

    private float _prevMaster, _prevMusic, _prevAmb, _prevSfx, _prevFoley, _prevDialogue, _prevUI;

    private const string _musicStateParam = "MusicState";
    private MusicState _currentMusicState = MusicState.MainMenu;

    /* ============================ Awake ============================== */
    private void Awake()
    {
        if (_sfxPool == null)
            Debug.LogWarning("AudioManager: SFX pool is not assigned");

        _busMaster = GetBusChecked("bus:/");
        _busMusic = GetBusChecked("bus:/PauseAffected/Music");
        _busAmb = GetBusChecked("bus:/PauseAffected/Ambience");
        _busSfx = GetBusChecked("bus:/PauseAffected/SFX");
        _busFoley = GetBusChecked("bus:/PauseAffected/Foley");
        _busDialogue = GetBusChecked("bus:/PauseAffected/Dialogue");
        _busUI = GetBusChecked("bus:/UI");
    }

    /* ============================ Update ============================= */
    private void Update()
    {
        UpdateVolumesIfDirty();
    }

    /* ============================ Public API ========================= */

    #region -------- SFX ----------

    public SfxHandle PlaySfx3D(Vector3 pos, EventReference ev)
    {
        if (_sfxPool == null) return SfxHandle.Invalid;
        return _sfxPool.TryPlay3D(ev, pos);
    }

    public SfxHandle PlaySfxAttached(Transform t, EventReference ev)
    {
        if (_sfxPool == null) return SfxHandle.Invalid;
        return _sfxPool.TryPlayAttached(ev, t);
    }

    public SfxHandle PlaySfx2D(EventReference ev)
    {
        if (_sfxPool == null) return SfxHandle.Invalid;
        return _sfxPool.TryPlay2D(ev);
    }

    public void StopSfx(SfxHandle handle, bool immediate = false)
    {
        _sfxPool?.Stop(handle, immediate);
    }
    /// <summary>
    /// Set parameter before fmod event instance start()
    /// </summary>
    /// <param name="position"></param>
    /// <param name="reference"></param>
    /// <param name="parameterName"></param>
    /// <param name="label"></param>
    /// <returns></returns>
    public SfxHandle PlaySfx3DWithLabelParameter(
    Vector3 position,
    EventReference reference,
    string parameterName,
    string label)
    {
        return _sfxPool.Play3DWithLabelParameter(position, reference, parameterName, label);
    }

    public void SetSfxParameter(SfxHandle handle, string name, float value, bool ignoreSeekSpeed = false)
    {
        _sfxPool?.SetParameter(handle, name, value, ignoreSeekSpeed);
    }

    public void SetSfxParameterByLabel(SfxHandle handle, string name, string label)
    {
        _sfxPool?.SetParameterLabel(handle, name, label);
    }


    #endregion

    #region -------- Music / Ambience ----------

    /// <summary>Cross-fade to a new music track.</summary>
    public void PlayMusic(EventReference musicRef, float fadeOut = 0.5f)
    {
        StopInstance(_music, fadeOut);
        _music = RuntimeManager.CreateInstance(musicRef);
        _music.start();
    }

    /// <summary>Change the FMOD labeled parameter "MusicState".</summary>
    public void SetMusicState(MusicState newState, bool immediate = false)
    {
        if (!_music.isValid()) return;
        if (!immediate && newState == _currentMusicState) return;

        _currentMusicState = newState;
        _music.setParameterByNameWithLabel(_musicStateParam, newState.ToString());
    }

    public void StopMusic(float fade = 0.5f) => StopInstance(_music, fade);

    public void PlayAmbience(EventReference ambRef, float fadeOut = 0.5f)
    {
        StopInstance(_ambience, fadeOut);
        _ambience = RuntimeManager.CreateInstance(ambRef);
        _ambience.start();
    }

    public void StopAmbience(float fade = 0.5f) => StopInstance(_ambience, fade);

    /// <summary>
    /// True if the current music EventInstance is valid and playing (or starting/sustaining).
    /// </summary>
    public bool IsMusicPlaying()
    {
        return IsInstancePlaying(_music);
    }

    /// <summary>
    /// True if the current ambience EventInstance is valid and playing (or starting/sustaining).
    /// </summary>
    public bool IsAmbiencePlaying()
    {
        return IsInstancePlaying(_ambience);
    }

    #endregion

    #region -------- Snapshot (Pause) ----------

    /// <summary>
    /// Start or stop the Pause snapshot.
    /// Uses ALLOWFADEOUT so that the snapshot's Release time defined in FMOD Studio
    /// is respected when disabling.
    /// </summary>
    /// <param name="enabled">True -> play snapshot, False -> stop with fade-out.</param>
    public void SetPauseSnapshot(bool enabled)
    {
        if (enabled)
        {
            if (_pauseSnapshot.isValid()) return;   // already playing
            EventReference snapRef = FMODEvents.Instance.PauseSnapshot;
            _pauseSnapshot = RuntimeManager.CreateInstance(snapRef);
            _pauseSnapshot.start();
        }
        else
        {
            if (!_pauseSnapshot.isValid()) return;

            // Allow FMOD to process the Release envelope instead of cutting immediately
            _pauseSnapshot.stop(STOP_MODE.ALLOWFADEOUT);
            _pauseSnapshot.release();
            _pauseSnapshot = default;
        }
    }

    #endregion

    /// <summary>
    /// Set volume (linear 0бе1) for a specific AudioChannel.
    /// Immediately updates the corresponding FMOD bus and
    /// caches the value so UpdateVolumesIfDirty() stays in sync.
    /// </summary>
    /// <param name="channel">Target mixer channel.</param>
    /// <param name="value">Linear amplitude, clamped to 0-1.</param>
    public void SetVolume(AudioChannel channel, float value)
    {
        value = Mathf.Clamp01(value);

        switch (channel)
        {
            case AudioChannel.Master:
                masterVolume = value;
                _busMaster.setVolume(value);
                _prevMaster = value;
                break;

            case AudioChannel.Music:
                musicVolume = value;
                _busMusic.setVolume(value);
                _prevMusic = value;
                break;

            case AudioChannel.Ambience:
                ambienceVolume = value;
                _busAmb.setVolume(value);
                _prevAmb = value;
                break;

            case AudioChannel.SFX:
                sfxVolume = value;
                _busSfx.setVolume(value);
                _prevSfx = value;
                break;

            case AudioChannel.Foley:
                foleyVolume = value;
                _busFoley.setVolume(value);
                _prevFoley = value;
                break;

            case AudioChannel.Dialogue:
                dialougeVolume = value;              
                _busDialogue.setVolume(value);
                _prevDialogue = value;
                break;

            case AudioChannel.UI:
                uiVolume = value;
                _busUI.setVolume(value);
                _prevUI = value;
                break;
        }
    }

    /* ============================ Cleanup ============================ */
    private void OnDestroy()
    {
        StopInstance(_music, 0f);
        StopInstance(_ambience, 0f);
        StopInstance(_pauseSnapshot, 0f);
        _sfxPool?.Shutdown();
    }

    /* ------------------------------------------------------------------ */
    /*  Helpers                                                           */
    /* ------------------------------------------------------------------ */

    private void UpdateVolumesIfDirty()
    {
        if (!Mathf.Approximately(masterVolume, _prevMaster))
        { _busMaster.setVolume(masterVolume); _prevMaster = masterVolume; }

        if (!Mathf.Approximately(musicVolume, _prevMusic))
        { _busMusic.setVolume(musicVolume); _prevMusic = musicVolume; }

        if (!Mathf.Approximately(ambienceVolume, _prevAmb))
        { _busAmb.setVolume(ambienceVolume); _prevAmb = ambienceVolume; }

        if (!Mathf.Approximately(sfxVolume, _prevSfx))
        { _busSfx.setVolume(sfxVolume); _prevSfx = sfxVolume; }

        if (!Mathf.Approximately(foleyVolume, _prevFoley))
        { _busFoley.setVolume(foleyVolume); _prevFoley = foleyVolume; }

        if (!Mathf.Approximately(dialougeVolume, _prevDialogue))
        { _busDialogue.setVolume(dialougeVolume); _prevDialogue = dialougeVolume; }

        if (!Mathf.Approximately(uiVolume, _prevUI))
        { _busUI.setVolume(uiVolume); _prevUI = uiVolume; }
    }

    private static bool IsInstancePlaying(EventInstance inst)
    {
        if (!inst.isValid())
            return false;

        inst.getPlaybackState(out PLAYBACK_STATE state);
        return state == PLAYBACK_STATE.PLAYING ||
               state == PLAYBACK_STATE.STARTING ||
               state == PLAYBACK_STATE.SUSTAINING;
    }

    private static Bus GetBusChecked(string path)
    {
        Bus bus = RuntimeManager.GetBus(path);
        if (!bus.isValid())
            Debug.LogError($"FMOD bus not found: {path}");
        return bus;
    }

    private static void StopInstance(EventInstance inst, float fade = 0)
    {
        if (!inst.isValid()) return;
        inst.stop(fade <= 0f ? STOP_MODE.IMMEDIATE : STOP_MODE.ALLOWFADEOUT);
        inst.release();
    }
}
