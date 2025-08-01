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

    [Header("Dependencies")]
    [SerializeField] private SfxPlayerPool _sfxPool;   // assign from Inspector
    /* ------------------------------------------------------------------ */
    /*  Internal                                                          */
    /* ------------------------------------------------------------------ */

    private Bus _busMaster, _busMusic, _busAmb, _busSfx;

    private EventInstance _music;
    private EventInstance _ambience;

    private float _prevMaster, _prevMusic, _prevAmb, _prevSfx;

    private const string _musicStateParam = "MusicState";
    private MusicState _currentMusicState = MusicState.MainMenu;

    /* ============================ Awake ============================== */
    private void Awake()
    {
        // auto-find pool if inspector not set
        if (_sfxPool == null)
            Debug.LogWarning("AudioManager: sfx pool is not assigned");

        _busMaster = GetBusChecked("bus:/");
        _busMusic = GetBusChecked("bus:/Music");
        _busAmb = GetBusChecked("bus:/Ambience");
        _busSfx = GetBusChecked("bus:/SFX");
    }

    /* ============================ Update ============================= */
    private void Update()
    {
        UpdateVolumesIfDirty();
    }

    /* ============================ Public API ========================= */
    public SfxHandle PlaySfx(Vector3 pos,
                             EventReference ev,
                             SfxPriority pr = SfxPriority.Medium)
    {
        if (_sfxPool == null) return SfxHandle.Invalid;
        return _sfxPool.TryPlay3D(ev, pos, pr);
    }

    public SfxHandle PlaySfxAttached(Transform t,
                                     EventReference ev,
                                     SfxPriority pr = SfxPriority.Medium)
    {
        if (_sfxPool == null) return SfxHandle.Invalid;
        return _sfxPool.TryPlayAttached(ev, t, pr);
    }

    public SfxHandle PlaySfx2D(EventReference ev,
                               SfxPriority pr = SfxPriority.Medium)
    {
        if (_sfxPool == null) return SfxHandle.Invalid;
        return _sfxPool.TryPlay2D(ev, pr);
    }

    /* ---------- Stop specific handle ---------- */

    public void StopSfx(SfxHandle handle, bool immediate = false)
    {
        _sfxPool?.Stop(handle, immediate);
    }

    /// <summary>Cross-fade to new music track.</summary>
    public void PlayMusic(EventReference musicRef, float fadeOut = 0.5f)
    {
        StopInstance(_music, fadeOut);
        _music = RuntimeManager.CreateInstance(musicRef);
        _music.start();
    }

    /// <summary>
    /// Change the FMOD labeled parameter "MusicState".
    /// </summary>
    /// <param name="newState">Target state.</param>
    /// <param name="immediate">
    /// If true, forces update even if state is unchanged
    /// (useful for initial sync).
    /// </param>
    public void SetMusicState(MusicState newState, bool immediate = false)
    {
        if (!_music.isValid()) return;
        if (!immediate && newState == _currentMusicState) return;

        _currentMusicState = newState;

#if FMOD_2_02_OR_NEWER
    // Uses FMOD 2.02+ helper for label names
    _music.setParameterByNameWithLabel(_musicStateParam, newState.ToString());
#else
        // Fallback: relies on label-index order (0,1,2¡¦)
        _music.setParameterByName(_musicStateParam, (float)newState);
#endif
    }

    public void StopMusic(float fade = 0.5f) => StopInstance(_music, fade);

    public void PlayAmbience(EventReference ambRef, float fadeOut = 0.5f)
    {
        StopInstance(_ambience, fadeOut);
        _ambience = RuntimeManager.CreateInstance(ambRef);
        _ambience.start();
    }

    public void StopAmbience(float fade = 0.5f) => StopInstance(_ambience, fade);

    /* ============================ Cleanup ============================ */
    private void OnDestroy()
    {
        StopInstance(_music, 0);
        StopInstance(_ambience, 0);
        _sfxPool?.Shutdown();           // pool handles its players
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
    }

    private static Bus GetBusChecked(string path)
    {
        var bus = RuntimeManager.GetBus(path);
        if (!bus.isValid())
            Debug.LogError($"FMOD bus not found: {path}");
        return bus;
    }

    private static void StopInstance(EventInstance inst, float fade)
    {
        if (!inst.isValid()) return;
        inst.stop(fade <= 0 ? STOP_MODE.IMMEDIATE : STOP_MODE.ALLOWFADEOUT);
        inst.release();
    }
}
