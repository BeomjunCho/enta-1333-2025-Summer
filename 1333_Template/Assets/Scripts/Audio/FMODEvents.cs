using UnityEngine;
using FMODUnity;

public class FMODEvents : Singleton<FMODEvents>
{
    [Header("Music")]
    [field: SerializeField] public EventReference MusicTheme { get; private set; }


    [Header("Ambience")]
    [field: SerializeField] public EventReference Ambience { get; private set; }

    [field: Header("SFX")]
    [field: SerializeField] public EventReference BuildingPlacement { get; private set; }
    [field: SerializeField] public EventReference UnitDead { get; private set; }
    [field: SerializeField] public EventReference FlagFlapping { get; private set; }
}
