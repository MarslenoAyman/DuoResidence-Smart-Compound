// CarDataCollector.cs
// The travel log of one car (one script on each car).
// Every time the car enters a road zone (Main Streets or Sub Streets), a ZoneDetector
// calls OnEnterZone(). The collector saves the zone, the compound hour and the day,
// and sends the entry to the AILearningEngine, which learns the daily traffic
// pattern from all cars.
using System.Collections.Generic;
using UnityEngine;

public class CarDataCollector : MonoBehaviour
{
    // -------------------------------------------------------
    // ZONE ENTRY
    // -------------------------------------------------------

    // One record, created every time this car enters a zone
    [System.Serializable]
    public class ZoneEntry
    {
        // Name of the zone the car entered
        public string zoneName;

        // Compound hour of the entry (0 to 24)
        public float hour;

        // Compound day of the entry
        public int day;

        public ZoneEntry(string zoneName, float hour, int day)
        {
            this.zoneName = zoneName;
            this.hour     = hour;
            this.day      = day;
        }
    }


    // -------------------------------------------------------
    // LIVE DATA (shown in the Inspector)
    // -------------------------------------------------------

    [Header("Collected Data")]
    // Zone entries recorded by this car since Play was pressed
    public int totalZoneEntries = 0;


    // -------------------------------------------------------
    // RUNTIME STATE
    // -------------------------------------------------------

    private TimeManager timeManager;
    private AILearningEngine learningEngine;

    // Today's zone entries of this car
    private readonly List<ZoneEntry> zoneLog = new List<ZoneEntry>();


    // -------------------------------------------------------
    // UNITY EVENTS
    // -------------------------------------------------------

    void Start()
    {
        timeManager    = FindAnyObjectByType<TimeManager>();
        learningEngine = FindAnyObjectByType<AILearningEngine>();
    }


    // -------------------------------------------------------
    // PUBLIC
    // -------------------------------------------------------

    // Called by a ZoneDetector when this car enters its zone
    public void OnEnterZone(string zoneName)
    {
        if (timeManager == null) return;

        float hour = timeManager.GetCurrentHour();
        int   day  = learningEngine != null ? learningEngine.currentDay : 0;

        var entry = new ZoneEntry(zoneName, hour, day);
        zoneLog.Add(entry);
        totalZoneEntries++;

        // The learning engine gets every entry right away
        if (learningEngine != null)
            learningEngine.RecordZoneEntry(entry);

        Debug.Log("[DataCollector] " + gameObject.name + " entered " + zoneName +
                  " at " + hour.ToString("F1") + "h, day " + day);
    }

    // Called by the learning engine at the start of each new day
    public void ClearDailyLog()
    {
        zoneLog.Clear();
    }
}
