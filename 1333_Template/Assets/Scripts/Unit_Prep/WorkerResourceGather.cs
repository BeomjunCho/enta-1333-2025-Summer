using System.Collections;
using UnityEngine;
using static SfxPlayerPool;

/// <summary>
/// Worker AI that only harvests a resource when it's standing next to it.
/// No movement commands, no vision scan just a spatial lookup every scanInterval,
/// and a separate gatherCooldown after each hit.
/// </summary>
[RequireComponent(typeof(UnitBase), typeof(UnitMovement))]
public class WorkerResourceGather : MonoBehaviour
{
    [Header("Gather Settings")]
    [Tooltip("Max distance at which harvesting can occur.")]
    [SerializeField] private float _gatherRange = 1.2f;
    [Tooltip("Seconds between each harvest hit.")]
    [SerializeField] private float _gatherCooldown = 3f;
    [Tooltip("Damage applied per harvest hit.")]
    [SerializeField] private int _gatherDamage = 10;

    [Header("Scan Settings")]
    [Tooltip("Seconds between checks for a nearby resource.")]
    [SerializeField] private float _scanInterval = 0.5f;

    private UnitBase _core;
    private UnitManager _unitManager;
    private IDamageable _target;
    private float _scanTimer;
    private float _cooldownTimer;
    public enum ResourceType { Rock, Wood }

    private SfxHandle _gatherSfx;

    private void Awake()
    {
        _core = GetComponent<UnitBase>();
        _scanTimer = 0f;
    }

    /// <summary>
    /// Called from UnitBase.Initialize(...) to inject your UnitManager.
    /// </summary>
    public void Initialize(UnitManager unitManager)
    {
        _unitManager = unitManager;
        if (_unitManager == null)
            Debug.LogError("WorkerResourceGather: UnitManager injection failed");
    }

    private void Update()
    {
        // 1) Dead or currently moving -> do nothing
        if (_core.CurrentState == UnitState.Dead ||
            _core.CurrentState == UnitState.Moving)
            return;

        // 2) Tick timers
        _scanTimer -= Time.deltaTime;
        _cooldownTimer = Mathf.Max(0f, _cooldownTimer - Time.deltaTime);

        // 3) If no target (or it's dead), look again—but only every scanInterval
        if ((_target == null || !_target.IsAlive) &&
            _core.CurrentState == UnitState.Idle &&
            _scanTimer <= 0f)
        {
            _target = _unitManager.FindNearestResource(
                             transform.position, _gatherRange);
            _scanTimer = _scanInterval;
        }

        // 4) If still no valid target, bail
        if (_target == null || !_target.IsAlive)
            return;

        // 5) If within gather range and cooldown elapsed, harvest
        float dist = Vector3.Distance(
            transform.position, _target.Tr.position);

        if (dist <= _gatherRange && _cooldownTimer <= 0f &&
            _core.CurrentState != UnitState.Attacking)
        {
            StartCoroutine(GatherRoutine());
        }
    }

    private IEnumerator GatherRoutine()
    {
        if (_target == null || !_target.IsAlive)
            yield break;

        // Face your resource
        Vector3 dir = _target.Tr.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);

        // Play your work animation via the Attacking state
        _core.InternalChangeState(UnitState.Attacking);

        // Sync to your animation hit frame
        yield return new WaitForSeconds(0.4f);

        if (_target == null || !_target.IsAlive)
        {
            _core.InternalChangeState(UnitState.Idle);
            yield break;
        }

        // Actually apply damage and reset the cooldown now
        _target.TakeDamage(_gatherDamage);
        GatheringSfx();
        _cooldownTimer = _gatherCooldown;

        // Go back to Idle so we can start the next cycle
        _core.InternalChangeState(UnitState.Idle);

        // If the resource is fully harvested, clear the target
        if (!_target.IsAlive)
            _target = null;
    }

    public void GatheringSfx()
    {
        if (_target is not MonoBehaviour mb) return;

        GameObject go = mb.gameObject;
        ResourceType? type = null;

        if (go.TryGetComponent<EnvRock>(out _)) type = ResourceType.Rock;
        else if (go.TryGetComponent<EnvTree>(out _)) type = ResourceType.Wood;
        if (!type.HasValue) return;

        // Play with labeled parameter in one shot (parameter applied before start)
        _gatherSfx = AudioManager.Instance.PlaySfx3DWithLabelParameter(
            transform.position,
            FMODEvents.Instance.ResourceGathering,
            "HitResourceType",
            type.Value.ToString());
    }
}
