// DigitalTwinCarSetup.cs
// Editor tool that prepares the 15 cars of the Digital Twin view.
// Run it from the Unity menu: Tools > Setup Digital Twin Cars
//
// What it does, step by step:
//   1. Reads every car waypoint in the scene by name
//   2. Collects the cars from the Audi, McLaren and Mercedes groups
//   3. Gives 15 cars a CompoundCarDriver with their own route, start hour and road height
//   4. Sets a solid BoxCollider and a kinematic Rigidbody on each driving car
//   5. Removes the driver from the cars that are not used, then saves the scene
//
// It only changes the Digital Twin cars. The VR Walkthrough and 3D View modes are not touched.
// The whole file is editor only, so it is left out of the final app build.
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DigitalTwinCarSetup
{
    // -------------------------------------------------------
    // ROUTE DATA
    // -------------------------------------------------------

    // One car route: the car name, the hour it appears, and its looping waypoint list
    private struct CarRoute
    {
        public string id;
        public float appearHour;
        public string[] waypoints;

        public CarRoute(string id, float appearHour, string[] waypoints)
        {
            this.id = id;
            this.appearHour = appearHour;
            this.waypoints = waypoints;
        }
    }

    // Names of the three car groups under the "Cars" object, in this order:
    // Audi 01 to 08 (index 0 to 7), McLaren 01 to 08 (8 to 15), Mercedes 01 to 08 (16 to 23)
    private static readonly string[] CarGroups = { "Audi E-Tron Cars", "McLaren P1 Cars", "Mercedes Maybach Cars" };

    // Which cars drive, matched one to one with the routes below:
    // Audi 01 to 04, McLaren 01 to 06, Mercedes 01 to 05
    private static readonly int[] ActiveCarIndices = { 0, 1, 2, 3, 8, 9, 10, 11, 12, 13, 16, 17, 18, 19, 20 };

    // Cars that stay parked: Audi 05 to 08, McLaren 07 and 08, Mercedes 06 to 08
    private static readonly int[] UnusedCarIndices = { 4, 5, 6, 7, 14, 15, 21, 22, 23 };

    // Road height for each car model, so its wheels touch the road
    private static readonly float[] AudiRoadY    = { 0.0402f, 0.0002f, 0.0002f, 0.0002f, 0.0002f, 0.0002f, 0.0002f, 0.0002f };
    private static readonly float[] McLarenRoadY = { 1.0196f, 0.8196f, 0.8196f, 0.8196f, 0.8196f, 0.8196f, 0.8196f, 0.8196f };
    private const float MercedesRoadY = 0.8936f;

    // Collider size shared by all cars (center and size in meters)
    private static readonly Vector3 CarColliderCenter = new Vector3(0f, 0.75f, 0f);
    private static readonly Vector3 CarColliderSize   = new Vector3(2.2f, 1.5f, 4.8f);


    // -------------------------------------------------------
    // MENU ENTRY
    // -------------------------------------------------------

    [MenuItem("Tools/Setup Digital Twin Cars")]
    public static void SetupCars()
    {
        Dictionary<string, Transform> waypointMap = BuildWaypointMap();

        List<Transform> allCars = CollectCars();
        if (allCars == null) return;

        // Give each active car its route, in the order of the route list
        CarRoute[] routes = GetRoutes();
        for (int i = 0; i < routes.Length; i++)
        {
            int carIndex = ActiveCarIndices[i];
            ConfigureCar(allCars[carIndex], carIndex, routes[i], waypointMap);
        }

        RemoveDriversFromUnusedCars(allCars);

        // Save the scene so the setup is kept
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[Setup] " + routes.Length + " cars configured. Scene saved.");
    }


    // -------------------------------------------------------
    // SETUP STEPS
    // -------------------------------------------------------

    // Finds every object tagged "CarWaypoint" and stores it by name
    private static Dictionary<string, Transform> BuildWaypointMap()
    {
        var map = new Dictionary<string, Transform>();
        foreach (GameObject waypoint in GameObject.FindGameObjectsWithTag("CarWaypoint"))
        {
            if (!map.ContainsKey(waypoint.name))
                map[waypoint.name] = waypoint.transform;
        }
        return map;
    }

    // Lists all cars from the three groups, in group order. Returns null if a group is missing.
    private static List<Transform> CollectCars()
    {
        GameObject carsParent = GameObject.Find("Cars");
        if (carsParent == null)
        {
            Debug.LogError("[Setup] Cars parent not found");
            return null;
        }

        var cars = new List<Transform>();
        foreach (string groupName in CarGroups)
        {
            Transform group = carsParent.transform.Find(groupName);
            if (group == null)
            {
                Debug.LogError("[Setup] Group not found: " + groupName);
                return null;
            }

            for (int i = 0; i < group.childCount; i++)
                cars.Add(group.GetChild(i));
        }
        return cars;
    }

    // Adds and fills the driver, collider and rigidbody of one car
    private static void ConfigureCar(Transform car, int carIndex, CarRoute route, Dictionary<string, Transform> waypointMap)
    {
        Undo.RecordObject(car.gameObject, "Setup Car " + route.id);

        // Add the driver script if the car does not have it yet
        CompoundCarDriver driver = car.GetComponent<CompoundCarDriver>();
        if (driver == null)
            driver = Undo.AddComponent<CompoundCarDriver>(car.gameObject);

        driver.carID      = route.id;
        driver.appearHour = route.appearHour;
        driver.roadY      = GetRoadHeight(carIndex);
        driver.waypoints  = ResolveWaypoints(route, waypointMap);

        SetupCollider(car);
        SetupRigidbody(car);

        EditorUtility.SetDirty(car.gameObject);
        Debug.Log("[Setup] " + route.id + " > " + car.name + " | appears at " + route.appearHour + "h | " + driver.waypoints.Length + " waypoints");
    }

    // Removes the driver from cars that are not part of the 15 routes
    private static void RemoveDriversFromUnusedCars(List<Transform> allCars)
    {
        foreach (int index in UnusedCarIndices)
        {
            if (index >= allCars.Count) continue;

            CompoundCarDriver driver = allCars[index].GetComponent<CompoundCarDriver>();
            if (driver != null)
                Undo.DestroyObjectImmediate(driver);
        }
    }


    // -------------------------------------------------------
    // HELPERS
    // -------------------------------------------------------

    // Returns the road height for a car based on its model
    private static float GetRoadHeight(int carIndex)
    {
        if (carIndex < 8)  return AudiRoadY[carIndex];
        if (carIndex < 16) return McLarenRoadY[carIndex - 8];
        return MercedesRoadY;
    }

    // Turns the waypoint names of a route into scene objects, warning about any missing name
    private static Transform[] ResolveWaypoints(CarRoute route, Dictionary<string, Transform> waypointMap)
    {
        var list = new List<Transform>();
        foreach (string name in route.waypoints)
        {
            if (waypointMap.TryGetValue(name, out Transform waypoint))
                list.Add(waypoint);
            else
                Debug.LogWarning("[Setup] Waypoint not found: " + name + " for " + route.id);
        }
        return list.ToArray();
    }

    // Gives the car a solid box collider that fits the car body
    private static void SetupCollider(Transform car)
    {
        BoxCollider box = car.GetComponent<BoxCollider>();
        if (box == null)
            box = Undo.AddComponent<BoxCollider>(car.gameObject);

        box.isTrigger = false;
        box.center    = CarColliderCenter;
        box.size      = CarColliderSize;
    }

    // Makes the car kinematic, so the driver script moves it instead of physics
    private static void SetupRigidbody(Transform car)
    {
        Rigidbody body = car.GetComponent<Rigidbody>();
        if (body == null)
            body = Undo.AddComponent<Rigidbody>(car.gameObject);

        body.isKinematic = true;
        body.useGravity  = false;
    }


    // -------------------------------------------------------
    // ROUTES
    // -------------------------------------------------------
    // Each route only uses waypoints on the same road, or turns at a real intersection,
    // so cars never cut through buildings or enter the garages.
    // After the last waypoint the car loops back to the first one.
    //
    // Roads and their waypoints:
    //   Right Main  (X = +95)   RightMain_01 (north) to RightMain_07 (south)
    //   Left Main   (X = -95)   LeftMain_01  (north) to LeftMain_07  (south)
    //   Upper Main  (Z = +149)  UpperMain_01 (west)  to UpperMain_05 (east)
    //   Lower Main  (Z = -149)  LowerMain_01 (west)  to LowerMain_05 (east)
    //   Middle Main (Z = 0)     MiddleMain_01 (west) to MiddleMain_05 (east)
    //   Upper Sub   (Z = +118)  UpperSub_01  (west)  to UpperSub_05  (east)
    //   Lower Sub   (Z = -118)  LowerSub_01  (west)  to LowerSub_05  (east)
    //   Sub Left    (X = -41)   SubLeft_01   (north) to SubLeft_05   (south)
    //   Sub Right   (X = +41)   SubRight_01  (north) to SubRight_05  (south)
    //
    // Cars appear one per hour, from 6 AM (Audi 01) to 8 PM (Mercedes 05).
    private static CarRoute[] GetRoutes()
    {
        return new[]
        {
            // 6 AM, Audi 01: Right Main, north to south and back
            new CarRoute("Audi01", 6f, new[]{
                "WP_RightMain_01_UpperMain","WP_RightMain_02_UpperGarage","WP_RightMain_03_UpperSub",
                "WP_RightMain_04_Middle","WP_RightMain_05_LowerSub","WP_RightMain_06_LowerGarage",
                "WP_RightMain_07_LowerMain","WP_RightMain_06_LowerGarage","WP_RightMain_05_LowerSub",
                "WP_RightMain_04_Middle","WP_RightMain_03_UpperSub","WP_RightMain_02_UpperGarage" }),

            // 7 AM, Audi 02: Left Main, south to north and back
            new CarRoute("Audi02", 7f, new[]{
                "WP_LeftMain_07_LowerMain","WP_LeftMain_06_LowerGarage","WP_LeftMain_05_LowerSub",
                "WP_LeftMain_04_Middle","WP_LeftMain_03_UpperSub","WP_LeftMain_02_UpperGarage",
                "WP_LeftMain_01_UpperMain","WP_LeftMain_02_UpperGarage","WP_LeftMain_03_UpperSub",
                "WP_LeftMain_04_Middle","WP_LeftMain_05_LowerSub","WP_LeftMain_06_LowerGarage" }),

            // 8 AM, Audi 03: Upper Main, west to east and back
            new CarRoute("Audi03", 8f, new[]{
                "WP_UpperMain_01_LeftMain","WP_UpperMain_02_SubLeft","WP_UpperMain_03_Center",
                "WP_UpperMain_04_SubRight","WP_UpperMain_05_RightMain","WP_UpperMain_04_SubRight",
                "WP_UpperMain_03_Center","WP_UpperMain_02_SubLeft" }),

            // 9 AM, Audi 04: Lower Main, west to east and back
            new CarRoute("Audi04", 9f, new[]{
                "WP_LowerMain_01_LeftMain","WP_LowerMain_02_SubLeft","WP_LowerMain_03_Center",
                "WP_LowerMain_04_SubRight","WP_LowerMain_05_RightMain","WP_LowerMain_04_SubRight",
                "WP_LowerMain_03_Center","WP_LowerMain_02_SubLeft" }),

            // 10 AM, McLaren 01: Middle Main, east to west and back
            new CarRoute("McLaren01", 10f, new[]{
                "WP_MiddleMain_05_RightMain","WP_MiddleMain_04_SubRight","WP_MiddleMain_03_Center",
                "WP_MiddleMain_02_SubLeft","WP_MiddleMain_01_LeftMain","WP_MiddleMain_02_SubLeft",
                "WP_MiddleMain_03_Center","WP_MiddleMain_04_SubRight" }),

            // 11 AM, McLaren 02: Upper Sub, west to east and back
            new CarRoute("McLaren02", 11f, new[]{
                "WP_UpperSub_01_LeftMain","WP_UpperSub_02_SubLeft","WP_UpperSub_03_Center",
                "WP_UpperSub_04_SubRight","WP_UpperSub_05_RightMain","WP_UpperSub_04_SubRight",
                "WP_UpperSub_03_Center","WP_UpperSub_02_SubLeft" }),

            // 12 PM, McLaren 03: Lower Sub, east to west and back
            new CarRoute("McLaren03", 12f, new[]{
                "WP_LowerSub_05_RightMain","WP_LowerSub_04_SubRight","WP_LowerSub_03_Center",
                "WP_LowerSub_02_SubLeft","WP_LowerSub_01_LeftMain","WP_LowerSub_02_SubLeft",
                "WP_LowerSub_03_Center","WP_LowerSub_04_SubRight" }),

            // 1 PM, McLaren 04: Sub Right, north to south and back
            new CarRoute("McLaren04", 13f, new[]{
                "WP_SubRight_01_UpperSub","WP_SubRight_02_UpperMid","WP_SubRight_03_Middle",
                "WP_SubRight_04_LowerMid","WP_SubRight_05_LowerSub","WP_SubRight_04_LowerMid",
                "WP_SubRight_03_Middle","WP_SubRight_02_UpperMid" }),

            // 2 PM, McLaren 05: Sub Left, south to north and back
            new CarRoute("McLaren05", 14f, new[]{
                "WP_SubLeft_05_LowerSub","WP_SubLeft_04_LowerMid","WP_SubLeft_03_Middle",
                "WP_SubLeft_02_UpperMid","WP_SubLeft_01_UpperSub","WP_SubLeft_02_UpperMid",
                "WP_SubLeft_03_Middle","WP_SubLeft_04_LowerMid" }),

            // 3 PM, McLaren 06: north loop through Upper Main, Left Main and Upper Sub
            new CarRoute("McLaren06", 15f, new[]{
                "WP_RightMain_01_UpperMain","WP_UpperMain_05_RightMain",
                "WP_UpperMain_04_SubRight","WP_UpperMain_03_Center","WP_UpperMain_02_SubLeft",
                "WP_UpperMain_01_LeftMain","WP_LeftMain_01_UpperMain",
                "WP_LeftMain_02_UpperGarage","WP_LeftMain_03_UpperSub",
                "WP_UpperSub_01_LeftMain","WP_UpperSub_02_SubLeft","WP_UpperSub_03_Center",
                "WP_UpperSub_04_SubRight","WP_UpperSub_05_RightMain",
                "WP_RightMain_03_UpperSub","WP_RightMain_02_UpperGarage" }),

            // 4 PM, Mercedes 01: south loop through Lower Main, Left Main and Lower Sub
            new CarRoute("Mercedes01", 16f, new[]{
                "WP_RightMain_07_LowerMain","WP_LowerMain_05_RightMain",
                "WP_LowerMain_04_SubRight","WP_LowerMain_03_Center","WP_LowerMain_02_SubLeft",
                "WP_LowerMain_01_LeftMain","WP_LeftMain_07_LowerMain",
                "WP_LeftMain_06_LowerGarage","WP_LeftMain_05_LowerSub",
                "WP_LowerSub_01_LeftMain","WP_LowerSub_02_SubLeft","WP_LowerSub_03_Center",
                "WP_LowerSub_04_SubRight","WP_LowerSub_05_RightMain",
                "WP_RightMain_05_LowerSub","WP_RightMain_06_LowerGarage" }),

            // 5 PM, Mercedes 02: outer loop around the whole compound
            new CarRoute("Mercedes02", 17f, new[]{
                "WP_RightMain_01_UpperMain","WP_UpperMain_05_RightMain",
                "WP_UpperMain_03_Center","WP_UpperMain_01_LeftMain",
                "WP_LeftMain_01_UpperMain","WP_LeftMain_04_Middle",
                "WP_LeftMain_07_LowerMain","WP_LowerMain_01_LeftMain",
                "WP_LowerMain_03_Center","WP_LowerMain_05_RightMain",
                "WP_RightMain_07_LowerMain","WP_RightMain_04_Middle" }),

            // 6 PM, Mercedes 03: inner loop on the sub streets
            new CarRoute("Mercedes03", 18f, new[]{
                "WP_SubRight_01_UpperSub","WP_UpperSub_04_SubRight",
                "WP_UpperSub_03_Center","WP_UpperSub_02_SubLeft",
                "WP_SubLeft_01_UpperSub","WP_SubLeft_03_Middle",
                "WP_SubLeft_05_LowerSub","WP_LowerSub_02_SubLeft",
                "WP_LowerSub_03_Center","WP_LowerSub_04_SubRight",
                "WP_SubRight_05_LowerSub","WP_SubRight_03_Middle" }),

            // 7 PM, Mercedes 04: Middle Main, then back through Upper Sub
            new CarRoute("Mercedes04", 19f, new[]{
                "WP_RightMain_04_Middle","WP_MiddleMain_05_RightMain",
                "WP_MiddleMain_04_SubRight","WP_MiddleMain_03_Center",
                "WP_MiddleMain_02_SubLeft","WP_MiddleMain_01_LeftMain",
                "WP_LeftMain_04_Middle","WP_LeftMain_03_UpperSub",
                "WP_UpperSub_01_LeftMain","WP_UpperSub_03_Center",
                "WP_UpperSub_05_RightMain","WP_RightMain_03_UpperSub" }),

            // 8 PM, Mercedes 05: south loop through Lower Sub, Left Main and Lower Main
            new CarRoute("Mercedes05", 20f, new[]{
                "WP_LowerSub_05_RightMain","WP_LowerSub_03_Center",
                "WP_LowerSub_01_LeftMain","WP_LeftMain_05_LowerSub",
                "WP_LeftMain_06_LowerGarage","WP_LeftMain_07_LowerMain",
                "WP_LowerMain_01_LeftMain","WP_LowerMain_03_Center",
                "WP_LowerMain_05_RightMain","WP_RightMain_07_LowerMain",
                "WP_RightMain_05_LowerSub","WP_RightMain_06_LowerGarage" }),
        };
    }
}
#endif
