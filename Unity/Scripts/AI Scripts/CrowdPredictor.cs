// CrowdPredictor.cs
// Raises or lowers the street lights of each zone before the crowd arrives.
// It only works at night (20:00 to 02:00).
//
// It has two sources for its predictions:
//   1. CrowdHistory.json : a table of the usual crowd level (0 to 100) of each zone
//      for each hour. Every few seconds it looks 30 minutes ahead in this table.
//   2. AILearningEngine  : once the engine has learned from the cars, it sends its
//      own live predictions through ApplyAIPrediction().
//
// A crowded zone is set to 100%, a calm zone to 80%. Lights under manual control
// are never changed. Lights it controls switch to AI mode, which shows a purple
// AI badge on the main dashboard.
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class CrowdPredictor : MonoBehaviour
{
    // -------------------------------------------------------
    // SETTINGS (editable in the Inspector)
    // -------------------------------------------------------

    [Header("Prediction Settings")]
    // How many minutes ahead to predict
    public float predictionWindowMinutes = 30f;

    // Crowd level (0 to 100) at or above which a zone counts as crowded
    public int crowdedThreshold = 65;

    // Real seconds between two predictions
    public float predictionInterval = 10f;


    // -------------------------------------------------------
    // LIVE DATA (shown in the Inspector)
    // -------------------------------------------------------

    [Header("Live Prediction")]
    public string mainStreetPrediction = "Waiting...";
    public string subStreetPrediction  = "Waiting...";

    // True while the AI is allowed to control the lights (at night)
    public bool aiActive = false;


    // -------------------------------------------------------
    // CONSTANTS
    // -------------------------------------------------------

    private const string MAIN_STREETS = "Main Streets";
    private const string SUB_STREETS  = "Sub Streets";

    // Name of the crowd table in a Resources folder, and its backup location
    private const string CROWD_FILE_NAME   = "CrowdHistory";
    private const string CROWD_BACKUP_PATH = "/Data/CrowdHistory.json";


    // -------------------------------------------------------
    // RUNTIME STATE
    // -------------------------------------------------------

    private TimeManager timeManager;

    // Street lights of each zone
    private readonly List<LightController> mainStreetLights = new List<LightController>();
    private readonly List<LightController> subStreetLights  = new List<LightController>();

    // Crowd table: zone name > hour > crowd level
    private readonly Dictionary<string, Dictionary<int, int>> crowdData = new Dictionary<string, Dictionary<int, int>>();

    // True once the crowd table is loaded
    private bool dataLoaded = false;

    // Time since the last prediction
    private float predictionTimer = 0f;


    // -------------------------------------------------------
    // UNITY EVENTS
    // -------------------------------------------------------

    void Start()
    {
        timeManager = FindAnyObjectByType<TimeManager>();

        LightController.FindAllByZone(mainStreetLights, subStreetLights);
        Debug.Log("[CrowdPredictor] Tracking " + mainStreetLights.Count + " main + " + subStreetLights.Count + " sub lights");

        LoadCrowdData();
    }

    void Update()
    {
        if (!dataLoaded || timeManager == null) return;

        // The AI only works at night
        aiActive = timeManager.IsNight();
        if (!aiActive) return;

        predictionTimer += Time.deltaTime;
        if (predictionTimer >= predictionInterval)
        {
            predictionTimer = 0f;
            RunPrediction();
        }
    }


    // -------------------------------------------------------
    // PREDICTION FROM THE CROWD TABLE
    // -------------------------------------------------------

    // Looks 30 minutes ahead in the crowd table and sets both zones
    void RunPrediction()
    {
        float predictedHour = (timeManager.GetCurrentHour() + predictionWindowMinutes / 60f) % 24f;
        int hour = Mathf.FloorToInt(predictedHour);

        int mainCrowd = GetPredictedCrowd(MAIN_STREETS, hour);
        int subCrowd  = GetPredictedCrowd(SUB_STREETS,  hour);

        bool mainCrowded = mainCrowd >= crowdedThreshold;
        bool subCrowded  = subCrowd  >= crowdedThreshold;

        mainStreetPrediction = BuildPredictionText(mainCrowded, mainCrowd, hour);
        subStreetPrediction  = BuildPredictionText(subCrowded,  subCrowd,  hour);

        ApplyToZone(mainStreetLights, mainCrowded);
        ApplyToZone(subStreetLights,  subCrowded);

        Debug.Log("[CrowdPredictor] Predicting " + hour + "h: Main " + mainCrowd + "%, Sub " + subCrowd + "%");
    }

    // Crowd level of a zone at an hour, or 0 if the table has no value
    int GetPredictedCrowd(string zoneName, int hour)
    {
        if (!crowdData.TryGetValue(zoneName, out Dictionary<int, int> hours)) return 0;
        return hours.TryGetValue(hour, out int level) ? level : 0;
    }

    // Prediction text, for example "CROWDED, predicted 70% at 21:00"
    string BuildPredictionText(bool crowded, int level, int hour)
    {
        return (crowded ? "CROWDED" : "CALM") + ", predicted " + level + "% at " + hour.ToString("D2") + ":00";
    }


    // -------------------------------------------------------
    // PREDICTION FROM THE LEARNING ENGINE
    // -------------------------------------------------------

    // Called by AILearningEngine with its live prediction for one zone
    public void ApplyAIPrediction(string zoneName, bool crowded)
    {
        bool isMain = zoneName.Contains("Main");
        string text = "AI: " + (crowded ? "CROWDED" : "calm") + " predicted";

        if (isMain) mainStreetPrediction = text;
        else        subStreetPrediction  = text;

        // Lights are only changed at night
        if (timeManager == null || !timeManager.IsNight()) return;

        ApplyToZone(isMain ? mainStreetLights : subStreetLights, crowded);
    }


    // -------------------------------------------------------
    // APPLY TO LIGHTS
    // -------------------------------------------------------

    // Puts every light of a zone under AI control and sets its crowd state.
    // Lights under manual control are skipped.
    void ApplyToZone(List<LightController> lights, bool crowded)
    {
        foreach (LightController light in lights)
        {
            if (light == null) continue;
            if (light.currentMode == LightController.LightMode.Manual) continue;

            light.currentMode = LightController.LightMode.AIControl;
            light.SetZoneCrowded(crowded);
        }
    }

    // Returns the latest prediction text of a zone, for the dashboard
    public string GetPredictionText(string zoneName)
    {
        if (zoneName.Contains("Main")) return mainStreetPrediction;
        if (zoneName.Contains("Sub"))  return subStreetPrediction;
        return "No data";
    }


    // -------------------------------------------------------
    // LOADING THE CROWD TABLE
    // -------------------------------------------------------

    // Loads CrowdHistory.json from Resources, or from Assets/Data as a backup
    void LoadCrowdData()
    {
        TextAsset file = Resources.Load<TextAsset>(CROWD_FILE_NAME);
        if (file != null)
        {
            ParseCrowdJson(file.text);
            return;
        }

        string backupPath = Application.dataPath + CROWD_BACKUP_PATH;
        if (File.Exists(backupPath))
        {
            ParseCrowdJson(File.ReadAllText(backupPath));
            return;
        }

        Debug.LogWarning("[CrowdPredictor] CrowdHistory.json not found");
    }

    // Reads the hourly table of both zones
    void ParseCrowdJson(string json)
    {
        crowdData[MAIN_STREETS] = ParseZoneData(json, MAIN_STREETS);
        crowdData[SUB_STREETS]  = ParseZoneData(json, SUB_STREETS);

        dataLoaded = crowdData[MAIN_STREETS].Count > 0;
        Debug.Log("[CrowdPredictor] Data loaded, Main: " + crowdData[MAIN_STREETS].Count +
                  " hours, Sub: " + crowdData[SUB_STREETS].Count + " hours");
    }

    // Reads the "hourly_averages" block of one zone, written as "hour": level pairs
    Dictionary<int, int> ParseZoneData(string json, string zoneName)
    {
        var result = new Dictionary<int, int>();

        int zoneIndex = json.IndexOf("\"" + zoneName + "\"");
        if (zoneIndex < 0) return result;

        int averagesIndex = json.IndexOf("\"hourly_averages\"", zoneIndex);
        if (averagesIndex < 0) return result;

        int openBrace = json.IndexOf('{', averagesIndex);
        if (openBrace < 0) return result;
        int closeBrace = json.IndexOf('}', openBrace);
        if (closeBrace < 0) return result;

        string block = json.Substring(openBrace + 1, closeBrace - openBrace - 1);

        foreach (string pair in block.Split(','))
        {
            int colon = pair.IndexOf(':');
            if (colon < 0) continue;

            string hourText  = pair.Substring(0, colon).Trim().Replace("\"", "");
            string levelText = pair.Substring(colon + 1).Trim().Split('/')[0].Trim();

            if (int.TryParse(hourText, out int hour) && int.TryParse(levelText, out int level))
                result[hour] = level;
        }

        return result;
    }
}
