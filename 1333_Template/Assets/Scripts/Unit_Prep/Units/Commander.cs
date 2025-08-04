using System.Collections.Generic;
using UnityEngine;
using FMODUnity;

/// <summary>
/// A concrete melee unit ( Commander ) that inherits from UnitBase.
/// </summary>
public class Commander : UnitBase, IAttackSfxProvider
{
    public EventReference AttackEvent => FMODEvents.Instance.CommanderAttack;
}
