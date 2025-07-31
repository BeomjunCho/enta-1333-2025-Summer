using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Helper for showing placement instructions next to the mouse cursor.
/// </summary>
public class BuildingPlacementUIHelper : MonoBehaviour
{
    [SerializeField]
    private RectTransform _panel;         // Panel containing instruction labels

    [SerializeField]
    private TMP_Text _labelLeft;              // Label for left-click action

    [SerializeField]
    private TMP_Text _labelMiddle;            // Label for middle-click action

    [SerializeField]
    private TMP_Text _labelRight;             // Label for right-click action

    [SerializeField]
    private Canvas _canvas;

    [Header("Settings")]
    [Tooltip("Offset from mouse position in screen space.\n" +
                 "Positive X moves right, negative X moves left.")]
    [SerializeField] private Vector2 _screenOffset = new Vector2(-140f, 0f);

    private void Awake()
    {
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();

        // Initialize texts
        _labelLeft.text = "Left Click to Placement";
        _labelMiddle.text = "Middle Click to Rotate";
        _labelRight.text = "Right Click to Cancel";

        // Disable raycast blocking so panel never intercepts clicks
        DisableRaycastBlocking();

        // Hide by default
        _panel.gameObject.SetActive(false);
    }

    /// <summary>
    /// Disable raycast targets so the panel never blocks input.
    /// </summary>
    private void DisableRaycastBlocking()
    {
        // Option 1: CanvasGroup
        CanvasGroup cg = _panel.gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;

        // Option 2: turn off raycastTarget on graphics (kept for clarity)
        // foreach (var g in _panel.GetComponentsInChildren<Graphic>())
        //     g.raycastTarget = false;
    }

    /// <summary>Show the instruction panel.</summary>
    public void Show() => _panel.gameObject.SetActive(true);

    /// <summary>Hide the instruction panel.</summary>
    public void Hide() => _panel.gameObject.SetActive(false);

    /// <summary>
    /// Place the panel at mouse position plus configured offset.
    /// </summary>
    /// <param name="screenPos">Mouse position in screen space.</param>
    public void UpdatePosition(Vector2 screenPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform,
            screenPos,
            _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera,
            out Vector2 localPoint
        );

        // Apply user-defined offset (left of cursor by default)
        _panel.anchoredPosition = localPoint + _screenOffset;
    }
}