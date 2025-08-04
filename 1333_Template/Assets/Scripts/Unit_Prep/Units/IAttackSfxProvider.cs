using FMODUnity;
using UnityEngine;

/// <summary>
/// Units that can play an attack sound should implement this.
/// </summary>
public interface IAttackSfxProvider
{
    EventReference AttackEvent { get; }
}