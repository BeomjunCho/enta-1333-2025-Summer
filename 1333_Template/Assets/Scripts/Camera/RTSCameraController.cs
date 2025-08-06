using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class RTSCameraController : MonoBehaviour
{
    /* ------------------------------------------------------------------ */
    /*  Panning                                                            */
    /* ------------------------------------------------------------------ */
    [Header("Panning")]
    public bool useKeyboardPan = true;
    public bool useMouseDragPan = true;
    public float panSpeed = 20f;
    public float dragSpeed = 0.5f;

    /* ------------------------------------------------------------------ */
    /*  Zooming                                                            */
    /* ------------------------------------------------------------------ */
    [Header("Zooming")]
    public float scrollZoomSpeed = 20f;
    public float verticalZoomSpeed = 20f;
    public float minHeight = 10f;
    public float maxHeight = 80f;

    /* ------------------------------------------------------------------ */
    /*  Rotating                                                           */
    /* ------------------------------------------------------------------ */
    [Header("Rotating")]
    public bool useKeyboardRotate = true;
    public float initialFocusDistance = 20f;
    public float rotateSpeed = 50f;

    /* ------------------------------------------------------------------ */
    /*  Bounds                                                             */
    /* ------------------------------------------------------------------ */
    [Header("Bounds")]
    [Tooltip("Lower-left XZ world corner")]
    [SerializeField] private Vector2 _boundsMin = new(-50f, -50f);

    [Tooltip("Upper-right XZ world corner")]
    [SerializeField] private Vector2 _boundsMax = new(50f, 50f);

    /* ------------------------------------------------------------------ */
    /*  Internal                                                           */
    /* ------------------------------------------------------------------ */
    private Vector3 _pivot;   // imaginary target point

    private void Start()
    {
        _pivot = transform.position + transform.forward * initialFocusDistance;
    }

    private void Update()
    {
        /* --------- Camera-relative axes ---------- */
        Vector3 right = transform.right; right.y = 0; right.Normalize();
        Vector3 forward = transform.forward; forward.y = 0; forward.Normalize();

        Vector3 oldPos = transform.position;
        Vector3 pos = oldPos;

        /* --------- Keyboard pan ---------- */
        if (useKeyboardPan)
        {
            Vector2 input = Vector2.zero;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input.y += 1;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input.y -= 1;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input.x -= 1;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1;

            if (input.sqrMagnitude > 0.01f)
            {
                Vector3 dir = (right * input.x + forward * input.y).normalized;
                pos += dir * panSpeed * Time.deltaTime;
            }
        }

        /* --------- Mouse-drag pan (middle-button) ---------- */
        if (useMouseDragPan && Mouse.current.middleButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            Vector3 drag = (right * -delta.x + forward * -delta.y) * dragSpeed * Time.deltaTime;
            pos += drag;
        }

        /* --------- Zoom ---------- */
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > Mathf.Epsilon)
            pos += transform.forward * scroll * scrollZoomSpeed * Time.deltaTime;

        if (Keyboard.current.fKey.isPressed) pos.y -= verticalZoomSpeed * Time.deltaTime;
        if (Keyboard.current.rKey.isPressed) pos.y += verticalZoomSpeed * Time.deltaTime;

        pos.y = Mathf.Clamp(pos.y, minHeight, maxHeight);

        /* --------- Bounds clamp ---------- */
        pos.x = Mathf.Clamp(pos.x, _boundsMin.x, _boundsMax.x);
        pos.z = Mathf.Clamp(pos.z, _boundsMin.y, _boundsMax.y);

        /* --------- Apply & sync pivot ---------- */
        transform.position = pos;
        _pivot += (pos - oldPos);

        /* --------- Rotate ---------- */
        if (useKeyboardRotate)
        {
            if (Keyboard.current.qKey.isPressed)
                transform.RotateAround(_pivot, Vector3.up, rotateSpeed * Time.deltaTime);
            if (Keyboard.current.eKey.isPressed)
                transform.RotateAround(_pivot, Vector3.up, -rotateSpeed * Time.deltaTime);
        }

        transform.LookAt(_pivot, Vector3.up);
    }
}
