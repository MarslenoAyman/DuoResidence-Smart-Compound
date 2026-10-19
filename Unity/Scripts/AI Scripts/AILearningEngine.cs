// AILearningEngine.cs
// The learning part of the AI lighting system.
//
// Every time a car enters a road zone, CarDataCollector reports it here.
// The engine counts the car entries for each zone (Main Streets and Sub Streets)
// in each hour of the day, and keeps adding to these counts day after day.
//
// After enough days, it uses the counts to predict how busy each zone will be
// 30 minutes from now, and tells CrowdPredictor to raise the lights in busy
// zones before the cars arrive. The more days it collects, the higher its confidence.
using System.Collections.Generic;
using UnityEngine;

public class AILearningEngine : MonoBehaviour
{
    // -------------------------------------------------------
    // SETTINGS (editable in the Inspector)
    // -------------------------------------------------------

    [Header("Learning Settings")]
    // Days of data needed before the engine starts predicting
    public int minimumDaysToLearn = 1;

    // How many minutes ahead the engine predicts
    public float predictionWindowMinutes = 30f;

    // Activity level (0 to 100) at or above which a zone counts as crowded
    public int crowdedThreshold = 40;

    // Real seconds between two predictions
    public float predictionUpdateInterval = 8f;


    // -------------------------------------------------------
    // LIVE DATA (shown in the Inspector)
    // -------------------------------------------------------

    [Header("Live AI Data")]
    // The compound day the engine is on
    public int currentDay = 1;

    // All car entries recorded since Play was pressed
    public int totalEntriesRecorded = 0;

    // True once enough days are collected to predict
    public bool hasEnoughData = false;

    // Latest prediction text for each zone
    public string mainPrediction = "Learning...";
    public string subPrediction  = "Learning...";

    // How much the engine trusts its predictions, from 0 to 100
    [Range(0f, 100f)]
    public float confidenceLevel = 0f;


    // -------------------------------------------------------
    // CONSTANTS
    // -------------------------------------------------------

    private const string MAIN_STREETS = "Main Streets";
    private const string SUB_STREETS  = "Sub Streets";

    // Car entries in one hour that count as 100% activity
    private const float ENTRIES_FOR_FULL_ACTIVITY = 5f;

    // Total entries that give 100% confidence (500 entries)
    private const float ENTRIES_PER_CONFIDENCE_POINT = 5f;


    // -------------------------------------------------------
    // RUNTIME STATE
    // -------------------------------------------------------

    // Car entries counted for each zone, for each hour of the day (0 to 23)
    private readonly Dictionary<string, int[]> entriesPerHour = new Dictionary<string, int[]>
    {
        { MAIN_STREETS, new int[24] },
        { SUB_STREETS,  new int[24] },
    };

    // Car entries recorded today, used for the end of day log
    private int entriesToday = 0;

    private TimeManager timeManager;
    private CrowdPredictor crowdPredictor;
    private CarDataCollector[] allCollectors;

    // The clock hour in the last frame, used to catch midnight
    private float previousHour = -1f;

    // Time since the last prediction
    private float predictionTimer = 0f;


    // -------------------------------------------------------
    // UNITY EVENTS
    // -------------------------------------------------------

    void Start()
    {
        timeManager    = FindAnyObjectByType<TimeManager>();
        crowdPredictor = FindAnyObjectByType<CrowdPredictor>();
        allCollectors  = FindObjectsByType<CarDataCollector>();

        Debug.Log("[AI] Learning Engine started, tracking " + allCollectors.Length + " cars");
    }

    void Update()
    {
        if (timeManager == null) return;

        // When the clock passes midnight, a new compound day begins
        float currentHour = timeManager.GetCurrentHour();
        if (previousHour >= 23f && currentHour < 1f)
            OnDayEnd();
        previousHour = currentHour;

        // Predict on a fixed interval, at night only, once there is enough data
        if (!timeManager.IsNight() || !hasEnoughData) return;

        predictionTimer += Time.deltaTime;
        if (predictionTimer >= predictionUpdateInterval)
        {
            predictionTimer = 0f;
            RunPrediction();
        }
    }


    // -------------------------------------------------------
    // LEARNING
    // -------------------------------------------------------

    // Called by CarDataCollector every time a car enters a zone
    public void RecordZoneEntry(CarDataCollector.ZoneEntry entry)
    {
        entriesToday++;
        totalEntriesRecorded++;

        if (!entriesPerHour.TryGetValue(entry.zoneName, out int[] hours)) return;

        int hour = Mathf.FloorToInt(entry.hour);
        if (hour < 0 || hour > 23) return;

        hours[hour]++;
        confidenceLevel = Mathf.Clamp(totalEntriesRecorded / ENTRIES_PER_CONFIDENCE_POINT, 0f, 100f);
    }

    // Starts a new day: checks if there is enough data and clears the daily car logs
    void OnDayEnd()
    {
        Debug.Log("[AI] Day " + currentDay + " ended, recorded " + entriesToday + " entries");

        currentDay++;
        if (currentDay > minimumDaysToLearn) hasEnoughData = true;

        entriesToday = 0;
        foreach (CarDataCollector collector in allCollectors)
            collector.ClearDailyLog();

        Debug.Log("[AI] Day " + currentDay + " started, AI confidence: " + confidenceLevel.ToString("F0") + "%");
    }


    // -------------------------------------------------------
    // PREDICTION
    // -------------------------------------------------------

    // Looks at the learned activity 30 minutes ahead and updates the lights
    void RunPrediction()
    {
        float predictedHour = (timeManager.GetCurrentHour() + predictionWindowMinutes / 60f) % 24f;
        int hour = Mathf.FloorToInt(predictedHour);

        int mainActivity = GetLearnedActivity(MAIN_STREETS, hour);
        int subActivity  = GetLearnedActivity(SUB_STREETS,  hour);

        bool mainCrowded = mainActivity >= crowdedThreshold;
        bool subCrowded  = subActivity  >= crowdedThreshold;

        mainPrediction = BuildPredictionText(mainCrowded, mainActivity, hour);
        subPrediction  = BuildPredictionText(subCrowded,  subActivity,  hour);

        // CrowdPredictor applies the result to the street lights
        if (crowdPredictor != null)
        {
            crowdPredictor.ApplyAIPrediction(MAIN_STREETS, mainCrowded);
            crowdPredictor.ApplyAIPrediction(SUB_STREETS,  subCrowded);
        }
    }

    // Activity of a zone at an hour, from 0 to 100
    // (5 or more car entries in that hour = 100%)
    int GetLearnedActivity(string zoneName, int hour)
    {
        if (!entriesPerHour.TryGetValue(zoneName, out int[] hours)) return 0;

        float activity = Mathf.Clamp01(hours[hour] / ENTRIES_FOR_FULL_ACTIVITY) * 100f;
        return Mathf.RoundToInt(activity);
    }

    // Prediction text shown in the Inspector, for example:
    // "CROWDED at 21:00, activity 80% (high confidence)"
    string BuildPredictionText(bool crowded, int activity, int hour)
    {
        string state = crowded ? "CROWDED" : "calm";
        string confidence = confidenceLevel < 30f ? "low confidence"
                          : confidenceLevel < 70f ? "moderate confidence"
                          : "high confidence";
        return state + " at " + hour.ToString("D2") + ":00, activity " + activity + "% (" + confidence + ")";
    }

    // Returns the latest prediction text of a zone
    public string GetPredictionText(string zoneName)
    {
        if (zoneName.Contains("Main")) return mainPrediction;
        if (zoneName.Contains("Sub"))  return subPrediction;
        return "No data";
    }
}
