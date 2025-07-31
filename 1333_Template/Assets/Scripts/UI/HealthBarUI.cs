using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple world-space health bar: look at camera and update fill.
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private Image _fill = null;

    private Camera _cam;

    private void Awake()
    {
        _cam = Camera.main;
    }

    private void LateUpdate()
    {
        // billboard ?keep the bar facing the camera
        transform.rotation = Quaternion.LookRotation(
            transform.position - _cam.transform.position, Vector3.up);
    }

    /// <summary>
    /// 0 to 1 value, called by the owner object when HP changes.
    /// </summary>
    public void SetRatio(float r)
    {
        if (_fill == null) return;

        _fill.fillAmount = Mathf.Clamp01(r);
        _fill.color = Color.Lerp(Color.red, Color.green, _fill.fillAmount);
    }
}
