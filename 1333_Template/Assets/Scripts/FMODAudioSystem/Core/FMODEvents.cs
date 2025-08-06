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
    [field: Tooltip("Parameter: MusicState(MainMenu, InGame, Battle)")]
    [field: SerializeField] public EventReference MusicTheme { get; private set; }

    /* ------------------------------------------------------------------ */
    /*  Ambience                                                          */
    /* ------------------------------------------------------------------ */
    [field: Header("Ambience")]
    [field: SerializeField] public EventReference Ambience { get; private set; }
    [field: SerializeField] public EventReference BirdSing { get; private set; }
    [field: SerializeField] public EventReference WaterLapping { get; private set; } // Need to be replaced to Stereo

    /* ------------------------------------------------------------------ */
    /*  Foley                                                             */
    /* ------------------------------------------------------------------ */
    [field: Header("Foley")]
    [field: Tooltip("Parameter: HitResourceType(Rock, Wood)")]
    [field: SerializeField] public EventReference ResourceGathering { get; private set; }
    [field: SerializeField] public EventReference FlagFlapping { get; private set; }
    [field: SerializeField] public EventReference UnitFootStep { get; private set; }

    /* --------------------------------------------------------- --------- */
    /*  SFX : Building                                                    */
    /* ------------------------------------------------------------------ */
    [field: Header("SFX / Building")]
    [field: SerializeField] public EventReference BuildingPlacement { get; private set; }
    [field: SerializeField] public EventReference BarrackEnv { get; private set; }
    [field: SerializeField] public EventReference BlackSmithEnv { get; private set; }
    [field: SerializeField] public EventReference FarmEnv { get; private set; }
    [field: SerializeField] public EventReference LumberMillEnv { get; private set; }
    [field: SerializeField] public EventReference StableEnv { get; private set; }
    [field: SerializeField] public EventReference WorkShopEnv { get; private set; }

    /* ------------------------------------------------------------------ */
    /*  SFX : Unit                                                        */
    /* ------------------------------------------------------------------ */
    [field: Header("SFX / Unit")]
    [field: SerializeField] public EventReference ArcherAttack { get; private set; }
    [field: SerializeField] public EventReference SpearManAttack { get; private set; }
    [field: SerializeField] public EventReference AllMageAttack { get; private set; }
    [field: SerializeField] public EventReference CommanderAttack { get; private set; }
    [field: SerializeField] public EventReference CrossbowManAttack { get; private set; }
    [field: SerializeField] public EventReference RoyalKnightAttack { get; private set; }

    /* ------------------------------------------------------------------ */
    /*  SFX : UI                                                          */
    /* ------------------------------------------------------------------ */
    [field: Header("SFX / UI")]
    [field: SerializeField] public EventReference MenuButtonHover { get; private set; }

    [field: SerializeField] public EventReference MenuButtonClick { get; private set; }
    [field: SerializeField] public EventReference HudButtonHover { get; private set; }
    [field: SerializeField] public EventReference HudButtonClick { get; private set; }
    [field: SerializeField] public EventReference SelectedUIPanelOpen { get; private set; }
    [field: SerializeField] public EventReference WavePopUp { get; private set; }
    [field: SerializeField] public EventReference DefeatSfx { get; private set; }
    [field: SerializeField] public EventReference WinSfx { get; private set; }

    /* ------------------------------------------------------------------ */
    /*  SFX : Flag                                                        */
    /* ------------------------------------------------------------------ */
    [field: Header("SFX / Flag")]
    [field: SerializeField] public EventReference FlagPickUp { get; private set; }
    [field: SerializeField] public EventReference FlagPutDown { get; private set; }

    /* ------------------------------------------------------------------ */
    /*  SFX : Gate                                                        */
    /* ------------------------------------------------------------------ */
    [field: Header("SFX / Gate")]
    [field: Tooltip("Parameter: Gate(Open, Close)")]
    [field: SerializeField] public EventReference StoneGateOpenClose { get; private set; }
    [field: Tooltip("Parameter: Gate(Open, Close)")]
    [field: SerializeField] public EventReference WoodGateOpenClose { get; private set; }

    /* ------------------------------------------------------------------ */
    /*  Dialogue                                                          */
    /* ------------------------------------------------------------------ */
    [field: Header("Dialogue")]
    [field: SerializeField] public EventReference UnitDead { get; private set; }

    /* ------------------------------------------------------------------ */
    /*  FMOD Snap Shot                                                    */
    /* ------------------------------------------------------------------ */
    [field: Header("Snapshots")]
    [field: SerializeField] public EventReference PauseSnapshot { get; private set; }

}
