// CompoundColors.cs
// The shared color palette of the compound: building colors, street light state
// colors, dashboard colors and power level colors.
// The street lights and both dashboards read their colors from here,
// so changing a color here updates it everywhere.
using UnityEngine;

public static class CompoundColors
{
    // -------------------------------------------------------
    // BUILDING COLORS (taken from the compound renders)
    // -------------------------------------------------------

    // Warm sandy beige: the color of the main building walls
    public static readonly Color BeigeSand = new Color(0.784f, 0.725f, 0.604f);

    // Very dark gray: used for window frames and building edges
    public static readonly Color Charcoal = new Color(0.110f, 0.110f, 0.118f);

    // Warm gold: the LED accent strip color on the premium building
    public static readonly Color GoldAccent = new Color(0.788f, 0.592f, 0.227f);

    // Dark steel gray: used for railings and the parking canopy
    public static readonly Color DarkSteel = new Color(0.176f, 0.176f, 0.176f);

    // Fresh green: the grass and hedge color around the compound
    public static readonly Color CompoundGreen = new Color(0.290f, 0.486f, 0.247f);

    // Warm white: the color of the path and garden lights when on
    public static readonly Color WarmWhite = new Color(1.000f, 0.961f, 0.878f);


    // -------------------------------------------------------
    // SMART LIGHT STATE COLORS
    // Each state the light can be in has its own color
    // -------------------------------------------------------

    // Light is completely off (daytime, no power used)
    public static readonly Color LightOff = new Color(0.200f, 0.200f, 0.200f);

    // Light at 25% or less: save mode (around 20%)
    public static readonly Color LightNightMode = new Color(0.400f, 0.310f, 0.120f);

    // Light between 25% and 55%: dim night light (for example the 40% motion boost)
    public static readonly Color LightNight = new Color(0.788f, 0.592f, 0.227f);

    // Light above 55%: normal night (80%) and crowded zones (100%)
    public static readonly Color LightFull = new Color(1.000f, 0.961f, 0.878f);

    // Light is being controlled manually from the digital twin dashboard
    public static readonly Color LightManual = new Color(0.216f, 0.616f, 0.459f);

    // Light is being controlled by the AI prediction system
    public static readonly Color LightAI = new Color(0.498f, 0.467f, 0.867f);


    // -------------------------------------------------------
    // DASHBOARD UI COLORS
    // Colors used for all panels, text, and borders in the UI
    // -------------------------------------------------------

    // Dark see through background for all dashboard panels
    public static readonly Color UIBackground = new Color(0.110f, 0.110f, 0.118f, 0.92f);

    // Slightly lighter panel background for cards inside dashboards
    public static readonly Color UIPanel = new Color(0.176f, 0.176f, 0.200f, 0.95f);

    // Gold border color for panels and separators
    public static readonly Color UIBorder = new Color(0.788f, 0.592f, 0.227f, 0.60f);

    // Main text color: warm white, easy to read on dark backgrounds
    public static readonly Color UITextPrimary = new Color(1.000f, 0.961f, 0.878f);

    // Secondary text color: beige, used for labels and subtitles
    public static readonly Color UITextSecondary = new Color(0.784f, 0.725f, 0.604f);

    // Muted text color: used for less important information
    public static readonly Color UITextMuted = new Color(0.500f, 0.490f, 0.470f);


    // -------------------------------------------------------
    // POWER INDICATOR COLORS
    // Used in the energy dashboard to show consumption level
    // -------------------------------------------------------

    // Green: light is saving energy (low power usage)
    public static readonly Color PowerLow = new Color(0.290f, 0.686f, 0.310f);

    // Gold: moderate power usage
    public static readonly Color PowerMedium = new Color(0.788f, 0.592f, 0.227f);

    // Red: high power usage or all lights at 100%
    public static readonly Color PowerHigh = new Color(0.886f, 0.310f, 0.220f);


    // -------------------------------------------------------
    // HELPER FUNCTIONS
    // These functions return the right color automatically
    // -------------------------------------------------------

    // Give this function a brightness value (0 to 1) and it returns the matching light color
    public static Color GetLightColorByBrightness(float brightness)
    {
        // If brightness is zero the light is off
        if (brightness <= 0f) return LightOff;

        // If brightness is 25% or less it is in late night mode
        if (brightness <= 0.25f) return LightNightMode;

        // If brightness is 55% or less it is in normal night standby
        if (brightness <= 0.55f) return LightNight;

        // Anything above 55% means the light is at full brightness
        return LightFull;
    }

    // Give this function current watts and max watts to get the right power color
    public static Color GetPowerColor(float currentWatts, float maxWatts)
    {
        // Calculate how much of the maximum power is being used (0 to 1)
        float ratio = Mathf.Clamp01(currentWatts / maxWatts);

        // Below 35% usage means we are saving energy, show green
        if (ratio < 0.35f) return PowerLow;

        // Between 35% and 70% is moderate usage, show gold
        if (ratio < 0.70f) return PowerMedium;

        // Above 70% usage is high consumption, show red
        return PowerHigh;
    }
}
