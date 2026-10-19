// VRControllerVisual.cs
// Shows two VR hand controllers in front of the VR Walkthrough camera,
// so the walk looks and feels like a VR headset view.
//
// The controllers are built at runtime from Unity primitives (cylinders and cubes),
// so no extra 3D files are needed. They move up and down gently while idle,
// and lean forward while the user walks with W A S D.
// They appear and disappear together with the VR camera.
using UnityEngine;
using UnityEngine.InputSystem;

public class VRControllerVisual : MonoBehaviour
{
    // -------------------------------------------------------
    // SETTINGS (editable in the Inspector)
    // -------------------------------------------------------

    [Header("Controller Settings")]
    // How far in front of the camera the controllers float
    public float controllerDistance = 0.45f;

    // How far left and right of the center each controller sits
    public float controllerSideOffset = 0.22f;

    // How far below the camera center the controllers sit (negative = lower)
    public float controllerDownOffset = -0.22f;

    // Speed and height of the gentle idle movement
    public float bobSpeed  = 1.2f;
    public float bobAmount = 0.005f;

    // Forward lean angle while walking
    private const float WALK_LEAN_ANGLE = 8f;

    // Each controller is turned slightly inward (Y) and tilted (Z)
    private const float INWARD_TURN = 8f;
    private const float SIDE_TILT   = 5f;

    // Controller part colors
    private static readonly Color BodyColor    = new Color(0.18f, 0.18f, 0.20f);
    private static readonly Color StickColor   = new Color(0.25f, 0.25f, 0.28f);
    private static readonly Color TriggerColor = new Color(0.15f, 0.15f, 0.18f);
    private static readonly Color GripColor    = new Color(0.24f, 0.48f, 0.80f);


    // -------------------------------------------------------
    // RUNTIME STATE
    // -------------------------------------------------------

    private GameObject leftController;
    private GameObject rightController;

    // Time counter that drives the idle movement
    private float bobTimer = 0f;


    // -------------------------------------------------------
    // UNITY EVENTS
    // -------------------------------------------------------

    void Start()
    {
        leftController  = BuildController("VR_LeftController");
        rightController = BuildController("VR_RightController");
    }

    // The controllers follow the camera on and off
    void OnEnable()  { SetControllersActive(true); }
    void OnDisable() { SetControllersActive(false); }

    void OnDestroy()
    {
        if (leftController  != null) Destroy(leftController);
        if (rightController != null) Destroy(rightController);
    }

    // Places both controllers in front of the camera every frame
    void Update()
    {
        if (leftController == null || rightController == null) return;

        // Idle movement: the two controllers move in opposite directions
        bobTimer += Time.deltaTime * bobSpeed;
        float bob = Mathf.Sin(bobTimer) * bobAmount;

        float lean = IsWalking() ? WALK_LEAN_ANGLE : 0f;

        leftController.transform.localPosition = new Vector3(-controllerSideOffset, controllerDownOffset + bob, controllerDistance);
        leftController.transform.localRotation = Quaternion.Euler(lean, -INWARD_TURN, -SIDE_TILT);

        rightController.transform.localPosition = new Vector3(controllerSideOffset, controllerDownOffset - bob, controllerDistance);
        rightController.transform.localRotation = Quaternion.Euler(lean, INWARD_TURN, SIDE_TILT);
    }


    // -------------------------------------------------------
    // BUILDING THE CONTROLLERS
    // -------------------------------------------------------

    // Builds one controller from four simple parts under an empty parent
    GameObject BuildController(string controllerName)
    {
        GameObject controller = new GameObject(controllerName);
        controller.transform.SetParent(transform, false);

        // Body: a tilted cylinder for the handle
        AddPart(controller, "Body", PrimitiveType.Cylinder, Vector3.zero,
                new Vector3(0.045f, 0.075f, 0.045f), new Vector3(30f, 0f, 0f), BodyColor, false);

        // Thumbstick on top
        AddPart(controller, "Thumbstick", PrimitiveType.Cylinder, new Vector3(0f, 0.06f, 0.02f),
                new Vector3(0.018f, 0.008f, 0.018f), Vector3.zero, StickColor, false);

        // Trigger button at the front
        AddPart(controller, "Trigger", PrimitiveType.Cube, new Vector3(0f, 0f, 0.065f),
                new Vector3(0.030f, 0.020f, 0.025f), new Vector3(40f, 0f, 0f), TriggerColor, false);

        // Glowing blue ring at the bottom
        AddPart(controller, "Grip", PrimitiveType.Cylinder, new Vector3(0f, -0.065f, 0f),
                new Vector3(0.040f, 0.006f, 0.040f), Vector3.zero, GripColor, true);

        return controller;
    }

    // Creates one primitive part, places it, colors it, and removes its collider
    // so it never blocks clicks on the street lights
    void AddPart(GameObject parent, string partName, PrimitiveType shape, Vector3 position,
                 Vector3 scale, Vector3 rotation, Color color, bool glow)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.name = partName;
        part.transform.SetParent(parent.transform, false);
        part.transform.localPosition = position;
        part.transform.localScale    = scale;
        part.transform.localRotation = Quaternion.Euler(rotation);

        Destroy(part.GetComponent<Collider>());
        SetColor(part, color, glow);
    }

    // Gives a part its own URP material, with a soft glow when asked
    void SetColor(GameObject part, Color color, bool glow)
    {
        Renderer partRenderer = part.GetComponent<Renderer>();
        if (partRenderer == null) return;

        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", color);

        if (glow)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.4f);
        }

        partRenderer.material = material;
    }


    // -------------------------------------------------------
    // HELPERS
    // -------------------------------------------------------

    // True while any of the W A S D keys is held
    bool IsWalking()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return false;
        return kb.wKey.isPressed || kb.sKey.isPressed || kb.aKey.isPressed || kb.dKey.isPressed;
    }

    void SetControllersActive(bool active)
    {
        if (leftController  != null) leftController.SetActive(active);
        if (rightController != null) rightController.SetActive(active);
    }
}
