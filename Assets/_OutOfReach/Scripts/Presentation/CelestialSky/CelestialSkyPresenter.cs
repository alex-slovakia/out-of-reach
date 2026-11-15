using OutOfReach.Contracts;
using UnityEngine;

namespace OutOfReach.Presentation.CelestialSky
{
    /// <summary>
    /// Orients every material using "OutOfReach/Skybox/Celestial Panoramic" by publishing one global
    /// sky-from-world quaternion. Reads the authoritative mission clock (read-only) for the daily Horizons
    /// sample and follows whichever directional light is the scene Sun, so the light, the real Sun direction
    /// and the NASA SVS / Gaia DR2 stars stay consistent. It never changes RenderSettings, the light,
    /// exposure or any mission state. Disabled or absent, materials fall back to their static day-0 value.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CelestialSkyPresenter : MonoBehaviour
    {
        public const string GlobalRotationProperty = "_OorCelestialSkyRotation";
        private static readonly int GlobalRotation = Shader.PropertyToID(GlobalRotationProperty);
        private static readonly int GlobalSun = Shader.PropertyToID("_OorCelestialSun");
        private static readonly int GlobalEarth = Shader.PropertyToID("_OorCelestialEarth");

        [Tooltip("Directional light acting as the Sun. Empty uses RenderSettings.sun.")]
        [SerializeField] private Light sun;
        [Tooltip("Daily sample used before a mission clock is bound (0 = 2026-11-14 08:00 UTC).")]
        [SerializeField, Range(0, CelestialSkyFrame.SampleCount - 1)] private int dayWithoutClock;

        private ISimulationClock clock;
        private Quaternion lastLight = new Quaternion(0f, 0f, 0f, 0f);
        private int lastDay = -1;

        /// <summary>Current rotation from world directions to sky-frame (ICRF) directions.</summary>
        public Quaternion SkyFromWorld { get; private set; } = CelestialEphemeris2026.DefaultSkyFromWorld;
        /// <summary>Daily Horizons sample in use (index 0 = 2026-11-14 08:00 UTC).</summary>
        public int DayIndex { get; private set; }

        /// <summary>World direction from the Mars-centre proxy observer to Earth's centre (for a distant point).</summary>
        public Vector3 WorldDirectionToEarth =>
            CelestialSkyFrame.ToWorld(SkyFromWorld, CelestialEphemeris2026.EarthFromObserver[DayIndex]);
        /// <summary>World direction to the Sun; equals the negated light forward vector while a light is bound.</summary>
        public Vector3 WorldDirectionToSun =>
            CelestialSkyFrame.ToWorld(SkyFromWorld, CelestialEphemeris2026.SunFromObserver[DayIndex]);

        /// <summary>World direction of an ICRF/J2000 catalogue position (degrees), e.g. to place a finale marker.</summary>
        public Vector3 WorldDirectionFromRaDec(double rightAscensionDeg, double declinationDeg)
        {
            return CelestialSkyFrame.ToWorld(SkyFromWorld, CelestialSkyFrame.FromRaDec(rightAscensionDeg, declinationDeg));
        }

        /// <summary>Optional composition hook. Without it, <see cref="dayWithoutClock"/> is used.</summary>
        public void Configure(ISimulationClock simulationClock)
        {
            clock = simulationClock;
            Apply(true);
        }

        public void ConfigureSun(Light directionalSun)
        {
            sun = directionalSun;
            Apply(true);
        }

        private void OnEnable() => Apply(true);
        private void OnValidate() => Apply(true);
        private void LateUpdate() => Apply(false);

        private void OnDisable()
        {
            // Shader falls back to the material's static day-0 values.
            Shader.SetGlobalVector(GlobalRotation, Vector4.zero);
            Shader.SetGlobalVector(GlobalSun, Vector4.zero);
            Shader.SetGlobalVector(GlobalEarth, Vector4.zero);
            lastDay = -1;
        }

        private void Apply(bool force)
        {
            int day = clock != null
                ? CelestialSkyFrame.DayIndex(clock.ElapsedSimulationSeconds)
                : Mathf.Clamp(dayWithoutClock, 0, CelestialSkyFrame.SampleCount - 1);
            Light light = sun ? sun : RenderSettings.sun;
            Quaternion lightRotation = light
                ? light.transform.rotation
                : Quaternion.Euler(CelestialSkyFrame.DefaultLightEuler);
            // Exact compare: Quaternion == tolerates ~0.16 deg, which would let the Sun drift from the light.
            if (!force && day == lastDay && lightRotation.Equals(lastLight)) return;
            lastDay = day;
            lastLight = lightRotation;
            DayIndex = day;
            SkyFromWorld = CelestialSkyFrame.ForDay(day, lightRotation * Vector3.forward);
            Shader.SetGlobalVector(GlobalRotation, CelestialSkyFrame.ToShaderVector(SkyFromWorld));
            Shader.SetGlobalVector(GlobalSun, CelestialSkyFrame.SunShaderVector(day));
            Shader.SetGlobalVector(GlobalEarth, CelestialSkyFrame.EarthShaderVector(day));
        }
    }
}
