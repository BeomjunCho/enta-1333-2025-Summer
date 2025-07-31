using UnityEngine;
using System.Collections.Generic;
using TMPro;
using System.Collections;

public enum UIScreenType
{
    None,
    MainMenu,
    Settings,
    Pause,
    HowToPlay,
    Win,
    Lose
}

/// <summary>
/// Central controller for UI screens (menu, pause, settings, etc).
/// Use ShowScreen() to activate one screen and hide all others.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("UI Screens")]
    [SerializeField] private GameObject _mainMenuScreen;
    [SerializeField] private GameObject _settingsScreen;
    [SerializeField] private GameObject _pauseScreen;
    [SerializeField] private GameObject _howToPlayScreen;
    [SerializeField] private GameObject _winScreen; 
    [SerializeField] private GameObject _loseScreen;

    [Header("UI-Sensitive Game Objects")]
    [SerializeField] private GameObject _buildingPlacementManagerGO;
    [SerializeField] private GameObject _selectionManagerGO;
    [SerializeField] private GameObject _resourcePanelUI;

    [Header("Wave HUD (Gameplay Only)")]
    [Tooltip("Wave HUD root GameObject (contains texts)")]
    [SerializeField] private GameObject _waveHUDGO;
    [Tooltip("Displays current wave & remaining waves")]
    [SerializeField] private TMP_Text _waveInfoText;
    [Tooltip("Displays countdown to next wave")]
    [SerializeField] private TMP_Text _countdownText;

    [Header("Wave Popup")]
    [SerializeField] private GameObject _wavePopupGO; 
    [SerializeField] private TMP_Text _wavePopupText;   
    [SerializeField] private float _popupDuration = 2f; 
    private Coroutine _wavePopupRoutine;

    [Header("Wave Spawner")]
    [SerializeField] private EnemyWaveSpawner _enemyWaveSpawner;

    private Dictionary<UIScreenType, GameObject> _screenMap;
    // navigation history stack
    private Stack<UIScreenType> _history = new Stack<UIScreenType>();
    // currently active screen
    private UIScreenType _currentScreen = UIScreenType.None;

    private void Awake()
    {
        // Initialize map
        _screenMap = new Dictionary<UIScreenType, GameObject>
        {
            { UIScreenType.MainMenu, _mainMenuScreen },
            { UIScreenType.Settings, _settingsScreen },
            { UIScreenType.Pause, _pauseScreen },
            { UIScreenType.HowToPlay, _howToPlayScreen },
            { UIScreenType.Win, _winScreen },
            { UIScreenType.Lose, _loseScreen }
        };

        HideAll(); // Start with everything hidden
    }

    private void OnEnable()
    {
        if (_enemyWaveSpawner != null)
        {
            _enemyWaveSpawner.OnWaveChanged += UpdateWaveInfo;
            _enemyWaveSpawner.OnCountdownUpdated += UpdateCountdown;

            UpdateWaveInfo();
            UpdateCountdown(_enemyWaveSpawner.TimeToNextWave);
        }
    }

    private void OnDisable()
    {
        if (_enemyWaveSpawner != null)
        {
            _enemyWaveSpawner.OnWaveChanged -= UpdateWaveInfo;
            _enemyWaveSpawner.OnCountdownUpdated -= UpdateCountdown;
        }
    }

    /// <summary>
    /// Hides all UI screens.
    /// </summary>
    public void HideAll()
    {
        if (_screenMap == null)
            return;

        foreach (var kvp in _screenMap)
            if (kvp.Value != null)
                kvp.Value.SetActive(false);
    }

    /// <summary>
    /// Show only the specified UI screen.
    /// If recordHistory is true, pushes the previous screen onto history.
    /// </summary>
    public void ShowScreen(UIScreenType type, bool recordHistory = true)
    {
        if (recordHistory)
            _history.Push(_currentScreen);

        _currentScreen = type;

        HideAll();

        // UI screen activation
        if (_screenMap.TryGetValue(type, out var screen) && screen != null)
            screen.SetActive(true);

        // Game play object management
        HandleGameplayObjectsVisibility(type);
    }

    /// <summary>
    /// Enables gameplay objects only when UI is None (game in progress).
    /// </summary>
    private void HandleGameplayObjectsVisibility(UIScreenType screen)
    {
        bool enable = (screen == UIScreenType.None);

        if (_buildingPlacementManagerGO != null)
            _buildingPlacementManagerGO.SetActive(enable);

        if (_selectionManagerGO != null)
            _selectionManagerGO.SetActive(enable);

        if (_resourcePanelUI != null)
            _resourcePanelUI.SetActive(enable);

        if (_waveHUDGO != null)
        {
            _waveHUDGO.SetActive(enable);
            if (_waveHUDGO.activeSelf)
            {
                Debug.Log("Wave HUD Show UP");
            }
        }
    }

    /// <summary>
    /// Go back to the previous screen, or MainMenu if none.
    /// </summary>
    public void GoBack()
    {
        if (_history.Count > 0)
            ShowScreen(_history.Pop(), false);
        else
            ShowScreen(UIScreenType.MainMenu, false);
    }

    /// <summary>
    /// Returns true if the given screen is currently visible.
    /// </summary>
    public bool IsVisible(UIScreenType type)
    {
        return _screenMap.TryGetValue(type, out var screen) && screen.activeSelf;
    }

    public void ShowWavePopup(int waveNumber)
    {
        if (_wavePopupRoutine != null)
            StopCoroutine(_wavePopupRoutine);
        _wavePopupRoutine = StartCoroutine(WavePopupRoutine(waveNumber));
    }

    private IEnumerator WavePopupRoutine(int waveNumber)
    {
        if (_wavePopupGO == null || _wavePopupText == null)
            yield break;

        _wavePopupText.text = $"Wave {waveNumber} is coming!";
        _wavePopupGO.SetActive(true);
        yield return new WaitForSeconds(_popupDuration);
        _wavePopupGO.SetActive(false);
    }

    private void UpdateWaveInfo()
    {
        if (_waveInfoText == null || _enemyWaveSpawner == null) return;

        if (_enemyWaveSpawner.CurrentWave <= 0)
        {
            _waveInfoText.text = "No wave yet";
        }
        else
        {
            _waveInfoText.text =
                $"Wave {_enemyWaveSpawner.CurrentWave} underway - {_enemyWaveSpawner.RemainingWaves} to go";
        }
    }

    private void UpdateCountdown(float t)
    {
        if (_countdownText == null) return;

        if (t <= 0.01f)
            _countdownText.text = "Next wave ready";
        else
            _countdownText.text = $"Next wave in {t:F1}s";
    }

    public void ResetHUD()
    {
        _waveInfoText.text = "No wave yet";
    }
}


