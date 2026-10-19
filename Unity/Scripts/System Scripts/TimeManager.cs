// TimeManager.cs
// The master clock of the compound. It runs a full 24 hour day at an adjustable speed,
// and every other script follows it.
//
// The day is split into five lighting states, and an event fires when each one starts:
//   Daytime            06:00 to 18:00  lights off       (OnDaytime)
//   SunsetTransition   18:00 to 20:00  lights rising    (OnSunsetTransition)
//   Night              20:00 to 02:00  lights on        (OnNightStart)
//   SaveModeTransition 02:00 to 03:00  lights lowering  (OnSaveModeTransition)
//   SaveMode           03:00 to 06:00  lights at 20%    (OnSaveMode)
// The hours can be changed in the Inspector.
//
// There must be only one TimeManager in the scene. Other scripts can reach it
// through TimeManager.Instance.
using UnityEngine;
using UnityEngine.Events;

public class TimeManager : MonoBehaviour
{
    // The single clock in the scene
    public static TimeManager Instance { get; private set; }


    // -------------------------------------------------------
    // TIME SETTINGS
    // -------------------------------------------------------

    [Header("Time Settings")]
    // Clock speed: 1 = real time, 60 = one day in 24 minutes, 600 = one day in 2.4 minutes
    [Range(1f, 600f)]
    public float timeSpeed = 60f;

    // The hour the clock shows when Play is pressed
    [Range(0f, 24f)]
    public float startingHour = 6f;


    // -------------------------------------------------------
    // CURRENT TIME (shown live in the Inspector)
    // -------------------------------------------------------

    [Header("Current Time (Live)")]
    // The current hour as a decimal number, from 0.0 to 24.0
    [Range(0f, 24f)]
    public float currentHour = 6f;

    // The current time as text, like 08:30:00
    [HideInInspector]
    public string currentTimeString = "06:00:00";

    // The name of the current state
    public string currentStateName = "Daytime";


    // -------------------------------------------------------
    // STATE EVENTS
    // -------------------------------------------------------

    [Header("Time State Events")]
    public UnityEvent OnDaytime;
    public UnityEvent OnSunsetTransition;
    public UnityEvent OnNightStart;
    public UnityEvent OnSaveModeTransition;
    public UnityEvent OnSaveMode;


    // -------------------------------------------------------
    // STATE START HOURS
    // -------------------------------------------------------

    [Header("Time Thresholds (hours)")]
    // Daytime begins and the lights turn off
    public float sunriseHour = 6f;

    // The lights start rising from 0%
    public float sunsetTransitionHour = 18f;

    // Full night begins, the lights reach 80%
    public float nightStartHour = 20f;

    // The lights start lowering from 80% to 20%
    public float saveModeTransitionHour = 2f;

    // Save mode is fully active at 20%
    public float saveModeFullHour = 3f;


    // -------------------------------------------------------
    // STATES
    // -------------------------------------------------------

    public enum TimeState
    {
        Daytime,
        SunsetTransition,
        Night,
        SaveModeTransition,
        SaveMode
    }

    // The state right now, readable by other scripts
    [HideInInspector]
    public TimeState currentState;

    // Seconds in one hour, used to turn frame time into clock hours
    private const float SECONDS_PER_HOUR = 3600f;


    // -------------------------------------------------------
    // UNITY EVENTS
    // -------------------------------------------------------

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        currentHour = startingHour;
        UpdateTimeString();

        // Fire the starting state so every light knows where the day begins
        currentState     = GetStateForHour(currentHour);
        currentStateName = currentState.ToString();
        FireEventForState(currentState);
    }

    void Update()
    {
        // Move the clock forward by the frame time, at the chosen speed
        currentHour += (Time.deltaTime * timeSpeed) / SECONDS_PER_HOUR;

        // After midnight the clock starts again from 0
        if (currentHour >= 24f) currentHour -= 24f;

        UpdateTimeString();
        CheckForStateChange();
    }


    // -------------------------------------------------------
    // STATE DETECTION
    // -------------------------------------------------------

    // Fires the event of a new state the moment the clock enters it
    void CheckForStateChange()
    {
        TimeState newState = GetStateForHour(currentHour);
        if (newState == currentState) return;

        currentState     = newState;
        currentStateName = newState.ToString();
        FireEventForState(newState);
    }

    // Returns the state for any hour of the day
    public TimeState GetStateForHour(float hour)
    {
        if (hour >= saveModeFullHour       && hour < sunriseHour)          return TimeState.SaveMode;
        if (hour >= saveModeTransitionHour && hour < saveModeFullHour)     return TimeState.SaveModeTransition;
        if (hour >= sunriseHour            && hour < sunsetTransitionHour) return TimeState.Daytime;
        if (hour >= sunsetTransitionHour   && hour < nightStartHour)       return TimeState.SunsetTransition;

        // Everything else is night, from the night start hour through midnight
        return TimeState.Night;
    }

    void FireEventForState(TimeState state)
    {
        Debug.Log("[TimeManager] " + state + " at " + currentTimeString);

        switch (state)
        {
            case TimeState.Daytime:            OnDaytime.Invoke();            break;
            case TimeState.SunsetTransition:   OnSunsetTransition.Invoke();   break;
            case TimeState.Night:              OnNightStart.Invoke();         break;
            case TimeState.SaveModeTransition: OnSaveModeTransition.Invoke(); break;
            case TimeState.SaveMode:           OnSaveMode.Invoke();           break;
        }
    }


    // -------------------------------------------------------
    // TIME TEXT
    // -------------------------------------------------------

    // Turns the decimal hour into HH:MM:SS text
    void UpdateTimeString()
    {
        int hours = Mathf.FloorToInt(currentHour);

        float minutesExact = (currentHour - hours) * 60f;
        int minutes = Mathf.FloorToInt(minutesExact);

        int seconds = Mathf.FloorToInt((minutesExact - minutes) * 60f);

        currentTimeString = hours.ToString("D2") + ":" + minutes.ToString("D2") + ":" + seconds.ToString("D2");
    }


    // -------------------------------------------------------
    // PUBLIC HELPERS
    // -------------------------------------------------------

    public float  GetCurrentHour()      { return currentHour; }
    public string GetCurrentStateName() { return currentStateName; }
    public bool   IsNight()             { return currentState == TimeState.Night; }

    // How far the sunset rise has gone, from 0 (start) to 1 (end)
    public float GetSunsetProgress()
    {
        if (currentState != TimeState.SunsetTransition) return 0f;
        return Mathf.Clamp01((currentHour - sunsetTransitionHour) / (nightStartHour - sunsetTransitionHour));
    }

    // How far the save mode fade has gone, from 0 (start) to 1 (end)
    public float GetSaveModeProgress()
    {
        if (currentState != TimeState.SaveModeTransition) return 0f;
        return Mathf.Clamp01((currentHour - saveModeTransitionHour) / (saveModeFullHour - saveModeTransitionHour));
    }
}
