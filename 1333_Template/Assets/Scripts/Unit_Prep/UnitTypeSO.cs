// UnitType.cs
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Holds all the stats and references for a single unit type.
/// </summary>
[CreateAssetMenu(fileName = "UnitType", menuName = "Game/Unit Type")]
public class UnitTypeSO : ScriptableObject
{
    [Header("Basic Info")]
    [Tooltip("The display name of this unit type.")]
    [SerializeField] private string _typeName = "New Unit Name";

    [Header("Grid Size (in cells)")]
    [Tooltip("How many cells wide this unit occupies.")]
    [SerializeField] private int _width = 1;
    [Tooltip("How many cells tall this unit occupies.")]
    [SerializeField] private int _height = 1;

    [Header("Stats")]
    [Tooltip("Maximum health points of this unit.")]
    [SerializeField] private int _maxHp = 30;
    [Tooltip("Current health points of this unit.")]
    [SerializeField] private int _currentHp = 30;
    [Tooltip("Movement speed of this unit.")]
    [SerializeField] private float _moveSpeed = 3f;
    [Tooltip("Damage dealt by this unit on attack.")]
    [SerializeField] private int _damage = 5;
    [Tooltip("Defense value reducing incoming damage.")]
    [SerializeField] private int _defense = 2;

    [Header("Attack")]
    [Tooltip("Type of attack (e.g., Melee or Ranged).")]
    [SerializeField] private AttackType _attackType = AttackType.Melee;
    [Tooltip("Maximum range (in grid cells) at which this unit can attack.")]
    [SerializeField] private int _attackRange = 1;
    [Tooltip("Maximum range (in grid cells) at which this unit can see.")]
    [SerializeField] private int _visionRange = 1;
    [Tooltip("Seconds between consecutive attacks.")]
    [SerializeField] private float _attackCooldown = 0.8f;

    [Header("Faction Materials")]
    [Tooltip("Array of Materials, indexed by Team enum. e.g. [0] = Player, [1] = Enemy.")]
    [SerializeField] private Material[] _armyMaterials = null;

    [Header("Mount Settings")]
    [Tooltip("Whether this unit rides a mount (horse).")]
    [SerializeField] private bool _isMounted = false;

    [Header("Spawn Costs")]
    [Tooltip("Resources required to spawn ONE unit of this type.")]
    [SerializeField] private List<ResourceCost> _spawnCosts = new();

    /// <summary>
    /// The display name of this unit type.
    /// </summary>
    public string TypeName => _typeName;

    /// <summary>
    /// Width in grid cells this unit occupies.
    /// </summary>
    public int Width => _width;

    /// <summary>
    /// Height in grid cells this unit occupies.
    /// </summary>
    public int Height => _height;

    /// <summary>
    /// The maximum health points of this unit.
    /// </summary>
    public int MaxHp => _maxHp;

    /// <summary>
    /// The current health points of this unit.
    /// </summary>
    public int CurrentHp => _currentHp;

    /// <summary>
    /// Movement speed of this unit.
    /// </summary>
    public float MoveSpeed => _moveSpeed;

    /// <summary>
    /// Damage value dealt by this unit.
    /// </summary>
    public int Damage => _damage;

    /// <summary>
    /// Defense value that reduces incoming damage.
    /// </summary>
    public int Defense => _defense;

    /// <summary>
    /// Attack type, such as Melee or Ranged.
    /// </summary>
    public AttackType AttackType => _attackType;
    /// <summary>
    /// Maximum vision range in grid cells.
    /// </summary>
    public int VisionRange => _visionRange;

    /// <summary>
    /// Maximum attack range in grid cells.
    /// </summary>
    public int AttackRange => _attackRange;

    /// <summary>
    /// Seconds between consecutive attacks.
    /// </summary>
    public float AttackCooldown => _attackCooldown;

    /// <summary>
    /// Whether this unit rides a mount (horse).
    /// </summary>
    public bool IsMounted => _isMounted;

    /// <summary>
    /// Resources required to spawn one unit of this type.
    /// </summary>
    public List<ResourceCost> spawnCosts => _spawnCosts;

    /// <summary>
    /// Returns the Material corresponding to the given team.
    /// If no material is assigned for that team index, returns null.
    /// </summary>
    public Material GetArmyMaterial(Team team)
    {
        int index = (int)team;
        if (_armyMaterials != null && index >= 0 && index < _armyMaterials.Length)
        {
            return _armyMaterials[index];
        }
        return null;
    }
}
