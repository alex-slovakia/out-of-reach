using System;
using OutOfReach.Contracts;
using UnityEngine;

namespace OutOfReach.Presentation.MarineOcean
{
    [Serializable]
    public struct OceanContactPoint
    {
        public Transform Anchor;
        [Range(.1f, 3f)] public float Radius;
        [Range(0, 1)] public float Strength;
        public OceanContactPoint(Transform anchor, float radius, float strength = 1)
        { Anchor = anchor; Radius = radius; Strength = strength; }
    }

    /// <summary>Decorative water only. Owns its renderer property block and dedicated splash system.
    /// Reads the mission clock for pause; never advances simulation, moves the deck, or applies forces.</summary>
    [DisallowMultipleComponent]
    public sealed class MarineOceanPresenter : MonoBehaviour
    {
        public const int MaximumContacts = 16;
        [SerializeField] private MeshRenderer surface;
        [SerializeField] private ParticleSystem splashes;
        [SerializeField] private OceanContactPoint[] contacts = Array.Empty<OceanContactPoint>();
        [SerializeField] private bool automaticSplashes = true;
        [SerializeField] private bool reducedMotion;
        private ISimulationClock clock;
        private MaterialPropertyBlock properties, originalProperties;
        private bool captured, particleRunning;
        private Vector3 phase;
        private float amplitude = .075f, nearRadius = 90;
        private readonly Vector4[] contactData = new Vector4[MaximumContacts];
        private readonly float[] previousCrest = new float[MaximumContacts];
        private readonly float[] cooldown = new float[MaximumContacts];
        private uint burstSerial;
        private static readonly int PhaseId = Shader.PropertyToID("_OceanPhase");
        private static readonly int MotionId = Shader.PropertyToID("_WaveStrength");
        private static readonly int OriginId = Shader.PropertyToID("_WaterOrigin");
        private static readonly int ContactsId = Shader.PropertyToID("_ContactPoints");
        private static readonly int CountId = Shader.PropertyToID("_ContactCount");
        private const float Tau = Mathf.PI * 2;

        public bool IsClockBound => clock != null;
        public bool ReducedMotion => reducedMotion;
        public MeshRenderer Surface => surface;
        public int ContactCount => contacts == null ? 0 : Mathf.Min(contacts.Length, MaximumContacts);

        /// <summary>Authoring / composition injection. Supplied particle system must be dedicated to this presenter.</summary>
        public void ConfigureAssets(MeshRenderer waterRenderer, ParticleSystem contactSplashes)
        {
            Restore();
            surface = waterRenderer; splashes = contactSplashes;
            if (isActiveAndEnabled) { Capture(); ApplySurface(); }
        }

        public void ClearBinding()
        {
            Restore();
            clock = null;
            ResetContactHistory();
        }

        /// <summary>Bind Core's read-only clock and explicit anchors. Copies the array once, never polls the scene.</summary>
        public void Configure(ISimulationClock simulationClock, OceanContactPoint[] points)
        {
            if (simulationClock == null) throw new ArgumentNullException(nameof(simulationClock));
            SetContacts(points);
            BindClock(simulationClock);
        }

        /// <summary>Use with the prefab's already-authored explicit contact references.</summary>
        public void BindClock(ISimulationClock simulationClock)
        {
            if (simulationClock == null) throw new ArgumentNullException(nameof(simulationClock));
            clock = simulationClock;
            ResetContactHistory();
            if (isActiveAndEnabled) { Capture(); ApplySurface(); }
        }

        /// <summary>Replace supplied contact references; positions use XZ and are projected onto this visual sea level.</summary>
        public void SetContacts(OceanContactPoint[] points)
        {
            int count = points == null ? 0 : points.Length;
            if (count > MaximumContacts) throw new ArgumentOutOfRangeException(nameof(points), "At most 16 ocean contacts.");
            for (int i = 0; i < count; ++i)
                if (!Finite(points[i].Radius) || points[i].Radius < .1f || points[i].Radius > 3 ||
                    !Finite(points[i].Strength) || points[i].Strength < 0 || points[i].Strength > 1)
                    throw new ArgumentException("Contact radius must be .1–3 metres; strength 0–1.", nameof(points));
            contacts = count == 0 ? Array.Empty<OceanContactPoint>() : (OceanContactPoint[])points.Clone();
            ResetContactHistory();
        }

        public void SetReducedMotion(bool value)
        {
            if (reducedMotion == value) return;
            reducedMotion = value;
            ResetContactHistory();
            if (value && splashes) splashes.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleRunning = false;
            if (isActiveAndEnabled) ApplySurface();
        }

        /// <summary>Optional visual cue from composition. Rate-limited, disabled while paused/reduced/invalid.</summary>
        public bool TriggerContactSplash(int index, float strength = 1)
        {
            if (!isActiveAndEnabled || reducedMotion || Paused() || !surface || !splashes ||
                contacts == null || index < 0 || index >= contacts.Length || cooldown[index] > 0 || !Finite(strength)) return false;
            var point = contacts[index];
            if (!point.Anchor || point.Strength <= 0) return false;
            Vector3 position = point.Anchor.position;
            if (!Finite(position.x) || !Finite(position.z)) return false;
            float amount = Mathf.Clamp01(strength) * point.Strength;
            if (amount <= 0) return false;
            if (!particleRunning) { splashes.Play(false); particleRunning = true; }
            // Tiny non-colliding ballistic sprites. This is an authored contact cue, not simulated water impact.
            uint serial = ++burstSerial;
            for (int i = 0; i < 3; ++i)
            {
                float angle = (serial * 2.399963f + i * 2.094395f + index) % Tau;
                var radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 emissionPosition = position + radial * point.Radius;
                emissionPosition.y = surface.transform.position.y + VisualWaveHeight(emissionPosition) + .025f;
                var emit = new ParticleSystem.EmitParams
                {
                    position = emissionPosition,
                    velocity = radial * (.16f + .12f * amount) + Vector3.up * (.48f + .20f * amount),
                    startLifetime = .40f + .08f * i,
                    startSize = .035f + .018f * amount,
                    startColor = new Color(.68f, .75f, .76f, .30f * amount),
                    randomSeed = serial * 13u + (uint)i + 1u
                };
                splashes.Emit(emit, 1);
            }
            cooldown[index] = 1.6f;
            return true;
        }

        private void OnEnable() { Capture(); ResetContactHistory(); ApplySurface(); }
        private void LateUpdate()
        {
            if (!surface) { StopParticles(); return; }
            Capture();
            bool paused = Paused();
            bool animate = !paused && !reducedMotion;
            if (animate)
            {
                float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0, .1f);
                // Visual phases intentionally remain real-time when the mission time scale changes.
                // Three independent wrapped angles avoid precision loss and discontinuous large-time wrap.
                phase.x = Mathf.Repeat(phase.x + dt * Tau / 6.4f, Tau);
                phase.y = Mathf.Repeat(phase.y + dt * Tau / 4.1f, Tau);
                phase.z = Mathf.Repeat(phase.z + dt * Tau / 2.7f, Tau);
                for (int i = 0; i < ContactCount; ++i)
                {
                    cooldown[i] = Mathf.Max(0, cooldown[i] - dt);
                    float crest = ContactCrest(i);
                    if (automaticSplashes && previousCrest[i] <= .90f && crest > .90f) TriggerContactSplash(i, .6f);
                    previousCrest[i] = crest;
                }
            }
            if (splashes)
            {
                if (!animate && particleRunning) { splashes.Pause(false); particleRunning = false; }
                else if (animate && !particleRunning) { splashes.Play(false); particleRunning = true; }
            }
            ApplySurface();
        }

        private bool Paused()
        {
            if (clock == null) return true; // Deliberately inert until composition binds the real clock.
            try { return clock.IsPaused || clock.TimeScale <= 0; }
            catch (Exception exception) { clock = null; Debug.LogException(exception, this); return true; }
        }
        private void Capture()
        {
            if (captured || !surface) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            if (originalProperties == null) originalProperties = new MaterialPropertyBlock();
            surface.GetPropertyBlock(originalProperties); surface.GetPropertyBlock(properties); captured = true;
            var material = surface.sharedMaterial;
            if (material && material.HasProperty("_Amplitude")) amplitude = Mathf.Clamp(material.GetFloat("_Amplitude"), 0, .25f);
            if (material && material.HasProperty("_NearRadius")) nearRadius = Mathf.Max(1, material.GetFloat("_NearRadius"));
        }
        private void ApplySurface()
        {
            if (!captured || !surface) return;
            int count = Mathf.Min(ContactCount, MaximumContacts);
            for (int i = 0; i < MaximumContacts; ++i)
            {
                contactData[i] = Vector4.zero;
                if (i >= count || !contacts[i].Anchor) continue;
                Vector3 p = contacts[i].Anchor.position;
                if (Finite(p.x) && Finite(p.z)) contactData[i] = new Vector4(p.x, p.z, contacts[i].Radius, contacts[i].Strength);
            }
            properties.SetVector(PhaseId, new Vector4(phase.x, phase.y, phase.z, 0));
            properties.SetFloat(MotionId, reducedMotion ? 0 : 1);
            properties.SetVector(OriginId, surface.transform.position);
            properties.SetInteger(CountId, count);
            properties.SetVectorArray(ContactsId, contactData);
            surface.SetPropertyBlock(properties);
        }
        private void ResetContactHistory()
        {
            for (int i = 0; i < MaximumContacts; ++i) { cooldown[i] = .6f; previousCrest[i] = ContactCrest(i); }
        }
        private float ContactCrest(int index)
        {
            if (!surface || index >= ContactCount || !contacts[index].Anchor) return 0;
            Vector3 p = contacts[index].Anchor.position - surface.transform.position;
            return Mathf.Sin(p.x * .52f + p.z * .19f + phase.x);
        }
        private float VisualWaveHeight(Vector3 worldPosition)
        {
            Vector3 p = worldPosition - surface.transform.position;
            float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(nearRadius * .52f, nearRadius, Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z))));
            return amplitude * fade * (.55f * Mathf.Sin(p.x * .52f + p.z * .19f + phase.x)
                + .29f * Mathf.Sin(p.x * -.26f + p.z * .74f + phase.y)
                + .16f * Mathf.Sin(p.x * .83f + p.z * -.62f + phase.z));
        }
        private void StopParticles()
        {
            if (splashes) splashes.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleRunning = false;
        }
        private void Restore()
        {
            StopParticles();
            if (captured && surface) surface.SetPropertyBlock(originalProperties);
            captured = false;
        }
        private void OnDisable() => Restore();
        private void OnDestroy() => Restore();
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
