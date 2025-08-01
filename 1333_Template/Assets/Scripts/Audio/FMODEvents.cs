using UnityEngine;
using FMODUnity;

public class FMODEvents : Singleton<FMODEvents>
{
    [field: Header("Music")]
    [field: SerializeField] public EventReference MusicTheme { get; private set; }
}
