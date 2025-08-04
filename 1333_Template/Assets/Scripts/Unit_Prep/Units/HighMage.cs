using System.Collections.Generic;
using UnityEngine;
using FMODUnity;

/// <summary>
/// A concrete magic unit ( HighMage ) that inherits from UnitBase.
/// </summary>
public class HighMage : UnitBase, IAttackSfxProvider
{
    public EventReference AttackEvent => FMODEvents.Instance.AllMageAttack;
}
