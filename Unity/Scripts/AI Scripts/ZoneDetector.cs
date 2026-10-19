// ZoneDetector.cs
// An invisible trigger box placed over a road zone (one over the Main Streets,
// one over the Sub Streets).
// When a car drives in, it tells that car's CarDataCollector, which records the
// zone and the time for the AI learning engine.
// In the editor the box is drawn in blue, so it is easy to place.
using UnityEngine;

public class ZoneDetector : MonoBehaviour
{
    [Header("Zone Settings")]
    // Name of the zone, must match the names used by the AI ("Main Streets" or "Sub Streets")
    public string zoneName = "Main Streets";

    // Colors of the zone box in the editor
    private static readonly Color GizmoFill    = new Color(0f, 0.5f, 1f, 0.08f);
    private static readonly Color GizmoOutline = new Color(0f, 0.5f, 1f, 0.5f);


    // A car entered the zone: record the visit on its data collector
    void OnTriggerEnter(Collider other)
    {
        // The collector sits on the car itself or on its parent object
        CarDataCollector collector = other.GetComponent<CarDataCollector>();
        if (collector == null) collector = other.GetComponentInParent<CarDataCollector>();

        if (collector != null)
            collector.OnEnterZone(zoneName);
    }

    // Draws the zone box in the Scene view
    void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;

        Gizmos.color = GizmoFill;
        Gizmos.DrawCube(transform.position, box.size);

        Gizmos.color = GizmoOutline;
        Gizmos.DrawWireCube(transform.position, box.size);
    }
}
