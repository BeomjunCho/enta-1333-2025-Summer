using System.Collections.Generic;
using UnityEngine;
using FMODUnity;

/// <summary>
/// A concrete melee unit  (MountedKnight ) that inherits from UnitBase.
/// </summary>
public class MountedKnight : UnitBase, IAttackSfxProvider
{
    public EventReference AttackEvent => FMODEvents.Instance.RoyalKnightAttack;
}
