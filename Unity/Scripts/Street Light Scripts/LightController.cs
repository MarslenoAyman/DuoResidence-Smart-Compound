// LightController.cs
// The brain of each street light (one script on each of the 120 lights).
// The LDR sensor on the same pole decides day and night: while it reads daylight
// the light is off and the solar panel charges the battery (about 06:25 to 18:15).
// In the dark, the light follows the compound clock (TimeManager):
//   18:00 to 20:00  rises gradually toward 80% (once the LDR reads dark)
//   20:00 to 02:00  80% normal, 100% when the zone is crowded (sensor or AI)
//   02:00 to 03:00  lowers gradually from 80% to 20%
//   03:00 to sunrise 20% save mode, motion raises it to 40% for 30 seconds
// It also tracks the battery level and power use, and supports manual control
// from the light dashboard.
using System.Collections.Generic;
using UnityEngine;

public class LightController : MonoBehaviour
{
    // -------------------------------------------------------
    // LIGHT MODE ENUM
    // The possible states this light can be in
    // -------------------------------------------------------

    public enum LightMode
    {
        Auto,      // System is controlling the light automatically
        Manual,    // User overrode the light from the dashboard
        AIControl  // AI prediction system is controlling this light
    }


    // -------------------------------------------------------
    // LIVE DATA (visible in Inspector, read only where noted)
    // -------------------------------------------------------

    [Header("Live Data")]

    // Current brightness from 0 (off) to 1 (full), read only, set by system
    [Range(0f, 1f)]
    public float currentBrightness = 0f;

    // Current power consumption calculated automatically, not editable
    [HideInInspector]
    public float currentWatts = 0f;

    // Maximum watts this light can consume at 100% brightness
    // Based on real LED street light power ratings
    public float maxWatts = 40f;

    // Current mode this light is in
    public LightMode currentMode = LightMode.Auto;

    // Seconds the light stays at 40% after motion, before going back to 20%
    public float motionHoldSeconds = 30f;


    // -------------------------------------------------------
    // SOLAR PANEL SETTINGS
    // Values match real world solar panel behavior
    // -------------------------------------------------------

    [Header("Solar Panel (real world values)")]

    // Battery charge percentage (0 to 100)
    [Range(0f, 100f)]
    public float batteryPercent = 100f;

    // Battery charge per real minute of daylight, in percent
    // A 10 W panel at peak sun puts about 20 Wh per hour into a 50 Wh battery:
    // 40% per hour = 0.667% per minute
    public float chargeRatePerRealMinute = 0.667f;

    // Battery drain per watt used per real minute, in percent
    // A 40 W light on a 50 Wh battery uses 80% per hour = 1.33% per minute,
    // and 1.33 / 40 = 0.033% per watt per minute
    public float drainRatePerWattPerRealMinute = 0.033f;

    // Below this battery level the light drops to 10% to protect the battery
    public float lowBatteryThreshold = 15f;


    // -------------------------------------------------------
    // BRIGHTNESS CONSTANTS
    // These define the exact brightness for each state
    // -------------------------------------------------------

    // Brightness during a normal night (8 PM to 2 AM)
    private const float BRIGHTNESS_NIGHT        = 0.80f; // 80%

    // Brightness when zone is crowded during night
    private const float BRIGHTNESS_CROWDED      = 1.00f; // 100%

    // Brightness in save mode (3 AM to 6 AM)
    private const float BRIGHTNESS_SAVE         = 0.20f; // 20%

    // Brightness when motion is detected during save mode
    private const float BRIGHTNESS_MOTION       = 0.40f; // 40% (save mode + motion detected)

    // Starting brightness of sunset transition
    private const float BRIGHTNESS_SUNSET_START = 0.00f; // 0% (starts dark, rises to 80% by 8pm)

    // Ending brightness of sunset transition
    private const float BRIGHTNESS_SUNSET_END   = 0.80f; // 80%

    // Brightness used to protect a nearly empty battery
    private const float BRIGHTNESS_LOW_BATTERY  = 0.10f; // 10%

    // How quickly the light moves toward its target brightness
    private const float FADE_SPEED = 0.5f;

    // Unity light intensity at 100% brightness
    private const float MAX_LIGHT_INTENSITY = 5f;

    // Below this brightness the Unity light is switched off
    private const float LIGHT_OFF_THRESHOLD = 0.01f;


    // -------------------------------------------------------
    // PRIVATE STATE VARIABLES
    // -------------------------------------------------------

    // The actual Unity Light component on this object or its children
    private Light lightComponent;

    // Reference to the TimeManager
    private TimeManager timeManager;

    // The brightness we are smoothly moving toward
    private float targetBrightness = 0f;

    // Whether motion has been detected near this light
    private bool motionDetected = false;

    // Countdown timer after motion is detected (30 seconds)
    private float motionTimer = 0f;

    // Whether this zone is currently crowded
    private bool zoneCrowded = false;

    // Current time state cached from TimeManager
    private TimeManager.TimeState cachedTimeState;

    // The LDR sensor on the same pole (if there is none, the clock decides day and night)
    private LDRSensor ldrSensor;


    // -------------------------------------------------------
    // START
    // -------------------------------------------------------

    void Start()
    {
        // Find the Light component on this object or its children
        lightComponent = GetComponentInChildren<Light>();

        // Find the LDR sensor on the same pole
        ldrSensor = GetComponent<LDRSensor>();

        // Find the TimeManager in the scene
        timeManager = FindAnyObjectByType<TimeManager>();

        // Subscribe to all time state events
        if (timeManager != null)
        {
            timeManager.OnDaytime.AddListener(OnDaytime);
            timeManager.OnSunsetTransition.AddListener(OnSunsetTransition);
            timeManager.OnNightStart.AddListener(OnNightStart);
            timeManager.OnSaveModeTransition.AddListener(OnSaveModeTransition);
            timeManager.OnSaveMode.AddListener(OnSaveMode);
        }

        // Start with light off
        SetTargetBrightness(0f);
    }


    // -------------------------------------------------------
    // UPDATE: runs every frame
    // -------------------------------------------------------

    void Update()
    {
        if (currentMode != LightMode.Manual)
        {
            // Handle sunset and save mode gradual transitions every frame
            HandleGradualTransitions();

            // Count down motion timer
            HandleMotionTimer();

            // Switch off in daylight, as read by the LDR sensor
            HandleDaylight();

            // Protect battery if critically low
            HandleBatteryProtection();
        }

        // Smoothly move toward the target brightness
        currentBrightness = Mathf.Lerp(currentBrightness, targetBrightness, Time.deltaTime * FADE_SPEED);

        // Apply to Unity Light component
        ApplyBrightnessToLight();

        // Calculate watt usage
        currentWatts = maxWatts * currentBrightness;

        // Charge or drain the battery
        UpdateSolarPanel();
    }


    // -------------------------------------------------------
    // TIME EVENT LISTENERS
    // -------------------------------------------------------

    // 6 AM: the day starts on the clock; the LDR sensor switches the light off
    // when the sun is really up (see HandleDaylight)
    void OnDaytime()
    {
        if (currentMode == LightMode.Manual) return;
        if (currentMode == LightMode.AIControl) currentMode = LightMode.Auto;
        cachedTimeState = TimeManager.TimeState.Daytime;
        motionDetected  = false;
    }

    // 6 PM: begin gradual rise from 0% to 80% over 2 hours
    void OnSunsetTransition()
    {
        if (currentMode == LightMode.Manual) return;
        // AI control only runs from 8 PM to 2 AM, so it ends here
        if (currentMode == LightMode.AIControl) currentMode = LightMode.Auto;
        cachedTimeState = TimeManager.TimeState.SunsetTransition;
        SetTargetBrightness(BRIGHTNESS_SUNSET_START);
    }

    // 8 PM: full night begins, lights at 80% (or 100% if the zone is crowded)
    void OnNightStart()
    {
        if (currentMode == LightMode.Manual) return;
        cachedTimeState = TimeManager.TimeState.Night;

        // Apply crowd aware brightness
        ApplyNightBrightness();
    }

    // 2 AM: begin the gradual fade from 80% to 20% (the Update loop does the fade)
    void OnSaveModeTransition()
    {
        if (currentMode == LightMode.Manual) return;
        cachedTimeState = TimeManager.TimeState.SaveModeTransition;

        // Start fading toward 20%, Update loop handles the gradual fade
        SetTargetBrightness(BRIGHTNESS_NIGHT);
    }

    // 3 AM: save mode fully active, all lights at 20%
    void OnSaveMode()
    {
        if (currentMode == LightMode.Manual) return;
        cachedTimeState = TimeManager.TimeState.SaveMode;
        motionDetected  = false;
        SetTargetBrightness(BRIGHTNESS_SAVE);
    }


    // -------------------------------------------------------
    // GRADUAL TRANSITION HANDLER
    // Called every frame to smoothly update brightness during transitions
    // -------------------------------------------------------

    void HandleGradualTransitions()
    {
        if (timeManager == null) return;

        // During the sunset transition: rise from 0% to 80%
        if (cachedTimeState == TimeManager.TimeState.SunsetTransition)
        {
            // Get how far through the transition we are (0 = start, 1 = end)
            float progress = timeManager.GetSunsetProgress();

            // Blend between the start and end brightness by progress
            float transitionBrightness = Mathf.Lerp(
                BRIGHTNESS_SUNSET_START,
                BRIGHTNESS_SUNSET_END,
                progress);

            SetTargetBrightness(transitionBrightness);
        }

        // During the save mode transition: fade from 80% to 20%
        if (cachedTimeState == TimeManager.TimeState.SaveModeTransition)
        {
            float progress = timeManager.GetSaveModeProgress();

            float transitionBrightness = Mathf.Lerp(
                BRIGHTNESS_NIGHT,
                BRIGHTNESS_SAVE,
                progress);

            SetTargetBrightness(transitionBrightness);
        }
    }


    // -------------------------------------------------------
    // DAYLIGHT (LDR SENSOR)
    // -------------------------------------------------------

    // True in daylight. The LDR sensor decides; without a working sensor, the clock does.
    bool IsDaylight()
    {
        if (ldrSensor != null && ldrSensor.HasSun) return !ldrSensor.IsDarkEnough();
        return cachedTimeState == TimeManager.TimeState.Daytime;
    }

    // In daylight the light is off. In the dark morning after 6 AM,
    // before the sun is up, it stays at save mode brightness.
    void HandleDaylight()
    {
        if (IsDaylight())
        {
            SetTargetBrightness(0f);
            return;
        }

        if (cachedTimeState == TimeManager.TimeState.Daytime)
            SetTargetBrightness(BRIGHTNESS_SAVE);
    }


    // -------------------------------------------------------
    // CROWD AWARENESS
    // -------------------------------------------------------

    // Called by the crowd sensor and the AI to say whether this zone is crowded
    public void SetZoneCrowded(bool crowded)
    {
        zoneCrowded = crowded;

        // Only apply during active night hours (8 PM to 2 AM)
        if (cachedTimeState == TimeManager.TimeState.Night && currentMode != LightMode.Manual)
            ApplyNightBrightness();
    }

    // Applies the correct night brightness based on crowd state
    void ApplyNightBrightness()
    {
        if (zoneCrowded)
            SetTargetBrightness(BRIGHTNESS_CROWDED);  // 100% when crowded
        else
            SetTargetBrightness(BRIGHTNESS_NIGHT);    // 80% normal night
    }


    // -------------------------------------------------------
    // MOTION DETECTION
    // -------------------------------------------------------

    // Called by the PIR sensor when it detects movement
    public void OnMotionDetected()
    {
        // Only react during save mode (3 AM to 6 AM)
        if (cachedTimeState != TimeManager.TimeState.SaveMode) return;
        if (currentMode == LightMode.Manual) return;

        // Raise to 40% and start the hold timer
        motionDetected = true;
        motionTimer    = motionHoldSeconds;
        SetTargetBrightness(BRIGHTNESS_MOTION);
    }

    // Counts down the motion timer and returns to save mode brightness
    void HandleMotionTimer()
    {
        if (!motionDetected) return;

        motionTimer -= Time.deltaTime;

        // When the hold time ends, go back to save mode brightness
        if (motionTimer <= 0f)
        {
            motionDetected = false;
            motionTimer    = 0f;

            // Return to 20% save mode if we are still in save mode
            if (cachedTimeState == TimeManager.TimeState.SaveMode)
                SetTargetBrightness(BRIGHTNESS_SAVE);
        }
    }


    // -------------------------------------------------------
    // MANUAL OVERRIDE (called from dashboard)
    // -------------------------------------------------------

    public void SetManualBrightness(float brightness)
    {
        currentMode = LightMode.Manual;
        SetTargetBrightness(Mathf.Clamp01(brightness));
    }

    public void SetToAuto()
    {
        currentMode = LightMode.Auto;

        // Immediately apply again the correct brightness for current time
        if (cachedTimeState == TimeManager.TimeState.Daytime)
            SetTargetBrightness(0f);
        else if (cachedTimeState == TimeManager.TimeState.Night)
            ApplyNightBrightness();
        else if (cachedTimeState == TimeManager.TimeState.SaveMode ||
                 cachedTimeState == TimeManager.TimeState.SaveModeTransition)
            SetTargetBrightness(BRIGHTNESS_SAVE);
    }


    // -------------------------------------------------------
    // SOLAR PANEL AND BATTERY
    // -------------------------------------------------------

    // Charges the battery in daylight and drains it by the power used at night
    void UpdateSolarPanel()
    {
        if (timeManager == null) return;

        // Calculate how much real time (in minutes) passed this frame
        float realMinutesThisFrame = Time.deltaTime / 60f;

        if (IsDaylight())
        {
            // The solar panel charges while the LDR reads daylight
            // chargeRatePerRealMinute is based on real solar panel specs
            batteryPercent += chargeRatePerRealMinute * realMinutesThisFrame;
        }
        else
        {
            // Battery drains based on how many watts we are using right now
            // drainRatePerWattPerRealMinute is based on real battery capacity
            batteryPercent -= currentWatts * drainRatePerWattPerRealMinute * realMinutesThisFrame;
        }

        // Clamp between 0 and 100
        batteryPercent = Mathf.Clamp(batteryPercent, 0f, 100f);
    }


    // -------------------------------------------------------
    // BATTERY PROTECTION
    // -------------------------------------------------------

    void HandleBatteryProtection()
    {
        // If the battery is nearly empty in the dark, drop to 10% to protect it
        if (batteryPercent <= lowBatteryThreshold && !IsDaylight())
        {
            SetTargetBrightness(BRIGHTNESS_LOW_BATTERY);
        }
    }


    // -------------------------------------------------------
    // APPLY TO UNITY LIGHT
    // -------------------------------------------------------

    void ApplyBrightnessToLight()
    {
        if (lightComponent == null) return;

        // Switch the light off when it is almost dark
        lightComponent.enabled = currentBrightness > LIGHT_OFF_THRESHOLD;

        // Map brightness (0 to 1) to the Unity light intensity
        lightComponent.intensity = currentBrightness * MAX_LIGHT_INTENSITY;

        // Set color based on brightness level using compound palette
        lightComponent.color = CompoundColors.GetLightColorByBrightness(currentBrightness);
    }


    // -------------------------------------------------------
    // TARGET BRIGHTNESS SETTER
    // -------------------------------------------------------

    void SetTargetBrightness(float brightness)
    {
        targetBrightness = Mathf.Clamp01(brightness);
    }


    // -------------------------------------------------------
    // ZONE LOOKUP (shared by the dashboards and the AI)
    // -------------------------------------------------------

    // Finds every street light (tagged "StreetLight") and sorts it into a zone.
    // A light whose parent object name contains "Main" belongs to Main Streets,
    // every other light belongs to Sub Streets.
    public static void FindAllByZone(List<LightController> mainStreets, List<LightController> subStreets)
    {
        foreach (GameObject lightObject in GameObject.FindGameObjectsWithTag("StreetLight"))
        {
            LightController light = lightObject.GetComponent<LightController>();
            if (light == null) continue;

            Transform parent = lightObject.transform.parent;
            if (parent != null && parent.name.Contains("Main")) mainStreets.Add(light);
            else                                                subStreets.Add(light);
        }
    }


    // -------------------------------------------------------
    // CLEANUP
    // -------------------------------------------------------

    void OnDestroy()
    {
        if (timeManager != null)
        {
            timeManager.OnDaytime.RemoveListener(OnDaytime);
            timeManager.OnSunsetTransition.RemoveListener(OnSunsetTransition);
            timeManager.OnNightStart.RemoveListener(OnNightStart);
            timeManager.OnSaveModeTransition.RemoveListener(OnSaveModeTransition);
            timeManager.OnSaveMode.RemoveListener(OnSaveMode);
        }
    }
}