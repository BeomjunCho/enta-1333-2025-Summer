using System.Collections;
using UnityEngine;

/// <summary>
/// Listens for castle destruction event and triggers lose sequence.
/// </summary>
public class LoseChecker : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the UI manager for showing Lose screen.")]
    [SerializeField] private UIManager _uiManager;

    [Tooltip("Seconds for fade transitions.")]
    [SerializeField] private float _fadeDuration = 1.0f;

    // Internal flag to ensure lose sequence only runs once
    private bool _loseStarted = false;

    private void OnEnable()
    {
        BuildingEvents.OnCastleDestroyed += HandleCastleDestroyed;
    }

    private void OnDisable()
    {
        BuildingEvents.OnCastleDestroyed -= HandleCastleDestroyed;
    }

    /// <summary>
    /// Called when the castle is destroyed.
    /// Starts the lose routine if not already started.
    /// </summary>
    /// <param name="castle">The castle building that was destroyed.</param>
    private void HandleCastleDestroyed(BuildingCastle castle)
    {
        if (_loseStarted || _uiManager == null)
            return;

        _loseStarted = true;
        StartCoroutine(LoseRoutine());
    }

    /// <summary>
    /// Performs fade-out, pauses gameplay, shows Lose screen,
    /// then fades back in while keeping the game paused.
    /// </summary>
    private IEnumerator LoseRoutine()
    {
        // Fade to black
        yield return ScreenFader.Instance.Fade(0f, 1f, _fadeDuration);

        // Stop game time
        Time.timeScale = 0f;

        // Show Lose screen (no history record)
        _uiManager.ShowScreen(UIScreenType.Lose, false);

        // Wait one frame to ensure UI is visible
        yield return null;

        // Fade back to transparent
        yield return ScreenFader.Instance.Fade(1f, 0f, _fadeDuration);
    }

    /// <summary>
    /// Resets internal state to allow lose sequence to run again.
    /// </summary>
    public void ResetLose()
    {
        _loseStarted = false;
    }
}
