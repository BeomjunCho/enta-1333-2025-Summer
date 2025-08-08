using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pager for the HowToPlay screen. Controls multiple TMP_Text blocks so only one is visible,
/// and wires Back/Next/Previous buttons with the correct behavior:
/// - Previous always invokes UIManager.GoBack().
/// - Back/Next navigate between pages. Back is disabled on first page, Next is disabled on last page.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class HowToPlayPager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private UIManager _uiManager = null; // used for GoBack when Previous is pressed

    [Header("Content")]
    [Tooltip("Ordered list of TMP_Text blocks representing pages. Only one is shown at a time.")]
    [SerializeField] private List<TMP_Text> _pages = new();

    [Header("Controls")]
    [SerializeField] private Button _previousButton = null;      // page-wise previous
    [SerializeField] private Button _nextButton = null;      // page-wise next
    [SerializeField] private Button _BackButton = null;  // global go-back (calls UIManager.GoBack)

    private int _currentIndex = 0;

    private void Awake()
    {
        if (_previousButton != null)
            _previousButton.onClick.AddListener(OnBack);

        if (_nextButton != null)
            _nextButton.onClick.AddListener(OnNext);

        if (_BackButton != null)
            _BackButton.onClick.AddListener(OnPrevious);

        RefreshVisuals();
    }

    private void OnDestroy()
    {
        if (_previousButton != null)
            _previousButton.onClick.RemoveListener(OnBack);
        if (_nextButton != null)
            _nextButton.onClick.RemoveListener(OnNext);
        if (_BackButton != null)
            _BackButton.onClick.RemoveListener(OnPrevious);
    }

    /// <summary>
    /// Navigates to previous page (does not call GoBack).
    /// </summary>
    private void OnBack()
    {
        if (_currentIndex > 0)
        {
            _currentIndex = Mathf.Max(0, _currentIndex - 1);
            RefreshVisuals();
        }
    }

    /// <summary>
    /// Advances to next page.
    /// </summary>
    private void OnNext()
    {
        if (_currentIndex < _pages.Count - 1)
        {
            _currentIndex++;
            RefreshVisuals();
        }
    }

    /// <summary>
    /// Always invokes GoBack on UIManager.
    /// </summary>
    private void OnPrevious()
    {
        if (_uiManager != null)
            _uiManager.GoBack();
        else
            Debug.LogWarning("UIManager reference missing on HowToPlayPager.");
    }

    /// <summary>
    /// Updates visible page and button states.
    /// </summary>
    private void RefreshVisuals()
    {
        for (int i = 0; i < _pages.Count; i++)
        {
            if (_pages[i] != null)
                _pages[i].gameObject.SetActive(i == _currentIndex);
        }

        if (_previousButton != null)
            _previousButton.interactable = _currentIndex > 0; // disable on first page

        if (_nextButton != null)
            _nextButton.interactable = _currentIndex < _pages.Count - 1; // disable on last page
    }

    /// <summary>
    /// Jump to a specific page index (zero-based).
    /// </summary>
    public void SetPage(int index)
    {
        _currentIndex = Mathf.Clamp(index, 0, Mathf.Max(0, _pages.Count - 1));
        RefreshVisuals();
    }

    /// <summary>
    /// Returns current page index.
    /// </summary>
    public int GetCurrentPageIndex() => _currentIndex;
}
