// LightDashboard.cs
// One shared panel that shows the live data of a single street light.
// It opens when the user left clicks a street light in any view mode
// (Digital Twin, VR Walkthrough or 3D View).
//
// The panel shows the light name, brightness, power, mode (Auto, Manual or AI)
// and battery level. From the panel the user can take manual control of the light
// with a brightness slider, or give control back to Auto.
// Clicking the same light again, the close button, or the C key closes the panel.
// On the twin side, every manual or Auto change is also sent to the real compound
// through the SyncManager.
//
// This script sits on one UI object in the scene, not on each light.
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class LightDashboard : MonoBehaviour
{
    // -------------------------------------------------------
    // SHARED ACCESS
    // -------------------------------------------------------

    // The single dashboard in the scene, so the cameras can open and close it
    public static LightDashboard Instance { get; private set; }

    // How far a mouse click can reach a street light
    private const float CLICK_RANGE = 200f;


    // -------------------------------------------------------
    // STATE
    // -------------------------------------------------------

    // True while the panel is on screen
    public bool isVisible = false;

    // The light the panel is showing right now
    private LightController currentLight;

    // Root of the panel's visual tree
    private VisualElement root;

    // Panel elements, found by name in the UI document
    private Label         lightIDLabel, brightnessLabel, wattsLabel, modeLabel, batteryLabel;
    private VisualElement brightnessBar, batteryBar;
    private Slider        brightnessSlider;
    private Button        overrideButton, autoButton, closeButton;


    // -------------------------------------------------------
    // UNITY EVENTS
    // -------------------------------------------------------

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        UIDocument uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;

        root = uiDocument.rootVisualElement;

        // The full screen root never takes clicks itself, only the panel inside it does
        root.pickingMode = PickingMode.Ignore;

        GetUIReferences();
        SetDashboardVisible(false);
    }

    // Refreshes the live values of the open light every frame
    void Update()
    {
        if (!isVisible || currentLight == null) return;

        UpdateBrightness();
        UpdatePower();
        UpdateMode();
        UpdateBattery();
    }


    // -------------------------------------------------------
    // CALLED BY THE CAMERAS
    // -------------------------------------------------------

    // Sends a ray from the mouse through the given camera.
    // If it hits a street light, the dashboard opens for that light.
    public static void OpenForLightUnderMouse(Camera cam)
    {
        if (Instance == null || cam == null || Mouse.current == null) return;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, CLICK_RANGE)) return;

        // The light script sits on the clicked object or on its parent
        LightController light = hit.collider.GetComponent<LightController>();
        if (light == null && hit.collider.transform.parent != null)
            light = hit.collider.transform.parent.GetComponent<LightController>();

        if (light != null) Instance.ShowForLight(light);
    }

    // Closes the dashboard if it is open
    public static void CloseShared()
    {
        if (Instance != null) Instance.SetDashboardVisible(false);
    }

    // Opens the panel for a light. Clicking the light that is already open closes it.
    public void ShowForLight(LightController light)
    {
        if (light == null) return;

        if (isVisible && currentLight == light)
        {
            SetDashboardVisible(false);
            return;
        }

        currentLight = light;
        if (lightIDLabel != null) lightIDLabel.text = light.gameObject.name;

        // Every new light starts with the Override button showing
        ShowAutoControls();
        SetDashboardVisible(true);
    }

    // Shows or hides the whole panel
    public void SetDashboardVisible(bool visible)
    {
        isVisible = visible;
        if (root != null) root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }


    // -------------------------------------------------------
    // SETUP
    // -------------------------------------------------------

    // Links the panel elements and connects the slider and buttons
    void GetUIReferences()
    {
        lightIDLabel     = root.Q<Label>("light-id");
        brightnessLabel  = root.Q<Label>("brightness-value");
        wattsLabel       = root.Q<Label>("watts-value");
        modeLabel        = root.Q<Label>("mode-value");
        batteryLabel     = root.Q<Label>("battery-value");
        brightnessBar    = root.Q<VisualElement>("brightness-bar-fill");
        batteryBar       = root.Q<VisualElement>("battery-bar-fill");
        brightnessSlider = root.Q<Slider>("brightness-slider");
        overrideButton   = root.Q<Button>("btn-override");
        autoButton       = root.Q<Button>("btn-auto");
        closeButton      = root.Q<Button>("btn-close");

        if (brightnessSlider != null) brightnessSlider.RegisterValueChangedCallback(OnSliderChanged);
        if (overrideButton   != null) overrideButton.clicked += OnOverrideClicked;
        if (autoButton       != null) autoButton.clicked     += OnAutoClicked;
        if (closeButton      != null) closeButton.clicked    += () => SetDashboardVisible(false);
    }


    // -------------------------------------------------------
    // LIVE VALUES
    // -------------------------------------------------------

    void UpdateBrightness()
    {
        int percent = Mathf.RoundToInt(currentLight.currentBrightness * 100f);
        if (brightnessLabel != null) brightnessLabel.text = percent + "%";
        if (brightnessBar   != null) brightnessBar.style.width = Length.Percent(percent);
    }

    void UpdatePower()
    {
        if (wattsLabel != null) wattsLabel.text = currentLight.currentWatts.ToString("F1") + " W";
    }

    // Mode text in its own color: green for Manual, purple for AI, gold for Auto
    void UpdateMode()
    {
        if (modeLabel == null) return;

        modeLabel.text = currentLight.currentMode.ToString();

        Color modeColor;
        switch (currentLight.currentMode)
        {
            case LightController.LightMode.Manual:    modeColor = CompoundColors.LightManual; break;
            case LightController.LightMode.AIControl: modeColor = CompoundColors.LightAI;     break;
            default:                                  modeColor = CompoundColors.GoldAccent;  break;
        }
        modeLabel.style.color = new StyleColor(modeColor);
    }

    // Battery bar: green above 50%, gold above 20%, red at 20% or below
    void UpdateBattery()
    {
        int percent = Mathf.RoundToInt(currentLight.batteryPercent);
        if (batteryLabel != null) batteryLabel.text = percent + "%";
        if (batteryBar == null) return;

        batteryBar.style.width = Length.Percent(percent);

        Color barColor;
        if (percent > 50)      barColor = CompoundColors.PowerLow;
        else if (percent > 20) barColor = CompoundColors.PowerMedium;
        else                   barColor = CompoundColors.PowerHigh;
        batteryBar.style.backgroundColor = new StyleColor(barColor);
    }


    // -------------------------------------------------------
    // MANUAL AND AUTO CONTROL
    // -------------------------------------------------------

    // Moving the slider sets the light brightness by hand
    void OnSliderChanged(ChangeEvent<float> evt)
    {
        if (currentLight != null)
            SetManual(evt.newValue / 100f);
    }

    // Override takes manual control and keeps the current brightness as the start value
    void OnOverrideClicked()
    {
        if (currentLight == null) return;

        SetManual(currentLight.currentBrightness);
        if (brightnessSlider != null) brightnessSlider.value = currentLight.currentBrightness * 100f;
        ShowManualControls();
    }

    // Auto gives control of the light back to the automatic system
    void OnAutoClicked()
    {
        if (currentLight != null)
        {
            currentLight.SetToAuto();
            if (SyncManager.Instance != null) SyncManager.Instance.SendAutoCommand(currentLight.gameObject.name);
        }
        ShowAutoControls();
    }

    // Sets the open light to a manual brightness (0 to 1) and shares the change
    void SetManual(float brightness)
    {
        currentLight.SetManualBrightness(brightness);
        if (SyncManager.Instance != null) SyncManager.Instance.SendOverrideCommand(currentLight.gameObject.name, brightness);
    }

    // Auto state: only the Override button is visible
    void ShowAutoControls()
    {
        SetShown(brightnessSlider, false);
        SetShown(overrideButton,   true);
        SetShown(autoButton,       false);
    }

    // Manual state: the slider and the Auto button are visible
    void ShowManualControls()
    {
        SetShown(brightnessSlider, true);
        SetShown(overrideButton,   false);
        SetShown(autoButton,       true);
    }

    // Shows or hides one panel element
    void SetShown(VisualElement element, bool shown)
    {
        if (element != null) element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
