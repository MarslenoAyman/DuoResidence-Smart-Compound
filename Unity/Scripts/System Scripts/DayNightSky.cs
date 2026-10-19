// DayNightSky.cs
// Moves and colors the sun (the Directional Light) through the compound day.
//
// The sun rises in the East at 06:00, passes high over the compound at noon,
// and sets in the West at 18:00. Its color and strength follow the real order:
//   pre dawn > orange sunrise > golden hour > white noon >
//   golden afternoon > deep orange sunset > purple dusk > night
// The script also changes the ambient light, the fog and the shadow strength,
// so the whole scene changes mood during the day.
//
// Every value is set by keyframes (an hour and a value). Between two keyframes
// the value blends smoothly, so the day has no sudden jumps.
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Light))]
public class DayNightSky : MonoBehaviour
{
    // -------------------------------------------------------
    // KEYFRAME TYPES
    // -------------------------------------------------------

    // A color and a number at a given hour of the day
    private struct SkyKey
    {
        public float hour;
        public Color color;
        public float value;

        public SkyKey(float hour, Color color, float value = 0f)
        {
            this.hour  = hour;
            this.color = color;
            this.value = value;
        }
    }


    // -------------------------------------------------------
    // SUN COLORS
    // -------------------------------------------------------

    private static readonly Color PreDawn    = new Color(0.05f, 0.04f, 0.15f); // deep blue purple
    private static readonly Color DawnGlow   = new Color(0.85f, 0.35f, 0.05f); // deep orange on the horizon
    private static readonly Color Sunrise    = new Color(1.00f, 0.55f, 0.15f); // warm orange sunrise
    private static readonly Color GoldenHour = new Color(1.00f, 0.78f, 0.42f); // golden hour
    private static readonly Color Morning    = new Color(1.00f, 0.90f, 0.65f); // warm morning
    private static readonly Color Noon       = new Color(1.00f, 0.97f, 0.88f); // bright white noon
    private static readonly Color Afternoon  = new Color(1.00f, 0.92f, 0.72f); // warm afternoon
    private static readonly Color GoldenEve  = new Color(1.00f, 0.72f, 0.28f); // golden evening
    private static readonly Color Sunset     = new Color(1.00f, 0.38f, 0.06f); // deep orange sunset
    private static readonly Color Dusk       = new Color(0.60f, 0.18f, 0.04f); // red purple dusk
    private static readonly Color Night      = new Color(0.00f, 0.00f, 0.00f); // no direct light

    // Sun color and intensity through the day (value = light intensity)
    private static readonly SkyKey[] SunKeys =
    {
        new SkyKey( 4.5f, Night,      0.00f),
        new SkyKey( 5.5f, PreDawn,    0.00f),
        new SkyKey( 6.0f, DawnGlow,   0.05f),
        new SkyKey( 6.5f, Sunrise,    0.35f),
        new SkyKey( 7.5f, GoldenHour, 0.75f),
        new SkyKey( 9.0f, Morning,    1.00f),
        new SkyKey(12.0f, Noon,       1.30f),
        new SkyKey(15.0f, Afternoon,  1.10f),
        new SkyKey(17.0f, GoldenEve,  0.85f),
        new SkyKey(17.8f, Sunset,     0.55f),
        new SkyKey(18.5f, Dusk,       0.18f),
        new SkyKey(19.5f, Night,      0.00f),
    };


    // -------------------------------------------------------
    // AMBIENT LIGHT
    // -------------------------------------------------------

    // Soft light that fills the shadows, through the day
    private static readonly SkyKey[] AmbientKeys =
    {
        new SkyKey( 4.5f, new Color(0.02f, 0.02f, 0.06f)), // night
        new SkyKey( 5.5f, new Color(0.05f, 0.04f, 0.10f)), // pre dawn
        new SkyKey( 6.5f, new Color(0.28f, 0.16f, 0.08f)), // sunrise
        new SkyKey( 9.0f, new Color(0.42f, 0.38f, 0.32f)), // morning
        new SkyKey(12.0f, new Color(0.52f, 0.54f, 0.58f)), // noon
        new SkyKey(16.0f, new Color(0.46f, 0.44f, 0.36f)), // afternoon
        new SkyKey(17.5f, new Color(0.38f, 0.18f, 0.06f)), // sunset
        new SkyKey(19.0f, new Color(0.10f, 0.06f, 0.08f)), // dusk
        new SkyKey(20.5f, new Color(0.02f, 0.02f, 0.06f)), // night
    };


    // -------------------------------------------------------
    // SHADOWS
    // -------------------------------------------------------

    // Shadow strength: none at night, soft at golden hour, full around noon
    private static readonly SkyKey[] ShadowKeys =
    {
        new SkyKey( 6.0f, Color.clear, 0.0f),
        new SkyKey( 7.0f, Color.clear, 0.5f),
        new SkyKey(10.0f, Color.clear, 1.0f),
        new SkyKey(14.0f, Color.clear, 1.0f),
        new SkyKey(17.0f, Color.clear, 0.7f),
        new SkyKey(18.5f, Color.clear, 0.2f),
        new SkyKey(19.5f, Color.clear, 0.0f),
    };


    // -------------------------------------------------------
    // FOG
    // -------------------------------------------------------

    // Warm haze at sunrise and sunset, blending to a light gray
    private static readonly Color FogGoldenWarm = new Color(0.90f, 0.55f, 0.25f);
    private static readonly Color FogGoldenGray = new Color(0.75f, 0.72f, 0.68f);

    // Clear day haze and dark night fog
    private static readonly Color FogDay   = new Color(0.72f, 0.76f, 0.82f);
    private static readonly Color FogNight = new Color(0.01f, 0.01f, 0.03f);


    // -------------------------------------------------------
    // SUN PATH
    // -------------------------------------------------------

    // Hours of sunrise and sunset, and the length of daylight
    private const float SUNRISE_HOUR = 6f;
    private const float DAY_LENGTH   = 12f;

    // Highest sun angle at noon, and the offset that keeps the sun
    // slightly below the horizon at sunrise and sunset
    private const float SUN_PEAK_ANGLE  = 75f;
    private const float SUN_ANGLE_SHIFT = 10f;

    // Sun direction: East at sunrise (90 degrees), West at sunset (270 degrees)
    private const float SUN_EAST = 90f;
    private const float SUN_WEST = 270f;


    // -------------------------------------------------------
    // RUNTIME STATE
    // -------------------------------------------------------

    private TimeManager timeManager;
    private Light sun;


    // -------------------------------------------------------
    // UNITY EVENTS
    // -------------------------------------------------------

    void Start()
    {
        timeManager = FindAnyObjectByType<TimeManager>();
        sun = GetComponent<Light>();
        sun.shadows = LightShadows.Soft;
    }

    void Update()
    {
        if (timeManager == null || sun == null) return;

        float hour = timeManager.GetCurrentHour();
        UpdateSunPosition(hour);
        UpdateSunAppearance(hour);
        UpdateAmbientLight(hour);
        UpdateFog(hour);
        UpdateShadows(hour);
    }


    // -------------------------------------------------------
    // SKY UPDATES
    // -------------------------------------------------------

    // Moves the sun along an arc from East to West, highest at noon
    void UpdateSunPosition(float hour)
    {
        // 0 at sunrise, 0.5 at noon, 1 at sunset
        float dayProgress = Mathf.Clamp01((hour - SUNRISE_HOUR) / DAY_LENGTH);

        float sunAngle     = Mathf.Sin(dayProgress * Mathf.PI) * SUN_PEAK_ANGLE - SUN_ANGLE_SHIFT;
        float sunDirection = Mathf.Lerp(SUN_EAST, SUN_WEST, dayProgress);

        transform.eulerAngles = new Vector3(sunAngle, sunDirection, 0f);
    }

    // Sets the sun color and strength for this hour
    void UpdateSunAppearance(float hour)
    {
        Sample(SunKeys, hour, out Color color, out float intensity);
        sun.color     = color;
        sun.intensity = intensity;
    }

    // Sets the flat ambient light for this hour
    void UpdateAmbientLight(float hour)
    {
        Sample(AmbientKeys, hour, out Color ambient, out _);
        RenderSettings.ambientMode  = AmbientMode.Flat;
        RenderSettings.ambientLight = ambient;
    }

    // Sets the fog: warm haze in the golden hours, light haze by day, dark fog at night
    void UpdateFog(float hour)
    {
        bool isDay        = hour >= 6f && hour <= 19.5f;
        bool isGoldenHour = (hour >= 6f && hour < 8f) || (hour >= 17f && hour <= 19f);

        RenderSettings.fog     = true;
        RenderSettings.fogMode = FogMode.Linear;

        if (isGoldenHour)
        {
            // Warmest right at sunrise and sunset, fading to gray over two hours
            float blend = hour < 12f ? (hour - 6f) / 2f : (19f - hour) / 2f;
            SetFog(Color.Lerp(FogGoldenWarm, FogGoldenGray, blend), 60f, 320f);
        }
        else if (isDay)
        {
            SetFog(FogDay, 100f, 500f);
        }
        else
        {
            SetFog(FogNight, 25f, 120f);
        }
    }

    // Sets the shadow strength for this hour
    void UpdateShadows(float hour)
    {
        Sample(ShadowKeys, hour, out _, out float strength);
        sun.shadowStrength = strength;
    }


    // -------------------------------------------------------
    // HELPERS
    // -------------------------------------------------------

    // Finds the two keyframes around this hour and blends between them.
    // Before the first keyframe or after the last one, the edge value is used.
    static void Sample(SkyKey[] keys, float hour, out Color color, out float value)
    {
        if (hour <= keys[0].hour)
        {
            color = keys[0].color;
            value = keys[0].value;
            return;
        }

        for (int i = 0; i < keys.Length - 1; i++)
        {
            SkyKey from = keys[i];
            SkyKey to   = keys[i + 1];
            if (hour < to.hour)
            {
                float blend = (hour - from.hour) / (to.hour - from.hour);
                color = Color.Lerp(from.color, to.color, blend);
                value = Mathf.Lerp(from.value, to.value, blend);
                return;
            }
        }

        SkyKey last = keys[keys.Length - 1];
        color = last.color;
        value = last.value;
    }

    static void SetFog(Color color, float startDistance, float endDistance)
    {
        RenderSettings.fogColor         = color;
        RenderSettings.fogStartDistance = startDistance;
        RenderSettings.fogEndDistance   = endDistance;
    }
}
