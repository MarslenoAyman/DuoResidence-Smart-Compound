// DigitalTwinCamera.cs
// Top down camera for the Digital Twin mode.
//
// Controls
//   W A S D or arrow keys = move across the compound
//   Mouse scroll          = zoom in and out (changes the camera height)
//   Right click + drag    = rotate the view
//   Left click on a street light = open its dashboard
//   C = close the dashboard, L = lock or unlock light clicks
//   ESC = return to the mode selector
using UnityEngine;
using UnityEngine.InputSystem;

public class DigitalTwinCamera : MonoBehaviour
{
    // -------------------------------------------------------
    // SETTINGS (editable in the Inspector)
    // -------------------------------------------------------

    [Header("Movement Settings")]
    // How fast the camera moves with W A S D
    public float panSpeed = 30f;

    // How fast the camera zooms with the scroll wheel
    public float zoomSpeed = 20f;

    // How fast the camera turns with right click drag
    public float rotateSpeed = 120f;

    // Lowest and highest camera height
    public float minHeight = 10f;
    public float maxHeight = 200f;

    [Header("Dashboard Control")]
    // When true, left clicks on street lights are ignored (L toggles it)
    public bool clickLocked = false;

    // Scroll values smaller than this are ignored
    private const float SCROLL_DEAD_ZONE = 0.01f;


    // -------------------------------------------------------
    // RUNTIME STATE
    // -------------------------------------------------------

    // Mouse position from the last frame, used while rotating
    private Vector2 lastMousePosition;

    // True while the right mouse button is held for rotation
    private bool isRotating = false;

    // The camera on this object, used for click rays
    private Camera cam;


    // -------------------------------------------------------
    // UNITY EVENTS
    // -------------------------------------------------------

    void Start()
    {
        cam = GetComponent<Camera>();
        FreeCursor();
    }

    void OnEnable()
    {
        FreeCursor();
    }

    void Update()
    {
        HandleEscape();
        HandlePan();
        HandleZoom();
        HandleRotate();
        HandleLightClick();
        HandleDashboardKeys();
    }


    // -------------------------------------------------------
    // INPUT HANDLING
    // -------------------------------------------------------

    // ESC goes back to the mode selector screen
    void HandleEscape()
    {
        if (Keyboard.current == null) return;
        if (!Keyboard.current.escapeKey.wasPressedThisFrame) return;

        if (ModeSelector.Instance != null)
            ModeSelector.Instance.ReturnToSelector();
    }

    // W A S D or arrow keys move the camera along the ground, in the direction it faces
    void HandlePan()
    {
        if (Keyboard.current == null) return;
        Keyboard kb = Keyboard.current;

        float horizontal = 0f;
        float vertical   = 0f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    vertical   += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  vertical   -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) horizontal += 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  horizontal -= 1f;

        // Flatten the camera directions so moving never changes the height
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right   = Vector3.ProjectOnPlane(transform.right,   Vector3.up).normalized;

        transform.position += (forward * vertical + right * horizontal) * panSpeed * Time.deltaTime;
    }

    // The scroll wheel raises or lowers the camera inside the height limits
    void HandleZoom()
    {
        if (Mouse.current == null) return;

        float scroll = Mouse.current.scroll.y.ReadValue();
        if (Mathf.Abs(scroll) < SCROLL_DEAD_ZONE) return;

        float newHeight = Mathf.Clamp(transform.position.y - scroll * zoomSpeed * Time.deltaTime, minHeight, maxHeight);
        transform.position = new Vector3(transform.position.x, newHeight, transform.position.z);
    }

    // Right click + drag turns the camera: sideways around the world up axis,
    // up and down around its own side axis
    void HandleRotate()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            isRotating        = true;
            lastMousePosition = Mouse.current.position.ReadValue();
        }
        if (Mouse.current.rightButton.wasReleasedThisFrame)
            isRotating = false;

        if (!isRotating) return;

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Vector2 delta = mousePosition - lastMousePosition;
        lastMousePosition = mousePosition;

        transform.Rotate(0f, delta.x * rotateSpeed * Time.deltaTime, 0f, Space.World);
        transform.Rotate(-delta.y * rotateSpeed * Time.deltaTime, 0f, 0f, Space.Self);
    }

    // Left click on a street light opens its dashboard
    void HandleLightClick()
    {
        if (clickLocked || Mouse.current == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        // Ignore clicks while the user is rotating the view
        if (Mouse.current.rightButton.isPressed) return;

        LightDashboard.OpenForLightUnderMouse(cam != null ? cam : Camera.main);
    }

    // C closes the dashboard, L locks or unlocks light clicks
    void HandleDashboardKeys()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.cKey.wasPressedThisFrame)
            LightDashboard.CloseShared();

        if (Keyboard.current.lKey.wasPressedThisFrame)
            clickLocked = !clickLocked;
    }

    // Shows the mouse cursor and lets it move freely
    void FreeCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }
}
