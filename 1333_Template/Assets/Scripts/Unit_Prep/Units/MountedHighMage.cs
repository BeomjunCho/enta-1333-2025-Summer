using System.Collections.Generic;
using UnityEngine;
using FMODUnity;

/// <summary>
/// A concrete magic unit ( MountedHighMage ) that inherits from UnitBase.
/// </summary>
public class MountedHighMage : UnitBase, IAttackSfxProvider
{
    public EventReference AttackEvent => FMODEvents.Instance.AllMageAttack;
}
