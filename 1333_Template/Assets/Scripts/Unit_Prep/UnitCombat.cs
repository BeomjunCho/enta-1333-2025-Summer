using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles combat logic for units, including target acquisition, repositioning,
/// and attack execution. Works for both enemy units and buildings as targets.
/// Integrates with UnitManager and IDamageable interface.
/// </summary>
[RequireComponent(typeof(UnitBase))]
public class UnitCombat : MonoBehaviour
{
    // ======= Timing and AI parameters =======
    [Header("Timing")]
    [SerializeField] private float _repositionDelay = 0.05f;  // Delay before attempting attack after reposition
    [SerializeField] private int _repositionTriesMax = 3;     // Maximum attempts for repositioning

    [Header("Smart Reposition")]
    [SerializeField] private int _repositionCandidates = 8;   // How many nodes to sample for each reposition check
    [SerializeField] private float _minDistanceGain = 0.25f;  // Minimum required improvement in distance to target

    [Header("Combat Data (Prefab)")]
    [Tooltip("Spawn point for arrows / magic. If null, uses transform.")]
    [SerializeField] private Transform _firePoint = null;

    [SerializeField] private bool _enableDebug = true;        // Debug logging toggle

    // ======= Cached references =======
    private UnitBase _core;           // Reference to main unit script
    private UnitMovement _movement;   // Handles pathfinding and movement
    private UnitManager _unitManager; // Global unit manager

    // ======= Combat stats (cached from UnitTypeSO) =======
    private AttackType _attackType = AttackType.Melee;
    private float _attackRange;       // Effective attack range (may be modified by multiplier)
    private float _visionRange;       // Effective vision range (may be modified by multiplier)
    private float _baseAttackRange;   // Base attack range from UnitTypeSO (never modified)
    private float _baseVisionRange;   // Base vision range from UnitTypeSO (never modified)
    private int _attackDamage;        // Damage dealt per attack
    private float _cooldown;          // Attack cooldown duration

    // ======= Runtime state =======
    private float _cooldownTimer;     // Current cooldown timer
    private IDamageable _currentTarget;    // Current attack target (unit or building)
    private float _rangeMul = 1f;     // Range multiplier for attack/vision (used by walls, upgrades)
    private bool _isInitialized = false;   // Has Init() been called?
    private bool _isRepositioning = false; // Is the unit currently trying to move closer before attack?
    private bool _anchored = false;        // If true, unit cannot move (e.g., on wall/garrisoned)
    private bool _isGarrisoned = false;    // True if the unit is stationed on a wall/building

    // ======= Public accessors =======
    public AttackType AttackType => _attackType;
    public int Damage => _attackDamage;
    public float AttackCooldown => _cooldown;
    public float AttackRange => _attackRange;
    public float VisionRange => _visionRange;
    public Transform FirePoint => _firePoint != null ? _firePoint : transform;
    public bool IsGarrisoned => _isGarrisoned;

    // ======= Debug helper =======
    /// <summary>
    /// Prints debug messages to the console if debug is enabled.
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void Log(string msg)
    {
        if (_enableDebug) Debug.Log($"[UnitCombat] {name}: {msg}");
    }

    // ======= Initialization =======
    /// <summary>
    /// Call to set up references and base stats from the unit type asset.
    /// Must be called before this component is used.
    /// </summary>
    public void Init(UnitManager um, UnitTypeSO type)
    {
        _unitManager = um;

        // Cache all combat-related values from the ScriptableObject
        _attackType = type.AttackType;
        _attackDamage = type.Damage;
        _cooldown = type.AttackCooldown;
        _baseAttackRange = type.AttackRange;
        _baseVisionRange = type.VisionRange;

        ApplyMultiplier();  // Compute effective ranges

        _isInitialized = true;
        // Scanning for targets is managed externally (e.g., by a global scanner)
    }

    /// <summary>
    /// Get required component references at runtime.
    /// </summary>
    private void Awake()
    {
        _core = GetComponent<UnitBase>();
        _movement = GetComponent<UnitMovement>();
    }

    /// <summary>
    /// Register and unregister this unit with the central combat scanner when enabled/disabled.
    /// </summary>
    private void OnEnable() => CombatScanner.Instance?.Register(this);
    private void OnDisable() => CombatScanner.Instance?.Unregister(this);

    /// <summary>
    /// Update cooldown timer every frame.
    /// </summary>
    private void Update()
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.deltaTime;
    }

    // ======= Scanning / Target Acquisition =======
    /// <summary>
    /// Perform a single scan for enemies/buildings within vision range and handle attack/reposition logic.
    /// </summary>
    public void ScanOnce()
    {
        if (!_isInitialized) return;
        if (_core.CurrentState == UnitState.Dead) return;
        AcquireOrUpdateTarget();
    }

    // ======= Main Targeting & Attack Logic =======
    /// <summary>
    /// Attempts to acquire a new target if none exists, or updates/loses target as needed.
    /// Chooses units over buildings when both are available.
    /// Initiates reposition or attack as appropriate.
    /// </summary>
    private void AcquireOrUpdateTarget()
    {
        if (_unitManager == null) return;

        // 1. Validate current target
        if (_currentTarget != null)
        {
            float dist = Vector3.Distance(transform.position, TargetPos(_currentTarget));
            if (!_currentTarget.IsAlive || dist > _visionRange)
                LoseTarget();
        }

        // 2. Find a new target if needed (prefer enemy units over buildings)
        if (_currentTarget == null)
        {
            _currentTarget = _unitManager.FindNearestEnemyUnit(_core, _visionRange) ??
                             _unitManager.FindNearestEnemyBuilding(_core, _visionRange);

            if (_currentTarget != null)
                OnAttackStarted();
        }

        // 3. If currently attacking a building, switch to a nearer enemy unit if found
        if (_currentTarget is BuildingBase)
        {
            var nearerUnit = _unitManager.FindNearestEnemyUnit(_core, _visionRange);
            if (nearerUnit != null) _currentTarget = nearerUnit;
        }

        // 4. Attack if in range, otherwise try to reposition closer (unless movement is anchored)
        if (_currentTarget != null && !_isRepositioning && _cooldownTimer <= 0f)
        {
            float dist = Vector3.Distance(transform.position, TargetPos(_currentTarget));
            if (dist <= _attackRange)
            {
                PerformAttack();
            }
            else
            {
                if (!_anchored)
                    OnAttackStarted(); // Attempt to move closer
            }
        }
    }

    /// <summary>
    /// Executes an attack: applies damage to the target, triggers attack animation,
    /// and resets attack cooldown.
    /// </summary>
    private void PerformAttack()
    {
        _cooldownTimer = _cooldown;

        // Rotate unit to face its target before attacking
        Vector3 dir = TargetPos(_currentTarget) - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

        switch (_attackType)
        {
            case AttackType.Melee:
                _currentTarget.TakeDamage(_attackDamage);
                break;

            case AttackType.Ranged:
                FireArrow();
                break;

            case AttackType.Magic:
                SpawnMagicImpact();
                break;
        }

        _core.InternalChangeState(UnitState.Attacking);
    }

    /// <summary>
    /// Interrupts movement and begins the reposition-then-attack routine.
    /// </summary>
    private void OnAttackStarted()
    {
        _movement.CancelMovement();    // stop current path immediately

        if (!_isRepositioning)
            StartCoroutine(RepositionThenAttack());
    }

    // ======= Smart Reposition Coroutine =======
    /// <summary>
    /// Attempts to move the unit closer to its target using smart node selection.
    /// Repeats up to a capped number of times or until in range.
    /// </summary>
    private IEnumerator RepositionThenAttack()
    {
        _isRepositioning = true;

        int tries = 0;
        GridManager gm = _movement.Grid;

        while (tries < _repositionTriesMax && IsRepositionContextValid())
        {
            tries++;

            // 1. Get the node where the unit currently stands
            GridNode from = gm.GetNodeFromWorldPosition(transform.position);

            // 2. Sample nearby free nodes to move to
            List<GridNode> nodes = gm.FindNearestFreeNodes(from, _repositionCandidates);
            nodes.Remove(from); // Remove current position

            Vector3 tgtPos = TargetPos(_currentTarget);
            float distNow = Vector3.Distance(transform.position, tgtPos);

            // 3. Remove nodes that don't get the unit significantly closer to the target
            nodes.RemoveAll(n =>
            {
                float d = Vector3.Distance(n.worldPosition, tgtPos);
                return (distNow - d) < _minDistanceGain;
            });

            // 4. If no candidate is good enough, move directly toward the target's node
            if (nodes.Count == 0)
            {
                GridNode tgtNode = gm.GetNodeFromWorldPosition(tgtPos);
                _movement.PlanAndReserveDestination(tgtNode);
                _movement.MoveTo(tgtNode);
                break;
            }

            // 5. Pick the node closest to the target
            nodes.Sort((a, b) =>
                Vector3.Distance(a.worldPosition, tgtPos)
                .CompareTo(Vector3.Distance(b.worldPosition, tgtPos)));

            GridNode pick = nodes[0];
            _movement.PlanAndReserveDestination(pick);
            _movement.MoveTo(pick);

            // 6. Wait until the movement is complete or reposition context is invalid
            while (_core.CurrentState == UnitState.Moving && IsRepositionContextValid())
                yield return null;

            if (!IsRepositionContextValid()) break;
            yield return new WaitForSeconds(_repositionDelay);

            // 7. If in attack range after moving, exit reposition loop
            if (Vector3.Distance(transform.position, tgtPos) <= _attackRange)
                break;
        }

        _isRepositioning = false;

        // If the reposition context is no longer valid (target dead/out of range), lose target
        if (!IsRepositionContextValid())
            LoseTarget();
    }

    // ======= Projectile helpers =======
    private void FireArrow()
    {
        ArrowProjectile arrow = ObjectPool.Instance.RentArrow();
        if (arrow == null) return;

        arrow.Launch(FirePoint.position,
                     _currentTarget,
                     _attackDamage,
                     ObjectPool.Instance.ReturnToPool);
    }

    private void SpawnMagicImpact()
    {
        MagicImpactEffect impact = ObjectPool.Instance.RentMagic();
        if (impact == null) return;

        Vector3 hitPos = TargetPos(_currentTarget);
        impact.Spawn(hitPos, ObjectPool.Instance.ReturnToPool);
        _currentTarget.TakeDamage(_attackDamage);
    }

    // ======= Helper Methods =======
    /// <summary>
    /// Gets the best position to target: closest edge for buildings, center for units.
    /// </summary>
    private Vector3 TargetPos(IDamageable target)
    {
        if (target is BuildingBase b)
            return b.GetClosestEdgePoint(transform.position);

        return target.Tr.position;
    }

    /// <summary>
    /// Returns true if a reposition/attack sequence is still valid (target exists and movement allowed).
    /// </summary>
    private bool IsRepositionContextValid()
    {
        if (_currentTarget == null || !_currentTarget.IsAlive)
            return false;
        return _movement != null && _movement.Grid != null;
    }

    /// <summary>
    /// Locks or unlocks movement and repositioning logic.
    /// </summary>
    public void SetAnchored(bool value)
    {
        _anchored = value;
        if (value) _core.InternalChangeState(UnitState.Idle);
    }

    /// <summary>
    /// Called by wall/building logic to indicate whether the unit is currently garrisoned.
    /// </summary>
    public void SetGarrisoned(bool value)
    {
        _isGarrisoned = value;
    }

    /// <summary>
    /// Applies a new multiplier to attack and vision ranges (for special buffs/debuffs).
    /// </summary>
    public void SetRangeMultiplier(float m)
    {
        _rangeMul = Mathf.Max(0.1f, m);
        ApplyMultiplier();
    }

    /// <summary>
    /// Recalculates effective attack and vision ranges from base values and multiplier.
    /// </summary>
    private void ApplyMultiplier()
    {
        _attackRange = _baseAttackRange * _rangeMul;
        _visionRange = _baseVisionRange * _rangeMul;
    }

    /// <summary>
    /// Clears the current target and re-centers the unit on the nearest walkable node if needed.
    /// Also sets state to Idle.
    /// </summary>
    private void LoseTarget()
    {
        _currentTarget = null;

        // If anchored (e.g. on wall), just claim node and go idle
        if (_anchored)
        {
            _movement?.OccupyCurrentNode();
            _core.InternalChangeState(UnitState.Idle);
            return;
        }

        // If unit is off-center or not blocking the cell, find the closest free node to occupy
        if (_movement != null && _movement.Grid != null)
        {
            GridManager grid = _movement.Grid;
            GridNode here = grid.GetNodeFromWorldPosition(transform.position);

            bool cellAlreadyBlocked = !here.walkable || grid.IsNodeReserved(here);
            float toCenter = Vector3.Distance(transform.position, here.worldPosition);

            if (!cellAlreadyBlocked ||
                toCenter > grid.GridSettings.NodeSize * 0.25f)
            {
                List<GridNode> free = grid.FindNearestFreeNodes(here, 1);
                if (free.Count > 0)
                {
                    GridNode dst = free[0];
                    _movement.PlanAndReserveDestination(dst);
                    _movement.MoveTo(dst);
                    // The unit will set Idle after reaching the cell
                    return;
                }
            }

            // Otherwise, just claim the current cell
            _movement.OccupyCurrentNode();
        }

        // Resume idle movement logic and set state to Idle
        _movement?.Resume();
        _core.InternalChangeState(UnitState.Idle);
    }
}
