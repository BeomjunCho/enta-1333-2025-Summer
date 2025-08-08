using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FMODUnity;

/// <summary>
/// Plays FMOD SFX on Hover / Click and, if attached to a child object,
/// forwards the click to the first parent Button so its onClick still fires.
/// </summary>
public class ButtonFmodSfx : MonoBehaviour,
                             IPointerEnterHandler,
                             IPointerExitHandler,
                             IPointerClickHandler
{
    /* ------------------------------------------------------------------ */
    /*  Inspector                                                         */
    /* ------------------------------------------------------------------ */

    [Header("FMOD Events")]
    [SerializeField] private EventReference _hoverEvent;
    [SerializeField] private EventReference _clickEvent;

    [Header("Forward Settings")]
    [Tooltip("If true, click is forwarded to the first parent Button.")]
    [SerializeField] private bool _forwardClickToParent = true;

    /* ------------------------------------------------------------------ */
    /*  Cache                                                             */
    /* ------------------------------------------------------------------ */

    private bool _isHovering;
    private Button _parentButton;

    /* ============================ Unity ============================== */

    private void Awake()
    {
        if (_forwardClickToParent)
            _parentButton = GetComponentInParent<Button>();
    }

    /* ====================== EventSystem hooks ======================== */

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_isHovering) return;
        _isHovering = true;

        if (!_hoverEvent.IsNull)
            AudioManager.Instance?.PlaySfx2D(_hoverEvent);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovering = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_clickEvent.IsNull)
            AudioManager.Instance?.PlaySfx2D(_clickEvent);

        if (_forwardClickToParent && _parentButton != null)
            _parentButton.onClick.Invoke(); // trigger original Button logic
    }
}
