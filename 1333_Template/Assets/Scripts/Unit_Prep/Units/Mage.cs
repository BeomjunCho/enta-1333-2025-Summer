using System.Collections.Generic;
using UnityEngine;
using FMODUnity;

/// <summary>
/// A concrete magic unit ( Mage ) that inherits from UnitBase.
/// </summary>
public class Mage : UnitBase, IAttackSfxProvider
{
    public EventReference AttackEvent => FMODEvents.Instance.AllMageAttack;
}
