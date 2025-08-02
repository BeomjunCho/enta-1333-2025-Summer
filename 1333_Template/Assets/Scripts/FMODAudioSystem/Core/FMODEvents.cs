using UnityEngine;
using FMODUnity;

/// <summary>
/// Central registry that exposes FMOD <see cref="EventReference"/> assets
/// grouped by audio domain.  Access via <c>FMODEvents.Instance</c>.
/// </summary>
public class FMODEvents : Singleton<FMODEvents>
{
    /* ------------------------------------------------------------------ */
    /*  Music                                                             */
    /* ------------------------------------------------------------------ */
    [field: Header("Music")]
    [field: SerializeField]
    public EventReference MusicTheme { get; private set; }

    /* ------------------------------------------------------------------ */
    /*  Ambience                                                          */
    /* ------------------------------------------------------------------ */
    [field: Header("Ambience")]
    [field: SerializeField]
    public EventReference Ambience { get; private set; }

    /* ------------------------------------------------------------------ */
    /*  SFX : Building                                                    */
    /* ------------------------------------------------------------------ */
    [field: Header("SFX / Building")]
    [field: SerializeField]
    public EventReference BuildingPlacement { get; private set; }

    /* ------------------------------------------------------------------ */
    /*  SFX : Unit                                                        */
    /* ------------------------------------------------------------------ */
    [field: Header("SFX / Unit")]
    [field: SerializeField]
    public EventReference UnitDead { get; private set; }

    [field: SerializeField]
    public EventReference FlagFlapping { get; private set; }

    /* ------------------------------------------------------------------ */
    /*  SFX : UI                                                       */
    /* ------------------------------------------------------------------ *//*
    [field: Header("SFX / UI")]
    [field: SerializeField]
    public EventReference MenuButtonHover { get; private set; }

    [field: SerializeField]
    public EventReference MenuButtonClick { get; private set; }*/

    /* ------------------------------------------------------------------ */
    /*  FMOD Snap Shot                                                    */
    /* ------------------------------------------------------------------ */
    [field: Header("Snapshots")]
    public EventReference PauseSnapshot { get; private set; }
}
