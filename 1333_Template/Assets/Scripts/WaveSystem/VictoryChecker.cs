using System.Collections;
using UnityEngine;

/// <summary>
/// Listens for unit deaths and triggers a victory sequence when
/// the final wave has been cleared of all enemy units.
/// </summary>
public class VictoryChecker : MonoBehaviour
{
    /* ------------------------------------------------------------------ */
    /*  Inspector                                                          */
    /* ------------------------------------------------------------------ */
    [Header("Managers")]
    [SerializeField] private UnitManager _unitManager;        // Reference in scene
    [SerializeField] private EnemyWaveSpawner _waveSpawner;   // Reference in scene
    [SerializeField] private UIManager _uiManager;            // Reference in scene
    [SerializeField] private float _fadeDuration = 1.0f;      // Seconds for fade

    /* ------------------------------------------------------------------ */
    /*  Runtime data                                                       */
    /* ------------------------------------------------------------------ */
    private bool _victoryStarted = false;

    /* ------------------------------------------------------------------ */
    /*  Unity lifecycle                                                    */
    /* ------------------------------------------------------------------ */
    private void OnEnable()
    {
        if (_unitManager != null)
            _unitManager.OnUnitUnregistered += HandleUnitUnregistered;
    }

    private void OnDisable()
    {
        if (_unitManager != null)
            _unitManager.OnUnitUnregistered -= HandleUnitUnregistered;
    }

    /* ------------------------------------------------------------------ */
    /*  Event handlers                                                     */
    /* ------------------------------------------------------------------ */
    /// <summary>
    /// Called each time a unit is removed from UnitManager.
    /// Triggers victory check when on the final wave.
    /// </summary>
    /// <param name="removed">The unit that was just unregistered.</param>
    private void HandleUnitUnregistered(UnitBase removed)
    {
        if (_victoryStarted || _unitManager == null || _waveSpawner == null)
            return;

        // We only care once the final wave is active.
        if (!_waveSpawner.IsOnFinalWave)
            return;

        // If any enemy units remain, do nothing.
        foreach (var unit in _unitManager.AllUnits)
        {
            if (unit != null && unit.Team == Team.Enemy && unit.IsAlive)
                return;
        }

        // No enemies left: start victory sequence.
        _victoryStarted = true;
        StartCoroutine(VictoryRoutine());
    }

    /* ------------------------------------------------------------------ */
    /*  Victory sequence                                                   */
    /* ------------------------------------------------------------------ */
    /// <summary>
    /// Performs fade-out, pauses gameplay, shows Win screen,
    /// then fades back in while keeping the game paused.
    /// </summary>
    private IEnumerator VictoryRoutine()
    {
        // Fade to black.
        yield return ScreenFader.Instance.Fade(0f, 1f, _fadeDuration);

        // Stop game time.
        Time.timeScale = 0f;

        // Activate WinScreen (no history recording).
        _uiManager.ShowScreen(UIScreenType.Win, false);

        // Ensure the screen is visible before fading in again.
        yield return null;

        // Fade from black to transparent.
        yield return ScreenFader.Instance.Fade(1f, 0f, _fadeDuration);
    }

    /* ------------------------------------------------------------------ */
    /*  Public helpers                                                     */
    /* ------------------------------------------------------------------ */
    /// <summary>
    /// Call this from WinScreen buttons to resume normal time scale.
    /// </summary>
    public void ResumeTime()
    {
        Time.timeScale = 1f;
    }

    /// <summary>Reset internal victory state for a new game.</summary>
    public void ResetVictory()
    {
        _victoryStarted = false;
    }
}
