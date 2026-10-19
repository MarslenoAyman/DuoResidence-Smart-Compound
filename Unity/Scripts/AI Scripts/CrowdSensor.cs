// CrowdSensor.cs
// A virtual crowd sensor for a whole zone. On a real site this would be a people
// counter camera or a group of motion sensors at the zone entrances.
//
// It adds a large trigger box over the zone and counts the moving objects inside it
// (objects with a Rigidbody, like the cars). At night (20:00 to 02:00), when the count
// reaches the crowded threshold, it sets every light of the zone to 100%, and back
// to 80% when the zone is calm again.
using System.Collections.Generic;
using UnityEngine;

public class CrowdSensor : MonoBehaviour
{
    // -------------------------------------------------------
    // SETTINGS (editable in the Inspector)
    // -------------------------------------------------------

    [Header("Crowd Sensor Settings")]
    // Name of this zone, shown in the console messages
    public string zoneName = "Main Street Zone";

    // Number of moving objects at which the zone counts as crowded
    public int crowdedThreshold = 3;

    // Size of the detection box, covering the whole zone
    public Vector3 zoneSize = new Vector3(50f, 5f, 50f);

    // Draws the detection box in the Scene view
    public bool showDebugBox = false;


    // -------------------------------------------------------
    // LIVE DATA (shown in the Inspector)
    // -------------------------------------------------------

    [Header("Live Data")]
    // Moving objects inside the zone right now
    public int currentOccupancy = 0;

    // True while the zone is crowded
    public bool isCrowded = false;


    // -------------------------------------------------------
    // RUNTIME STATE
    // -------------------------------------------------------

    private TimeManager timeManager;

    // The lights this sensor controls
    private readonly List<LightController> zoneLights = new List<LightController>();

    // The moving objects inside the zone
    private readonly List<Collider> objectsInZone = new List<Collider>();

    // Colors of the detection box in the editor
    private static readonly Color GizmoFill = new Color(0f, 1f, 0f, 0.1f);


    // -------------------------------------------------------
    // UNITY EVENTS
    // -------------------------------------------------------

    void Start()
    {
        timeManager = FindAnyObjectByType<TimeManager>();

        // The trigger box that covers the zone
        BoxCollider zoneBox = gameObject.AddComponent<BoxCollider>();
        zoneBox.isTrigger = true;
        zoneBox.size      = zoneSize;

        CollectZoneLights();
        Debug.Log("[CrowdSensor] " + zoneName + " watching " + zoneLights.Count + " lights");
    }

    // A moving object entered the zone
    void OnTriggerEnter(Collider other)
    {
        if (other.attachedRigidbody == null) return;

        if (!objectsInZone.Contains(other))
            objectsInZone.Add(other);

        UpdateCrowdState();
    }

    // An object left the zone
    void OnTriggerExit(Collider other)
    {
        objectsInZone.Remove(other);
        UpdateCrowdState();
    }

    // Draws the zone box: green when calm, red when crowded
    void OnDrawGizmos()
    {
        if (!showDebugBox) return;

        Gizmos.color = GizmoFill;
        Gizmos.DrawCube(transform.position, zoneSize);

        Gizmos.color = isCrowded ? Color.red : Color.green;
        Gizmos.DrawWireCube(transform.position, zoneSize);
    }


    // -------------------------------------------------------
    // CROWD LOGIC
    // -------------------------------------------------------

    // Uses the lights placed under this sensor. If there are none,
    // it controls every street light in the scene.
    void CollectZoneLights()
    {
        zoneLights.AddRange(GetComponentsInChildren<LightController>());
        if (zoneLights.Count > 0) return;

        foreach (GameObject lightObject in GameObject.FindGameObjectsWithTag("StreetLight"))
        {
            LightController light = lightObject.GetComponent<LightController>();
            if (light != null) zoneLights.Add(light);
        }
    }

    // Recounts the zone and updates the lights when the crowd state changes
    void UpdateCrowdState()
    {
        // Forget objects that were destroyed while inside the zone
        objectsInZone.RemoveAll(c => c == null);
        currentOccupancy = objectsInZone.Count;

        // The crowd logic only works at night
        if (timeManager == null || !timeManager.IsNight()) return;

        bool crowdedNow = currentOccupancy >= crowdedThreshold;
        if (crowdedNow == isCrowded) return;

        isCrowded = crowdedNow;
        foreach (LightController light in zoneLights)
        {
            if (light != null) light.SetZoneCrowded(isCrowded);
        }

        Debug.Log("[CrowdSensor] " + zoneName + " is now " + (isCrowded ? "CROWDED" : "CALM") +
                  ", occupancy: " + currentOccupancy);
    }
}
