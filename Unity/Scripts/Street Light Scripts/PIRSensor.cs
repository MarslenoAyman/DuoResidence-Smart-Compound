// PIRSensor.cs
// A virtual PIR (passive infrared) motion sensor, one on each street light pole.
// On a real pole this small sensor feels the body heat of people and cars moving nearby.
//
// In Unity it adds a trigger sphere around the light. When a moving object (one with
// a Rigidbody, like a car) enters the sphere, it tells the LightController.
// During save mode (03:00 to 06:00) the light then rises from 20% to 40% for 30 seconds.
using UnityEngine;

public class PIRSensor : MonoBehaviour
{
    // -------------------------------------------------------
    // SETTINGS (editable in the Inspector)
    // -------------------------------------------------------

    [Header("PIR Sensor Settings")]
    // Detection radius around the pole in meters (real PIR sensors cover about 5 to 12 m)
    public float detectionRadius = 8f;

    // Layers the sensor reacts to (all layers by default)
    public LayerMask detectionLayers = ~0;

    // Draws the detection sphere in the Scene view
    public bool showDebugSphere = false;

    // Fill color of the detection sphere in the editor
    private static readonly Color GizmoFill = new Color(1f, 1f, 0f, 0.3f);


    // -------------------------------------------------------
    // RUNTIME STATE
    // -------------------------------------------------------

    // The street light on the same object
    private LightController lightController;


    // -------------------------------------------------------
    // UNITY EVENTS
    // -------------------------------------------------------

    void Start()
    {
        lightController = GetComponent<LightController>();

        // The trigger sphere that acts as the detection area
        SphereCollider detectionArea = gameObject.AddComponent<SphereCollider>();
        detectionArea.isTrigger = true;
        detectionArea.radius    = detectionRadius;
    }

    // Something entered the detection sphere
    void OnTriggerEnter(Collider other)
    {
        // Only moving objects count
        if (other.attachedRigidbody == null) return;

        // Only objects on the chosen layers count
        if ((detectionLayers & (1 << other.gameObject.layer)) == 0) return;

        if (lightController != null)
            lightController.OnMotionDetected();

        Debug.Log("[PIR] Motion detected by " + gameObject.name + ", object: " + other.gameObject.name);
    }

    // Draws the detection sphere in yellow
    void OnDrawGizmos()
    {
        if (!showDebugSphere) return;

        Gizmos.color = GizmoFill;
        Gizmos.DrawSphere(transform.position, detectionRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
