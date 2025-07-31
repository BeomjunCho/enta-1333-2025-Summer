using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Changes target Image sprite when the pointer enters/exits this RectTransform area.
/// Works on Screen Space Overlay without per-frame Update.
/// </summary>
public class HoverZoneSpriteChanger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Target")]
    [SerializeField] private Image _targetImage = null;

    [Header("Sprites")]
    [SerializeField] private Sprite _normalSprite = null;
    [SerializeField] private Sprite _hoverSprite = null;

    /// <summary>
    /// Reset sprite to normal every time this object is enabled.
    /// </summary>
    private void OnEnable()
    {
        SetNormal();
    }

    /// <summary>
    /// Called by EventSystem when pointer enters this RectTransform.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_targetImage != null && _hoverSprite != null)
            _targetImage.sprite = _hoverSprite;
    }

    /// <summary>
    /// Called by EventSystem when pointer exits this RectTransform.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        SetNormal();
    }

    private void SetNormal()
    {
        if (_targetImage != null && _normalSprite != null)
            _targetImage.sprite = _normalSprite;
    }
}
