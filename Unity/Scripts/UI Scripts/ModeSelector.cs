// ModeSelector.cs
// Start screen of the compound. It shows at startup and every time ESC is pressed.
//
// Three buttons choose the view mode:
//   Digital Twin   : top down camera, cars drive on their routes
//   VR Walkthrough : first person walk with VR controller visuals, cars parked
//   3D View        : first person walk without the controllers, cars parked
//
// Choosing a mode turns on its camera, sets the cars, and hides this screen.
// ReturnToSelector() turns every camera off, closes any open dashboard,
// and shows this screen again.
// The UIDocument always stays enabled, so its visual tree is never rebuilt.
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

public class ModeSelector : MonoBehaviour
{
    // -------------------------------------------------------
    // CAMERA OBJECTS (set in the Inspector)
    // -------------------------------------------------------

    [Header("Camera References")]
    // Top down camera for the Digital Twin mode
    public GameObject digitalTwinCamera;

    // First person camera for the VR Walkthrough mode
    public GameObject vrCamera;

    // First person camera for the 3D View mode
    [FormerlySerializedAs("playerController")]
    public GameObject view3DCamera;


    // -------------------------------------------------------
    // MODES
    // -------------------------------------------------------

    // The view modes, plus None while the start screen is showing
    public enum ViewMode { None, DigitalTwin, VR, View3D }

    // The mode that is open right now
    public static ViewMode CurrentMode = ViewMode.None;

    // Single shared instance, so the cameras can return to this screen with ESC
    public static ModeSelector Instance;

    // How the cars look in each situation
    private enum CarsDisplay
    {
        Hidden,  // start screen: no cars shown
        Parked,  // VR and 3D View: cars visible but not moving
        Driving  // Digital Twin: each car follows its route
    }

    // Sorting order that puts the start screen in front of or behind the cameras
    private const int SORT_IN_FRONT = 10;
    private const int SORT_BEHIND   = -999;

    // Background color used if the background image cannot be loaded
    private static readonly Color FallbackBackground = new Color(0.08f, 0.07f, 0.05f);


    // -------------------------------------------------------
    // UI STATE
    // -------------------------------------------------------

    private UIDocument    uiDoc;
    private VisualElement root;
    private PanelSettings panelSettings;

    // The visual tree that already has the button clicks connected,
    // so the clicks are never connected twice
    private VisualElement wiredRoot;


    // -------------------------------------------------------
    // UNITY EVENTS
    // -------------------------------------------------------

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        uiDoc         = GetComponent<UIDocument>();
        panelSettings = uiDoc.panelSettings;

        TurnOffAllCameras();
        SetCars(CarsDisplay.Hidden);
        ShowSelector();
    }


    // -------------------------------------------------------
    // MODE SWITCHING
    // -------------------------------------------------------

    public void ActivateDigitalTwinMode()
    {
        OpenMode(ViewMode.DigitalTwin, digitalTwinCamera, CarsDisplay.Driving);
    }

    public void ActivateVRMode()
    {
        OpenMode(ViewMode.VR, vrCamera, CarsDisplay.Parked);
    }

    public void Activate3DViewMode()
    {
        OpenMode(ViewMode.View3D, view3DCamera, CarsDisplay.Parked);
    }

    // Closes the current mode and shows the start screen again
    public void ReturnToSelector()
    {
        CurrentMode = ViewMode.None;
        TurnOffAllCameras();
        SetCars(CarsDisplay.Hidden);

        // An open dashboard would sit on top of the start screen and block its buttons
        LightDashboard.CloseShared();
        if (MasterDashboard.Instance != null) MasterDashboard.Instance.SetVisible(false);

        ShowSelector();
    }

    // Turns on one camera, sets the cars, and hides the start screen
    void OpenMode(ViewMode mode, GameObject cameraObject, CarsDisplay cars)
    {
        CurrentMode = mode;
        TurnOffAllCameras();
        if (cameraObject != null) cameraObject.SetActive(true);
        SetCars(cars);
        HideSelector();
    }

    void TurnOffAllCameras()
    {
        if (digitalTwinCamera != null) digitalTwinCamera.SetActive(false);
        if (vrCamera          != null) vrCamera.SetActive(false);
        if (view3DCamera      != null) view3DCamera.SetActive(false);
    }


    // -------------------------------------------------------
    // SHOW AND HIDE THE START SCREEN
    // -------------------------------------------------------

    public void ShowSelector()
    {
        if (uiDoc == null) uiDoc = GetComponent<UIDocument>();
        root = uiDoc.rootVisualElement;

        // Draw the start screen in front of the cameras and let it take clicks
        if (panelSettings != null) panelSettings.sortingOrder = SORT_IN_FRONT;
        root.style.display    = DisplayStyle.Flex;
        root.style.visibility = Visibility.Visible;
        root.pickingMode      = PickingMode.Position;

        LoadBackground();
        WireButtons();
    }

    void HideSelector()
    {
        // Move the start screen behind the cameras and stop it from taking clicks.
        // The UIDocument stays enabled, so the root stays valid for next time.
        if (panelSettings != null) panelSettings.sortingOrder = SORT_BEHIND;
        root.style.display    = DisplayStyle.None;
        root.style.visibility = Visibility.Hidden;
        root.pickingMode      = PickingMode.Ignore;
    }

    // Connects the three buttons once for each visual tree
    void WireButtons()
    {
        if (wiredRoot == root) return;
        wiredRoot = root;

        Button digitalTwinButton = root.Q<Button>("btn-digital-twin");
        Button vrButton          = root.Q<Button>("btn-vr");
        Button view3DButton      = root.Q<Button>("btn-player");

        if (digitalTwinButton != null) digitalTwinButton.RegisterCallback<ClickEvent>(e => ActivateDigitalTwinMode());
        if (vrButton          != null) vrButton.RegisterCallback<ClickEvent>(e => ActivateVRMode());
        if (view3DButton      != null)
        {
            view3DButton.text = "3D View";
            view3DButton.RegisterCallback<ClickEvent>(e => Activate3DViewMode());
        }
    }

    // Loads the background image from Resources, or uses a dark color if it is missing
    void LoadBackground()
    {
        VisualElement background = root.Q<VisualElement>("background-image");
        if (background == null) return;

        Texture2D image = Resources.Load<Texture2D>("ModeSelectorBackground");

#if UNITY_EDITOR
        // Inside the editor, also look for the image in the UI folder
        if (image == null)
            image = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/ModeSelectorBackground.png");
#endif

        if (image != null) background.style.backgroundImage = new StyleBackground(image);
        else               background.style.backgroundColor = new StyleColor(FallbackBackground);
    }


    // -------------------------------------------------------
    // CARS
    // -------------------------------------------------------

    // Shows, parks or drives every car under the "Cars" object
    void SetCars(CarsDisplay display)
    {
        GameObject carsParent = GameObject.Find("Cars");
        if (carsParent == null) return;

        foreach (Transform group in carsParent.transform)
        {
            foreach (Transform car in group)
            {
                CompoundCarDriver driver = car.GetComponent<CompoundCarDriver>();

                switch (display)
                {
                    case CarsDisplay.Hidden:
                        if (driver != null) driver.enabled = false;
                        SetCarVisible(car, false);
                        break;

                    case CarsDisplay.Parked:
                        if (driver != null) driver.enabled = false;
                        SetCarVisible(car, true);
                        break;

                    case CarsDisplay.Driving:
                        // Cars with a driver show and hide themselves on their schedule.
                        // Cars without a driver stay hidden in the Digital Twin.
                        if (driver != null) driver.enabled = true;
                        else                SetCarVisible(car, false);
                        break;
                }
            }
        }
    }

    // Shows or hides every mesh of one car
    void SetCarVisible(Transform car, bool visible)
    {
        foreach (Renderer meshRenderer in car.GetComponentsInChildren<Renderer>())
            meshRenderer.enabled = visible;
    }
}
