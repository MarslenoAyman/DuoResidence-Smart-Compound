// WalkthroughCamera.cs
// First person fly camera used by two view modes:
//   VR Walkthrough : on "VR Camera", together with VRControllerVisual
//   3D View        : on "Player", without the controller visuals
// The camera starts outside the East gate, looking at the DuoResidence sign.
// Once it passes through the gate, the compound walls keep it inside.
//
// Controls
//   W A S D = move, Q = up, E = down, Shift = fast
//   Right click + drag = look around
//   Left click on a street light = open its dashboard
//   C = close the dashboard, L = lock or unlock light clicks
//   ESC = return to the mode selector
using UnityEngine;
using UnityEngine.InputSystem;

public class WalkthroughCamera : MonoBehaviour
{
    // -------------------------------------------------------
    // SETTINGS (editable in the Inspector)
    // -------------------------------------------------------

    [Header("Movement")]
    // Normal flying speed in meters per second
    public float flySpeed = 12f;

    // Speed multiplier while Shift is held
    public float fastMultiplier = 2.5f;

    [Header("Look")]
    // How fast the view turns when the mouse moves
    public float mouseSensitivity = 1.8f;

    [Header("Dashboard")]
    // When true, left clicks on street lights are ignored
    public bool clickLocked = false;


    // -------------------------------------------------------
    // COMPOUND LIMITS
    // -------------------------------------------------------

    // Inner faces of the compound walls
    // (the walls stand at ±105.15 on X and ±159.15 on Z)
    private const float INNER_MIN_X = -104.5f;
    private const float INNER_MAX_X =  104.5f;
    private const float INNER_MIN_Z = -158.5f;
    private const float INNER_MAX_Z =  158.5f;

    // Lowest and highest height the camera can fly at
    private const float INNER_MIN_Y = 0.5f;
    private const float INNER_MAX_Y = 50.0f;

    // Passing this X value means the camera went through the gate
    private const float GATE_ENTRY_X = 103f;

    // Before entering, the camera must stay inside the gate lane (Z = -5 to +5)
    private const float GATE_LANE_HALF_WIDTH = 5f;

    // Highest up or down look angle, so the view never flips over
    private const float MAX_PITCH = 85f;


    // -------------------------------------------------------
    // RUNTIME STATE
    // -------------------------------------------------------

    // True once the camera has passed through the gate
    private bool hasEnteredCompound = false;

    // The camera used for click rays (on this object or a child)
    private Camera cam;

    // Current turn angles of the view
    private float yaw;
    private float pitch;


    // -------------------------------------------------------
    // UNITY EVENTS
    // -------------------------------------------------------

    void Start()
    {
        // Find the camera on this object or on one of its children
        cam = GetComponentInChildren<Camera>(true);
        SyncAngles();
        FreeCursor();
    }

    void OnEnable()
    {
        // Every time this view mode opens, the walk starts again from the gate
        hasEnteredCompound = false;
        SyncAngles();
        FreeCursor();
    }

    void OnDisable()
    {
        FreeCursor();
    }

    void Update()
    {
        // Each frame: check keys, turn the view, move, then handle light clicks
        HandleEscape();
        HandleLook();
        HandleMovement();
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

    // Right click + drag turns the view
    void HandleLook()
    {
        if (Mouse.current == null) return;
        if (!Mouse.current.rightButton.isPressed) return;

        Vector2 delta = Mouse.current.delta.ReadValue();
        yaw   += delta.x * mouseSensitivity;
        pitch -= delta.y * mouseSensitivity;
        pitch  = Mathf.Clamp(pitch, -MAX_PITCH, MAX_PITCH);
        transform.eulerAngles = new Vector3(pitch, yaw, 0f);
    }

    // W A S D Q E move the camera, then the compound limits are applied
    void HandleMovement()
    {
        if (Keyboard.current == null) return;

        Vector3 direction = ReadMoveDirection(Keyboard.current);
        if (direction == Vector3.zero) return;

        // Shift makes the camera fly faster
        float speed = flySpeed;
        if (Keyboard.current.leftShiftKey.isPressed) speed *= fastMultiplier;

        Vector3 nextPosition = transform.position + direction.normalized * speed * Time.deltaTime;
        transform.position = ApplyCompoundLimits(nextPosition);
    }

    // Left click on a street light opens its dashboard
    void HandleLightClick()
    {
        if (clickLocked || Mouse.current == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        // Ignore clicks while the user is turning the view
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


    // -------------------------------------------------------
    // HELPERS
    // -------------------------------------------------------

    // Builds the move direction from the pressed keys
    Vector3 ReadMoveDirection(Keyboard kb)
    {
        Vector3 direction = Vector3.zero;
        if (kb.wKey.isPressed) direction += transform.forward;
        if (kb.sKey.isPressed) direction -= transform.forward;
        if (kb.dKey.isPressed) direction += transform.right;
        if (kb.aKey.isPressed) direction -= transform.right;
        if (kb.qKey.isPressed) direction += Vector3.up;
        if (kb.eKey.isPressed) direction -= Vector3.up;
        return direction;
    }

    // Keeps the camera inside the gate lane before entering,
    // and inside the compound walls after entering
    Vector3 ApplyCompoundLimits(Vector3 position)
    {
        // The first time the camera passes the gate, the wall limits switch on
        if (!hasEnteredCompound && position.x < GATE_ENTRY_X)
            hasEnteredCompound = true;

        position.y = Mathf.Clamp(position.y, INNER_MIN_Y, INNER_MAX_Y);

        if (hasEnteredCompound)
        {
            position.x = Mathf.Clamp(position.x, INNER_MIN_X, INNER_MAX_X);
            position.z = Mathf.Clamp(position.z, INNER_MIN_Z, INNER_MAX_Z);
        }
        else
        {
            position.z = Mathf.Clamp(position.z, -GATE_LANE_HALF_WIDTH, GATE_LANE_HALF_WIDTH);
        }

        return position;
    }

    // Reads the current rotation so turning continues smoothly
    void SyncAngles()
    {
        yaw   = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
        if (pitch > 180f) pitch -= 360f;
    }

    // Shows the mouse cursor and lets it move freely
    void FreeCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }
}
