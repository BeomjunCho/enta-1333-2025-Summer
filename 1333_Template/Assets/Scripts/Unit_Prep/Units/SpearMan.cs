using System.Collections.Generic;
using UnityEngine;
using FMODUnity;

/// <summary>
/// A concrete melee unit (SpearMan) that inherits from UnitBase.
/// </summary>
public class SpearMan : UnitBase, IAttackSfxProvider
{
    public EventReference AttackEvent => FMODEvents.Instance.SpearManAttack;
}
