using System.Collections.Generic;
using UnityEngine;
using FMODUnity;

/// <summary>
/// A concrete ranged unit ( CrossbowMan ) that inherits from UnitBase.
/// </summary>
public class CrossbowMan : UnitBase, IAttackSfxProvider
{
    public EventReference AttackEvent => FMODEvents.Instance.CrossbowManAttack;
}
