using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central service that distributes expensive <see cref="UnitCombat.ScanOnce"/> calls
/// across multiple frames.  The amount of work adapts to the total unit count so
/// detection remains responsive under heavy load or low frame-rates.
/// </summary>
public class CombatScanner : MonoBehaviour
{
    /* ------------------------------------------------------------------ */
    /*  Inspector                                                         */
    /* ------------------------------------------------------------------ */
    [Header("Scan Budget")]
    [Tooltip("Absolute upper-bound of scans processed in a single frame.")]
    [SerializeField] private int scansPerFrame = 20;

    [Tooltip("Percentage of total units to scan each frame (0.01-1).")]
    [Range(0.01f, 1f)]
    [SerializeField] private float _scanRatio = 0.20f;

    /* ------------------------------------------------------------------ */
    /*  Internal                                                          */
    /* ------------------------------------------------------------------ */
    private readonly List<UnitCombat> _units = new(); // Registered units
    private int _current;                              // Ring-buffer cursor

    /// <summary>Global access (one instance only).</summary>
    public static CombatScanner Instance { get; private set; }

    /* ------------------------------------------------------------------ */
    /*  Unity Callbacks                                                   */
    /* ------------------------------------------------------------------ */
    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (_units.Count == 0) return;

        // 1) Determine dynamic budget
        int budget = Mathf.Clamp(Mathf.CeilToInt(_units.Count * _scanRatio), 1, scansPerFrame);

        int scanned = 0;
        int loopGuard = _units.Count; // Prevent infinite loop if list shrinks while iterating

        // 2) Round-robin through the registry
        while (scanned < budget && loopGuard > 0 && _units.Count > 0)
        {
            _current %= _units.Count;
            UnitCombat uc = _units[_current];
            _current++;
            loopGuard--;

            // Remove null / disabled entries without counting toward budget
            if (uc == null || !uc.enabled)
            {
                _units.RemoveAt((_current - 1 + _units.Count) % _units.Count);
                continue;
            }

            uc.ScanOnce();
            scanned++;
        }
    }

    /* ------------------------------------------------------------------ */
    /*  Registration API                                                  */
    /* ------------------------------------------------------------------ */
    /// <summary>Adds a unit to the scan registry.</summary>
    /// <param name="uc">The <see cref="UnitCombat"/> to register.</param>
    public void Register(UnitCombat uc)
    {
        if (uc != null && !_units.Contains(uc))
            _units.Add(uc);
    }

    /// <summary>Removes a unit from the scan registry.</summary>
    /// <param name="uc">The <see cref="UnitCombat"/> to unregister.</param>
    public void Unregister(UnitCombat uc) => _units.Remove(uc);
}
