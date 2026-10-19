// CompoundCarDriver.cs
// Drives one car in the Digital Twin view (one script on each of the 15 driving cars).
// Each car has its own waypoint route, start hour and road height,
// all set by the DigitalTwinCarSetup tool.
//
// The car starts hidden. At its start hour it appears on its first waypoint and drives
// its route in a loop until 05:30 the next morning, then hides and waits for the next day.
// Its height stays locked to the road, so it never floats or sinks.
// Only its Y rotation turns toward the road ahead; the X = 270 tilt from the Blender
// export stays as it is.
// The Rigidbody is kinematic: the car is moved by this script, not by physics,
// and its BoxCollider is used by the zone detectors and the PIR sensors.
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CompoundCarDriver : MonoBehaviour
{
    // -------------------------------------------------------
    // SETTINGS (filled by DigitalTwinCarSetup)
    // -------------------------------------------------------

    [Header("Identity")]
    // Car name, for example "Audi01"
    public string carID = "Car";

    // Compound hour at which this car appears
    public float appearHour = 6f;

    // Height of the road under this car model
    public float roadY = 0f;

    [Header("Route (waypoints in driving order)")]
    public Transform[] waypoints;

    [Header("Movement")]
    // Driving speed in meters per second
    public float speed = 12f;

    // How quickly the car turns toward the next waypoint
    public float turnSpeed = 4f;

    // Distance at which a waypoint counts as reached
    public float reachDist = 3.5f;

    // Hour at which every car hides and resets (05:30)
    private const float RESET_HOUR = 5.5f;

    // Window after the start hour in which the car can appear (30 minutes)
    private const float APPEAR_WINDOW = 0.5f;

    // Collider that fits the car body (center and size in meters)
    private static readonly Vector3 ColliderCenter = new Vector3(0f, 0.75f, 0f);
    private static readonly Vector3 ColliderSize   = new Vector3(2.2f, 1.5f, 4.8f);


    // -------------------------------------------------------
    // RUNTIME STATE
    // -------------------------------------------------------

    // Index of the waypoint the car is driving to
    private int wpIdx = 0;

    // True while the car is driving
    private bool isActive = false;

    // True once the car has appeared today
    private bool appeared = false;

    // Clock hour in the last frame, used to catch the reset time
    private float prevHour = -1f;

    private Renderer[] rends;
    private Rigidbody  rb;

    // Starting rotation, which holds the X = 270 tilt from the Blender export
    private Vector3 initialEuler;

    void Awake()
    {
        rb          = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity  = false;

        rends = GetComponentsInChildren<Renderer>(true);

        // Make the collider fit the car body
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            box.isTrigger = false;
            box.center    = ColliderCenter;
            box.size      = ColliderSize;
        }

        // Keep the starting rotation, with the Blender tilt
        initialEuler = transform.eulerAngles;

        // Every car starts hidden
        SetVisible(false);
    }

    void Update()
    {
        TimeManager clock = TimeManager.Instance;
        if (clock == null) return;

        float hour = clock.GetCurrentHour();

        // Hide and reset the car when the clock passes 05:30 or midnight
        if (appeared)
        {
            bool crossedReset = (prevHour > 4f && prevHour < RESET_HOUR && hour >= RESET_HOUR)
                             || (prevHour > 23f && hour < 1f);
            if (crossedReset) ResetCar();
        }
        prevHour = hour;

        // Appear in the first 30 minutes after the start hour
        if (!appeared && hour >= appearHour && hour < appearHour + APPEAR_WINDOW)
            Appear();

        if (!isActive || waypoints == null || waypoints.Length == 0) return;

        Drive();
    }

    // -------------------------------------------------------
    // CAR STATES
    // -------------------------------------------------------

    // Places the car on its first waypoint, facing the second one, and shows it
    void Appear()
    {
        appeared  = true;
        isActive  = true;
        wpIdx     = 0;

        // This is the only time the car jumps to a position
        if (waypoints.Length > 0 && waypoints[0] != null)
        {
            Vector3 p = waypoints[0].position;
            p.y = roadY;
            transform.position = p;

            // Face toward second waypoint
            if (waypoints.Length > 1 && waypoints[1] != null)
                FaceTarget(waypoints[1].position);
        }

        SetVisible(true);
        Debug.Log("[" + carID + "] Appeared at " + appearHour + "h");
    }

    // Hides the car and sends it back to the start of its route
    void ResetCar()
    {
        isActive  = false;
        appeared  = false;
        wpIdx     = 0;
        SetVisible(false);
        Debug.Log("[" + carID + "] Reset at 5:30 AM");
    }

    // -------------------------------------------------------
    // DRIVING
    // -------------------------------------------------------

    // Moves the car toward the current waypoint, then on to the next one
    void Drive()
    {
        if (wpIdx >= waypoints.Length) wpIdx = 0;
        var target = waypoints[wpIdx];
        if (target == null) { wpIdx = (wpIdx + 1) % waypoints.Length; return; }

        // Keep the car on the road surface
        Vector3 pos = transform.position;
        pos.y = roadY;
        transform.position = pos;

        Vector3 tgt = target.position;
        tgt.y = roadY;

        Vector3 toTarget = tgt - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;

        if (dist < reachDist)
        {
            // Snap onto the waypoint so the car does not circle around it, then go to the next
            transform.position = new Vector3(tgt.x, roadY, tgt.z);
            wpIdx = (wpIdx + 1) % waypoints.Length;
            return;
        }

        // Turn toward the waypoint
        FaceTarget(tgt);

        // Move straight toward the waypoint (not along transform.forward, which can drift)
        Vector3 dir  = toTarget.normalized;
        float   step = speed * Time.deltaTime;
        transform.position += dir * step;
    }

    // Turns the car smoothly toward a point, changing only the Y rotation
    void FaceTarget(Vector3 target)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;

        float targetY = Quaternion.LookRotation(dir).eulerAngles.y;

        // Turn a little each frame
        float currentY = transform.eulerAngles.y;
        float newY     = Mathf.LerpAngle(currentY, targetY, turnSpeed * Time.deltaTime);

        // Keep the Blender tilt on X and Z, change only Y
        transform.eulerAngles = new Vector3(initialEuler.x, newY, initialEuler.z);
    }

    // Shows or hides every mesh of the car
    void SetVisible(bool visible)
    {
        if (rends == null) return;
        foreach (Renderer meshRenderer in rends)
            if (meshRenderer != null) meshRenderer.enabled = visible;
    }
}
