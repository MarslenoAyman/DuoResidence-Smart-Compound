// MasterDashboard.cs
// Main dashboard of the compound. Press TAB in any view mode to open or close it.
//
// Sections on the dashboard:
//   Time         : current compound time and lighting state, plus a speed slider (1x to 300x)
//   Zone cards   : live brightness, power and battery for the Main Streets and Sub Streets
//   Power bar    : total power used by all street lights compared to the maximum
//   Solar panels : live output of 320 panels x 400 W = 128 kW peak,
//                  following a sun curve from sunrise (6 AM) to sunset (6 PM) at 80% efficiency
//   Energy report: daily kWh generated and consumed, net energy saved, CO2 avoided,
//                  solar coverage, and the AI crowd prediction for the next 30 minutes
//
// The daily energy totals reset to zero at 06:00 every compound day.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class MasterDashboard : MonoBehaviour
{
    // The single main dashboard in the scene
    public static MasterDashboard Instance { get; private set; }

    // -------------------------------------------------------
    // SOLAR PANEL SETTINGS
    // -------------------------------------------------------

    // 40 buildings x 8 panels on each roof
    private const int PANEL_COUNT = 320;

    // Peak power of one panel in watts
    private const float PANEL_WATT_PEAK = 400f;

    // Peak power of all panels together in kW (128 kW)
    private const float PEAK_KW = (PANEL_COUNT * PANEL_WATT_PEAK) / 1000f;

    // Kilograms of CO2 avoided for every kWh not taken from the Egyptian grid
    private const float CO2_KG_PER_KWH = 0.4f;

    // Share of the panel output left after real world losses (heat, dust, wiring)
    private const float SOLAR_EFFICIENCY = 0.80f;

    // Hours when the panels start and stop producing power
    private const float SUNRISE = 6f;
    private const float SUNSET  = 18f;

    // Below this brightness a street light counts as off
    private const float LIGHT_ON_THRESHOLD = 0.01f;

    // Below this output (kW) the panels count as offline
    private const float SOLAR_ON_THRESHOLD = 0.1f;


    // -------------------------------------------------------
    // COLORS USED ONLY BY THIS DASHBOARD
    // (shared colors come from CompoundColors)
    // -------------------------------------------------------

    // Solar status badge when the panels are producing power
    private static readonly Color SolarOnColor = new Color(0.47f, 0.85f, 0.31f);

    // Solar output bar: orange when low, yellow when medium, pale yellow when high
    private static readonly Color SolarLowColor    = new Color(0.95f, 0.55f, 0.10f);
    private static readonly Color SolarMediumColor = new Color(0.98f, 0.82f, 0.20f);
    private static readonly Color SolarHighColor   = new Color(1.00f, 0.97f, 0.70f);


    // -------------------------------------------------------
    // CONNECTED SYSTEMS
    // -------------------------------------------------------

    // The compound clock and the AI crowd predictor, found at start
    private TimeManager    timeManager;
    private CrowdPredictor crowdPredictor;

    // Street lights grouped by zone, plus one list with all of them
    private readonly List<LightController> mainStreetLights = new List<LightController>();
    private readonly List<LightController> subStreetLights  = new List<LightController>();
    private readonly List<LightController> allLights        = new List<LightController>();

    // Power used when every light is at 100%, used for the power bar
    private float maxPossibleWatts = 0f;

    // Daily energy totals in watt hours
    private float solarGeneratedWh = 0f;
    private float energyConsumedWh = 0f;


    // -------------------------------------------------------
    // UI ELEMENTS (found by name in the UI document)
    // -------------------------------------------------------

    private VisualElement root;
    private bool isVisible = false;

    // Time section
    private Label  timeLabelUI, timeStateLabelUI;
    private Slider speedSlider;
    private Label  speedValue;

    // Zone cards
    private Label         mainBrightness, mainWatts, mainBattery, mainBadge;
    private VisualElement mainBar;
    private Label         subBrightness, subWatts, subBattery, subBadge;
    private VisualElement subBar;

    // Power section
    private VisualElement powerBarFill;
    private Label         powerBarValue, savingLabel, activeLightsValue;

    // Solar section
    private Label         solarStatus, solarNowKw, solarPeakKw;
    private VisualElement solarBarFill;

    // Energy report
    private Label energyGeneratedLabel, energyConsumedLabel;
    private Label energySavedLabel, energyCo2Label;
    private Label energyEfficiencyLabel, aiPredictionLabel;


    // -------------------------------------------------------
    // UNITY EVENTS
    // -------------------------------------------------------

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        root           = GetComponent<UIDocument>().rootVisualElement;

        // The full screen root never takes clicks itself, only the panels inside it do
        root.pickingMode = PickingMode.Ignore;

        timeManager    = FindAnyObjectByType<TimeManager>();
        crowdPredictor = FindAnyObjectByType<CrowdPredictor>();

        CollectLightControllers();
        foreach (LightController light in allLights)
            maxPossibleWatts += light.maxWatts;

        GetUIReferences();
        SetupSpeedSlider();
        SetVisible(false);

        if (solarPeakKw != null) solarPeakKw.text = PEAK_KW.ToString("F1") + " kW";

        // Start a new energy day every morning
        if (timeManager != null) timeManager.OnDaytime.AddListener(ResetDailyEnergy);
    }

    void Update()
    {
        // TAB opens or closes the dashboard
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            SetVisible(!isVisible);

        // Energy is counted all the time, even while the dashboard is closed
        AccumulateEnergy();

        // The screen is only refreshed while the dashboard is open
        if (!isVisible) return;
        UpdateTimeDisplay();
        UpdateZoneCards();
        UpdatePowerBar();
        UpdateActiveLights();
        UpdateSolarPanel();
        UpdateEnergyReport();
    }


    // -------------------------------------------------------
    // SETUP
    // -------------------------------------------------------

    // Finds every street light, sorted into Main Streets and Sub Streets
    void CollectLightControllers()
    {
        LightController.FindAllByZone(mainStreetLights, subStreetLights);
        allLights.AddRange(mainStreetLights);
        allLights.AddRange(subStreetLights);
    }

    // Links every label and bar to its element in the UI document
    void GetUIReferences()
    {
        timeLabelUI           = root.Q<Label>("time-label");
        timeStateLabelUI      = root.Q<Label>("time-state-label");
        speedSlider           = root.Q<Slider>("speed-slider");
        speedValue            = root.Q<Label>("speed-value");

        mainBrightness        = root.Q<Label>("main-brightness");
        mainWatts             = root.Q<Label>("main-watts");
        mainBattery           = root.Q<Label>("main-battery");
        mainBadge             = root.Q<Label>("badge-main");
        mainBar               = root.Q<VisualElement>("main-bar");
        subBrightness         = root.Q<Label>("sub-brightness");
        subWatts              = root.Q<Label>("sub-watts");
        subBattery            = root.Q<Label>("sub-battery");
        subBadge              = root.Q<Label>("badge-sub");
        subBar                = root.Q<VisualElement>("sub-bar");

        powerBarFill          = root.Q<VisualElement>("power-bar-fill");
        powerBarValue         = root.Q<Label>("power-bar-value");
        savingLabel           = root.Q<Label>("saving-label");
        activeLightsValue     = root.Q<Label>("active-lights-value");

        solarStatus           = root.Q<Label>("solar-status");
        solarBarFill          = root.Q<VisualElement>("solar-bar-fill");
        solarNowKw            = root.Q<Label>("solar-output-kw");
        solarPeakKw           = root.Q<Label>("solar-peak-kw");

        energyGeneratedLabel  = root.Q<Label>("energy-generated");
        energyConsumedLabel   = root.Q<Label>("energy-consumed");
        energySavedLabel      = root.Q<Label>("energy-saved");
        energyCo2Label        = root.Q<Label>("energy-co2");
        energyEfficiencyLabel = root.Q<Label>("energy-efficiency");
        aiPredictionLabel     = root.Q<Label>("ai-prediction");
    }

    // Connects the speed slider to the compound clock
    void SetupSpeedSlider()
    {
        if (speedSlider == null || timeManager == null) return;

        speedSlider.value = timeManager.timeSpeed;
        UpdateSpeedLabel(timeManager.timeSpeed);

        // Moving the slider changes the clock speed right away
        speedSlider.RegisterValueChangedCallback(evt =>
        {
            float speed = Mathf.Round(evt.newValue);
            if (timeManager != null) timeManager.timeSpeed = speed;
            UpdateSpeedLabel(speed);
        });
    }


    // -------------------------------------------------------
    // TIME SECTION
    // -------------------------------------------------------

    void UpdateTimeDisplay()
    {
        if (timeManager == null) return;
        if (timeLabelUI      != null) timeLabelUI.text      = timeManager.currentTimeString;
        if (timeStateLabelUI != null) timeStateLabelUI.text = timeManager.GetCurrentStateName();
    }

    void UpdateSpeedLabel(float speed)
    {
        if (speedValue != null) speedValue.text = Mathf.RoundToInt(speed) + "x";
    }


    // -------------------------------------------------------
    // ZONE CARDS
    // -------------------------------------------------------

    // Shows the average brightness, total power, average battery and mode of each zone
    void UpdateZoneCards()
    {
        UpdateZoneCard(mainStreetLights, mainBrightness, mainWatts, mainBattery, mainBar, mainBadge);
        UpdateZoneCard(subStreetLights,  subBrightness,  subWatts,  subBattery,  subBar,  subBadge);
    }

    // Fills one zone card from the lights of that zone
    void UpdateZoneCard(List<LightController> lights, Label brightnessLabel, Label wattsLabel,
                        Label batteryLabel, VisualElement bar, Label badge)
    {
        CalculateZoneStats(lights, out float brightness, out float watts, out float battery,
                           out LightController.LightMode mode);

        if (brightnessLabel != null) brightnessLabel.text = Mathf.RoundToInt(brightness * 100f) + "%";
        if (wattsLabel      != null) wattsLabel.text      = watts.ToString("F0") + " W";
        if (batteryLabel    != null) batteryLabel.text    = Mathf.RoundToInt(battery) + "%";
        if (bar             != null) bar.style.width      = Length.Percent(brightness * 100f);
        UpdateBadge(badge, mode, brightness);
    }

    // Works out the zone averages and its main mode:
    // AI if any light in the zone is under AI control, Manual if more than half are manual
    void CalculateZoneStats(List<LightController> lights,
        out float avgBrightness, out float totalWatts, out float avgBattery,
        out LightController.LightMode dominantMode)
    {
        avgBrightness = 0f;
        totalWatts    = 0f;
        avgBattery    = 0f;
        dominantMode  = LightController.LightMode.Auto;
        if (lights.Count == 0) return;

        int manualCount = 0;
        foreach (LightController light in lights)
        {
            avgBrightness += light.currentBrightness;
            totalWatts    += light.currentWatts;
            avgBattery    += light.batteryPercent;

            if (light.currentMode == LightController.LightMode.Manual)    manualCount++;
            if (light.currentMode == LightController.LightMode.AIControl) dominantMode = LightController.LightMode.AIControl;
        }

        avgBrightness /= lights.Count;
        avgBattery    /= lights.Count;
        if (manualCount > lights.Count / 2) dominantMode = LightController.LightMode.Manual;
    }

    // Sets the zone badge text and color: AI, MANUAL, OFF or AUTO
    void UpdateBadge(Label badge, LightController.LightMode mode, float brightness)
    {
        if (badge == null) return;

        bool aiRunning = crowdPredictor != null && crowdPredictor.aiActive;

        if (aiRunning && mode == LightController.LightMode.AIControl)
            SetBadge(badge, "AI", CompoundColors.LightAI, 0.15f);
        else if (mode == LightController.LightMode.Manual)
            SetBadge(badge, "MANUAL", CompoundColors.LightManual, 0.15f);
        else if (brightness <= LIGHT_ON_THRESHOLD)
            SetBadge(badge, "OFF", CompoundColors.UITextMuted, 0.12f);
        else
            SetBadge(badge, "AUTO", CompoundColors.GoldAccent, 0.15f);
    }

    // Writes the badge text, with the text in full color and the background faded
    void SetBadge(Label badge, string text, Color color, float backgroundAlpha)
    {
        badge.text = text;
        badge.style.color           = new StyleColor(color);
        badge.style.backgroundColor = new StyleColor(new Color(color.r, color.g, color.b, backgroundAlpha));
    }


    // -------------------------------------------------------
    // POWER SECTION
    // -------------------------------------------------------

    // Fills the power bar: green when low, gold when medium, red when high
    void UpdatePowerBar()
    {
        float totalWatts = GetTotalWatts();
        float percent = maxPossibleWatts > 0f ? (totalWatts / maxPossibleWatts) * 100f : 0f;

        if (powerBarFill != null)
        {
            powerBarFill.style.width = Length.Percent(percent);
            Color barColor = maxPossibleWatts > 0f
                ? CompoundColors.GetPowerColor(totalWatts, maxPossibleWatts)
                : CompoundColors.PowerLow;
            powerBarFill.style.backgroundColor = new StyleColor(barColor);
        }

        if (powerBarValue != null) powerBarValue.text = totalWatts.ToString("F0") + " W";
        if (savingLabel   != null) savingLabel.text   = "Saving " + (maxPossibleWatts - totalWatts).ToString("F0") + " W";
    }

    // Shows how many street lights are on out of all of them
    void UpdateActiveLights()
    {
        int activeCount = 0;
        foreach (LightController light in allLights)
            if (light.currentBrightness > LIGHT_ON_THRESHOLD) activeCount++;

        if (activeLightsValue != null) activeLightsValue.text = activeCount + " / " + allLights.Count;
    }

    // Adds up the power used by every street light right now
    float GetTotalWatts()
    {
        float total = 0f;
        foreach (LightController light in allLights)
            total += light.currentWatts;
        return total;
    }


    // -------------------------------------------------------
    // SOLAR SECTION
    // -------------------------------------------------------

    // Solar output follows a sun curve: zero at sunrise and sunset, highest at noon
    //   output = peak x sin(PI x day progress) x efficiency
    float GetSolarOutputKW()
    {
        if (timeManager == null) return 0f;

        float hour = timeManager.GetCurrentHour();
        if (hour < SUNRISE || hour >= SUNSET) return 0f;

        // 0 at sunrise, 0.5 at noon, 1 at sunset
        float dayProgress = (hour - SUNRISE) / (SUNSET - SUNRISE);
        return PEAK_KW * Mathf.Sin(Mathf.PI * dayProgress) * SOLAR_EFFICIENCY;
    }

    // Updates the solar status badge, output bar and kW values
    void UpdateSolarPanel()
    {
        float outputKw = GetSolarOutputKW();
        float percent  = (outputKw / PEAK_KW) * 100f;
        bool  isOn     = outputKw > SOLAR_ON_THRESHOLD;

        if (solarStatus != null)
        {
            solarStatus.text = isOn ? "GENERATING" : "OFFLINE";
            Color statusColor = isOn ? SolarOnColor : CompoundColors.UITextMuted;
            solarStatus.style.color           = new StyleColor(statusColor);
            solarStatus.style.backgroundColor = new StyleColor(new Color(statusColor.r, statusColor.g, statusColor.b, 0.15f));

            // Same faded color on all four borders
            StyleColor border = new StyleColor(new Color(statusColor.r, statusColor.g, statusColor.b, 0.4f));
            solarStatus.style.borderTopColor    = border;
            solarStatus.style.borderRightColor  = border;
            solarStatus.style.borderBottomColor = border;
            solarStatus.style.borderLeftColor   = border;
        }

        if (solarBarFill != null)
        {
            solarBarFill.style.width = Length.Percent(percent);
            Color barColor = percent < 30f ? SolarLowColor : percent < 70f ? SolarMediumColor : SolarHighColor;
            solarBarFill.style.backgroundColor = new StyleColor(barColor);
        }

        if (solarNowKw  != null) solarNowKw.text  = outputKw.ToString("F1") + " kW";
        if (solarPeakKw != null) solarPeakKw.text = PEAK_KW.ToString("F1") + " kW";
    }


    // -------------------------------------------------------
    // ENERGY REPORT
    // -------------------------------------------------------

    // Adds the energy produced and used during this frame to the daily totals
    void AccumulateEnergy()
    {
        if (timeManager == null) return;

        // How many compound hours passed this frame, at the current clock speed
        float compoundHours = (Time.deltaTime / 3600f) * timeManager.timeSpeed;

        solarGeneratedWh += GetSolarOutputKW() * 1000f * compoundHours;
        energyConsumedWh += GetTotalWatts() * compoundHours;
    }

    // Shows the daily energy totals and the AI prediction
    void UpdateEnergyReport()
    {
        float generatedKWh = solarGeneratedWh / 1000f;
        float consumedKWh  = energyConsumedWh / 1000f;
        float netSavedKWh  = Mathf.Max(0f, generatedKWh - consumedKWh);
        float co2SavedKg   = netSavedKWh * CO2_KG_PER_KWH;

        // Share of the used energy that the solar panels covered
        float coverage = consumedKWh > 0f
            ? Mathf.Clamp01(generatedKWh / consumedKWh) * 100f
            : (solarGeneratedWh > 0f ? 100f : 0f);

        if (energyGeneratedLabel  != null) energyGeneratedLabel.text  = generatedKWh.ToString("F2") + " kWh";
        if (energyConsumedLabel   != null) energyConsumedLabel.text   = consumedKWh.ToString("F2") + " kWh";
        if (energySavedLabel      != null) energySavedLabel.text      = netSavedKWh.ToString("F2") + " kWh";
        if (energyCo2Label        != null) energyCo2Label.text        = co2SavedKg.ToString("F1") + " kg";
        if (energyEfficiencyLabel != null) energyEfficiencyLabel.text = Mathf.RoundToInt(coverage) + "%";

        if (aiPredictionLabel != null && crowdPredictor != null)
            aiPredictionLabel.text = crowdPredictor.GetPredictionText("Main Streets") ?? "Learning...";
    }

    // Clears the daily totals at the start of a new day
    void ResetDailyEnergy()
    {
        solarGeneratedWh = 0f;
        energyConsumedWh = 0f;
    }


    // -------------------------------------------------------
    // VISIBILITY
    // -------------------------------------------------------

    // Shows or hides the whole dashboard
    public void SetVisible(bool visible)
    {
        isVisible = visible;
        if (root != null) root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
