using System.Collections.Generic;
using UnityEngine;
using FMODUnity;

/// <summary>
/// A concrete ranged unit ( Archer ) that inherits from UnitBase.
/// </summary>
public class Archer : UnitBase, IAttackSfxProvider
{
    public EventReference AttackEvent => FMODEvents.Instance.ArcherAttack;

}
