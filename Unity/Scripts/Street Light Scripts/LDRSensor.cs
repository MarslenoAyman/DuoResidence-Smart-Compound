// LDRSensor.cs
// A virtual LDR (light dependent resistor) sensor, one on each street light pole.
// On a real pole this tiny sensor measures how bright the surroundings are,
// to tell day from night.
//
// In Unity it measures the sun of the scene: it reads the intensity of the
// Directional Light that DayNightSky moves across the sky. When the reading falls
// below the darkness threshold, IsDarkEnough() returns true, and the LightController
// on the same pole switches the light on. When the sun rises above the threshold,
// the light switches off and the battery starts charging.
// With the default threshold (0.3) this happens at about 06:25 and 18:15.
using UnityEngine;

public class LDRSensor : MonoBehaviour
{
    // -------------------------------------------------------
    // SETTINGS AND READING
    // -------------------------------------------------------

    [Header("LDR Sensor Settings")]
    // Light reading from 0 (dark) to 1 (full daylight), updated every frame
    [Range(0f, 1f)]
    public float currentLightLevel = 1f;

    // At or below this reading it is dark enough for the lights
    // (real LDR circuits switch at about 10 to 50 lux, depending on calibration)
    public float darknessThreshold = 0.3f;


    // -------------------------------------------------------
    // RUNTIME STATE
    // -------------------------------------------------------

    // The sun of the scene (the Directional Light with DayNightSky on it)
    private Light sun;


    // -------------------------------------------------------
    // UNITY EVENTS
    // -------------------------------------------------------

    void Start()
    {
        DayNightSky sky = FindAnyObjectByType<DayNightSky>();
        if (sky != null) sun = sky.GetComponent<Light>();
        else Debug.LogWarning("[LDR] No DayNightSky found, " + gameObject.name + " cannot measure the sun");
    }

    // Reads the sun strength every frame (1 or more = full daylight)
    void Update()
    {
        currentLightLevel = sun != null ? Mathf.Clamp01(sun.intensity) : 1f;
    }


    // -------------------------------------------------------
    // PUBLIC
    // -------------------------------------------------------

    // True when the sensor found the sun and can give real readings
    public bool HasSun => sun != null;

    // True when it is dark enough for the street lights
    public bool IsDarkEnough()
    {
        return currentLightLevel <= darknessThreshold;
    }
}
