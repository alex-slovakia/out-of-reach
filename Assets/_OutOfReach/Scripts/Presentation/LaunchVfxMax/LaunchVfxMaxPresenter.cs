using System;
using System.Collections.Generic;
using OutOfReach.Presentation.Cinematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace OutOfReach.Presentation.LaunchVfxMax
{
    /// <summary>
    /// LAUNCH-VFX-MAX-V01. Unity Particle Pack layers sampled from the existing departure playhead.
    /// No clock, flight, audio, camera or gameplay authority: it reads TransportDeparturePresenter.PresentedSeconds
    /// and never writes back. Pausing holds the playhead, so every particle holds too; rewinding clears and
    /// replays deterministically (fixed seeds, fixed sub-steps).
    ///
    /// Division of labour with LaunchFxV03 (kept): V03 owns the nozzle-clipped plume mesh, the trench steam
    /// billows and the pad bounce light. This presenter adds what V03 does not have: the ignition flash,
    /// turbulent fire and embers in the jet, sparks off the deflector, directional ground dust and debris
    /// (flanks only, no radial ring/shockwave: the user cancelled those), heat shimmer, the trench-exit steam
    /// jet, the lingering smoke column during the climb and a flickering light that follows the plume.
    /// Camera shake already lives in TransportDeparturePresenter (zero with reduced motion), and the grass gust
    /// is written by LaunchGrassGustPresenter from the same playhead, so neither is duplicated here.
    /// </summary>
    [DefaultExecutionOrder(650), DisallowMultipleComponent]
    public sealed class LaunchVfxMaxPresenter : MonoBehaviour
    {
        public const string Version = "LAUNCH-VFX-MAX-V01";
        private const float StepSeconds = 1f / 30f;
        private const float MaximumCatchUpSeconds = 3f;
        private const float PlumeFadeSeconds = 21.1f;

        [SerializeField] private TransportDeparturePresenter departure;
        [SerializeField] private Transform nozzle, deflector, pad, trenchExit;
        [SerializeField] private GameObject flamePrefab, smokeColumnPrefab, dustPrefab, sparksPrefab, heatPrefab, steamJetPrefab;

        [Header("Look (safe to tune live in Play mode)")]
        [SerializeField, Range(0f, 2f)] private float fireScale = 1f;
        [SerializeField, Range(0f, 2f)] private float smokeScale = 1f;
        [SerializeField, Range(0f, 2f)] private float dustScale = 1f;
        [SerializeField, Range(0f, 400f)] private float flashIntensity = 160f;
        [SerializeField, Range(0f, 200f)] private float plumeLightIntensity = 40f;
        [SerializeField] private bool sparks = true, heatShimmer = true, trenchJet = true;
        [SerializeField] private Color smokeTint = new Color(.60f, .58f, .54f, .85f);
        [SerializeField] private Color dustTint = new Color(.56f, .50f, .41f, .80f);
        [SerializeField] private Color steamTint = new Color(.88f, .86f, .80f, .70f);
        [SerializeField] private Color lightColour = new Color(1f, .60f, .28f);

        private enum Kind { Flame, Embers, Smoke, Dust, Debris, Sand, Sparks, Heat, Steam }

        private sealed class Layer
        {
            public Kind Kind;
            public ParticleSystem System;
            public Transform Emitter;
            public float BaseRate;
            public int Capacity;
        }

        private readonly List<Layer> layers = new List<Layer>();
        private GameObject views;
        private Light plumeLight;
        private Vector3 footprint, flow, side;
        private float lastSeconds = -1f;
        private bool lastReduced, heatAvailable;

        public bool IsBuilt => views;
        public float PresentedSeconds { get; private set; }
        public int LiveParticleCount { get; private set; }

        /// <summary>Editor-time binding only (installer). All prefabs are read, never mutated.</summary>
        public void Configure(TransportDeparturePresenter timeline, Transform engineNozzle, Transform deflectorPlane,
            Transform groundPad, Transform outlet, GameObject flame, GameObject smokeColumn, GameObject dust,
            GameObject sparkPrefab, GameObject heat, GameObject steamJet)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Configure launch VFX outside Play mode.");
            departure = timeline; nozzle = engineNozzle; deflector = deflectorPlane; pad = groundPad; trenchExit = outlet;
            flamePrefab = flame; smokeColumnPrefab = smokeColumn; dustPrefab = dust;
            sparksPrefab = sparkPrefab; heatPrefab = heat; steamJetPrefab = steamJet;
        }

        private void LateUpdate()
        {
            if (!departure || !nozzle || !pad || !departure.IsPlaying)
            { ResetViews(); return; }
            float seconds = departure.PresentedSeconds;
            if (!Finite(seconds) || seconds < TransportDepartureTimeline.IgnitionSeconds
                || seconds >= TransportDepartureTimeline.OrbitSeconds)
            { ResetViews(); return; }
            if (departure.IsSuspended) return;
            bool reduced = departure.ReducedMotion;
            if (!views) { BuildViews(); if (!views) return; }
            if (lastSeconds >= 0f && (seconds < lastSeconds || reduced != lastReduced)) ResetViews();
            lastReduced = reduced;

            float from = lastSeconds < 0f ? TransportDepartureTimeline.IgnitionSeconds : lastSeconds;
            float remaining = seconds - from;
            if (remaining <= 0f) return; // Paused: the authoritative playhead holds, so particles hold.
            if (!views.activeSelf) { views.SetActive(true); Restart(); }
            // Long seeks catch up at most a few seconds so a skip never stalls a frame.
            if (remaining > MaximumCatchUpSeconds) { from = seconds - MaximumCatchUpSeconds; remaining = MaximumCatchUpSeconds; }

            float t = from;
            while (remaining > 1e-5f)
            {
                float dt = Mathf.Min(StepSeconds, remaining);
                t += dt; remaining -= dt;
                Step(t, dt, reduced);
            }
            PresentLight(seconds, reduced);
            PresentedSeconds = seconds;
            lastSeconds = seconds;
            int live = 0;
            foreach (var layer in layers) if (layer.System) live += layer.System.particleCount;
            LiveParticleCount = live;
        }

        // ---------- sampled envelopes (pure functions of the playhead) ----------

        private static float Thrust(float seconds) =>
            Smooth((seconds - TransportDepartureTimeline.IgnitionSeconds) / .6f) * (1f - Smooth((seconds - PlumeFadeSeconds) / .75f));

        private static float Flash(float seconds)
        {
            float age = seconds - TransportDepartureTimeline.IgnitionSeconds;
            return age < 0f ? 0f : Smooth(age / .05f) * Mathf.Exp(-age * 6.5f);
        }

        /// <summary>1 while the jet still reaches the deflector, fading to 0 by ~26 m of climb (same heights as V03).</summary>
        private static float GroundContact(float seconds)
        {
            if (seconds <= TransportDepartureTimeline.LiftoffSeconds) return 1f;
            float height = TransportDepartureTimeline.Evaluate(seconds, false).HeightMetres;
            return 1f - Smooth((height - 4f) / 22f);
        }

        private static float Climb(float seconds) => Smooth((seconds - TransportDepartureTimeline.LiftoffSeconds) / 3.8f);

        private void Step(float seconds, float dt, bool reduced)
        {
            float thrust = Thrust(seconds);
            float flash = reduced ? 0f : Flash(seconds);
            float contact = GroundContact(seconds);
            float climb = Climb(seconds);
            float budget = reduced ? .25f : 1f;
            Vector3 down = nozzle.forward;
            Vector3 exit = nozzle.position + down * 1.2f;
            Vector3 columnSource = nozzle.position + down * Mathf.Lerp(6f, 16f, climb);

            foreach (var layer in layers)
            {
                var system = layer.System;
                if (!system) continue;
                float rate = 0f;
                switch (layer.Kind)
                {
                    case Kind.Flame:
                    case Kind.Embers:
                    {
                        Aim(layer.Emitter, exit, down);
                        bool flame = layer.Kind == Kind.Flame;
                        float grow = Mathf.Lerp(.55f, 1f, climb) * fireScale;
                        var main = system.main;
                        main.startSpeed = flame ? Range(14f * grow, 30f * grow) : Range(18f * grow, 34f * grow);
                        main.startSize = flame ? Range(2.6f * grow, 5.8f * grow) : Range(.06f, .18f);
                        rate = layer.BaseRate * thrust * (1f + 2.5f * flash);
                        break;
                    }
                    case Kind.Smoke:
                    {
                        Aim(layer.Emitter, columnSource, down);
                        var main = system.main;
                        main.startSize = Range(6f * smokeScale, 11f * smokeScale);
                        bool climbing = seconds >= TransportDepartureTimeline.LiftoffSeconds - .3f;
                        rate = climbing ? layer.BaseRate * thrust * Mathf.Lerp(.6f, 1f, climb) : 0f;
                        break;
                    }
                    case Kind.Dust:
                    case Kind.Debris:
                    case Kind.Sand:
                        rate = layer.BaseRate * contact * thrust * dustScale;
                        if (layer.Kind == Kind.Debris && reduced) rate = 0f;
                        break;
                    case Kind.Sparks:
                        rate = sparks && !reduced ? layer.BaseRate * contact * thrust * (1f + 2f * flash) : 0f;
                        break;
                    case Kind.Heat:
                        rate = heatShimmer && heatAvailable && !reduced ? layer.BaseRate * thrust * Mathf.Max(contact, .35f) : 0f;
                        break;
                    case Kind.Steam:
                        rate = trenchJet ? layer.BaseRate * contact * Smooth((seconds - 9.35f) / .5f) : 0f;
                        break;
                }
                var emission = system.emission;
                emission.rateOverTimeMultiplier = rate * budget;
                emission.enabled = rate > 0f;
                var mainModule = system.main;
                mainModule.maxParticles = Mathf.Max(8, Mathf.RoundToInt(layer.Capacity * budget));
                system.Simulate(dt, false, false, false);
            }
        }

        private void PresentLight(float seconds, bool reduced)
        {
            if (!plumeLight) return;
            float thrust = Thrust(seconds);
            float flash = reduced ? 0f : Flash(seconds);
            float flicker = reduced ? 1f
                : .80f + .14f * Mathf.PerlinNoise(seconds * 9.5f, .37f) + .06f * Mathf.Sin(seconds * 41f);
            float intensity = flashIntensity * flash + plumeLightIntensity * thrust * flicker * (reduced ? .5f : 1f);
            plumeLight.transform.position = nozzle.position + nozzle.forward * Mathf.Lerp(4f, 10f, Climb(seconds));
            plumeLight.intensity = intensity;
            plumeLight.range = Mathf.Lerp(45f, 70f, flash);
            plumeLight.enabled = intensity > .01f;
        }

        // ---------- views ----------

        private void BuildViews()
        {
            if (!flamePrefab && !smokeColumnPrefab && !dustPrefab) return;
            views = new GameObject("Launch VFX MAX — Particle Pack sampled views") { hideFlags = HideFlags.DontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(views, gameObject.scene);
            ComputeFrame();
            heatAvailable = OpaqueTextureAvailable();
            int seed = 0;

            // Jet: turbulent fire and embers follow the nozzle (positions re-aimed every step).
            AddInstance(flamePrefab, "FlameStream", Kind.Flame, nozzle.position, nozzle.forward, ++seed,
                life: Range(.45f, .8f), cone: 6f, radius: 1.0f, rate: 95f, capacity: 260, gravity: -.05f, tint: null);
            AddInstance(flamePrefab, "FireEmbers", Kind.Embers, nozzle.position, nozzle.forward, ++seed,
                life: Range(.6f, 1.2f), cone: 14f, radius: 1.0f, rate: 120f, capacity: 260, gravity: .3f, tint: null);

            // Lingering column: world-space smoke shed by the climbing nozzle; it stays where it was born.
            AddInstance(smokeColumnPrefab, "Smoke", Kind.Smoke, nozzle.position, nozzle.forward, ++seed,
                life: Range(9f, 13f), cone: 32f, radius: 2.2f, rate: 26f, capacity: 340, gravity: -.012f, tint: smokeTint,
                speed: Range(1.5f, 4f));

            // Ground dust and debris along the two deflector flanks (directional, low, never a ring).
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector3 dir = (side * sign + flow * .25f).normalized;
                Vector3 aim = (dir + Vector3.up * .16f).normalized;
                Vector3 at = footprint + dir * 5f + Vector3.up * .6f;
                AddInstance(dustPrefab, "DustStorm", Kind.Dust, at, aim, ++seed,
                    life: Range(3f, 5f), cone: 28f, radius: 2.6f, rate: 34f, capacity: 160, gravity: -.02f, tint: dustTint,
                    speed: Range(9f, 17f), size: Range(5f, 9f));
                AddInstance(dustPrefab, "RockDebris", Kind.Debris, at, (dir + Vector3.up * .5f).normalized, ++seed,
                    life: Range(1.2f, 2f), cone: 30f, radius: 2f, rate: 10f, capacity: 40, gravity: 1f, tint: null,
                    speed: Range(10f, 18f), size: Range(.25f, .7f));
                AddInstance(dustPrefab, "SandParticles", Kind.Sand, at, aim, ++seed,
                    life: Range(.8f, 1.4f), cone: 24f, radius: 2.4f, rate: 140f, capacity: 260, gravity: .4f, tint: dustTint,
                    speed: Range(14f, 22f), size: Range(.05f, .12f));
            }

            // Sparks off the deflector impingement point, thrown up and out.
            AddInstance(sparksPrefab, "SparksEffect", Kind.Sparks, footprint + Vector3.up * .5f, (Vector3.up + flow * .35f).normalized, ++seed,
                life: Range(.3f, .8f), cone: 65f, radius: 1.5f, rate: 150f, capacity: 300, gravity: 1.2f, tint: null,
                speed: Range(12f, 26f), size: Range(.03f, .08f));

            // Heat shimmer rising off the pad (needs the URP opaque texture; skipped otherwise).
            if (heatAvailable)
                AddInstance(heatPrefab, "HeatDistortion", Kind.Heat, footprint + Vector3.up * 2.5f, Vector3.up, ++seed,
                    life: Range(1.5f, 2.5f), cone: 25f, radius: 4f, rate: 9f, capacity: 40, gravity: 0f, tint: null,
                    speed: Range(2f, 5f), size: Range(8f, 14f));

            // Pressurised deluge steam venting from the actual trench outlet along the flow.
            if (trenchExit)
            {
                Vector3 jet = (flow + Vector3.up * .22f).normalized;
                AddInstance(steamJetPrefab, "PressurisedSteam", Kind.Steam, trenchExit.position + Vector3.up * .8f, jet, ++seed,
                    life: Range(1.5f, 3f), cone: 14f, radius: 1.5f, rate: 26f, capacity: 120, gravity: -.04f, tint: steamTint,
                    speed: Range(14f, 22f), size: Range(3f, 7f));
            }

            var lightObject = new GameObject("Plume flash and flicker light");
            lightObject.transform.SetParent(views.transform, false);
            plumeLight = lightObject.AddComponent<Light>();
            plumeLight.type = LightType.Point; plumeLight.color = lightColour; plumeLight.range = 45f;
            plumeLight.shadows = LightShadows.None; plumeLight.renderMode = LightRenderMode.ForcePixel; plumeLight.enabled = false;
            views.SetActive(false);
        }

        private void ComputeFrame()
        {
            footprint = new Vector3(nozzle.position.x, pad.position.y + .1f, nozzle.position.z);
            if (deflector)
            {
                float dot = Vector3.Dot(nozzle.forward, deflector.up);
                if (dot < -.1f)
                {
                    float distance = Vector3.Dot(deflector.position - nozzle.position, deflector.up) / dot;
                    if (Finite(distance) && distance > 0f && distance < 200f)
                    {
                        footprint = nozzle.position + nozzle.forward * distance;
                        footprint.y = pad.position.y + .1f;
                    }
                }
            }
            flow = trenchExit ? Vector3.ProjectOnPlane(trenchExit.position - footprint, Vector3.up) : Vector3.zero;
            if (flow.sqrMagnitude < .25f) flow = Vector3.ProjectOnPlane(pad.forward, Vector3.up);
            flow = flow.sqrMagnitude > 1e-4f ? flow.normalized : Vector3.forward;
            side = Vector3.Cross(Vector3.up, flow).normalized;
        }

        private void AddInstance(GameObject prefab, string child, Kind kind, Vector3 position, Vector3 direction, int seed,
            ParticleSystem.MinMaxCurve life, float cone, float radius, float rate, int capacity, float gravity, Color? tint,
            ParticleSystem.MinMaxCurve? speed = null, ParticleSystem.MinMaxCurve? size = null)
        {
            if (!prefab) return;
            var instance = Instantiate(prefab, views.transform);
            instance.name = prefab.name + " · " + child + " · " + seed;
            instance.transform.localScale = Vector3.one;
            foreach (var audio in instance.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            foreach (var light in instance.GetComponentsInChildren<Light>(true)) light.enabled = false;

            ParticleSystem chosen = null;
            foreach (var system in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var e = system.emission; e.enabled = false;
                if (!chosen && system.name == child) chosen = system;
                else if (system.gameObject != instance) system.gameObject.SetActive(false);
            }
            if (!chosen) { Release(instance); return; }
            // The chosen system emits on its own; any other system on the root object stays silent.
            chosen.gameObject.SetActive(true);
            var emitter = chosen.transform;
            emitter.SetParent(views.transform, true);
            emitter.localScale = Vector3.one;
            if (instance != chosen.gameObject) Release(instance);
            emitter.name = prefab.name + " · " + child + " · " + seed;

            chosen.useAutoRandomSeed = false;
            chosen.randomSeed = (uint)(7919 + seed * 101);
            var main = chosen.main;
            main.playOnAwake = false; main.loop = true; main.prewarm = false; main.startDelay = 0f;
            main.stopAction = ParticleSystemStopAction.None;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.simulationSpeed = 1f; main.useUnscaledTime = true;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.startLifetime = life;
            main.startSize3D = false;
            if (speed.HasValue) main.startSpeed = speed.Value;
            if (size.HasValue) main.startSize = size.Value;
            main.gravityModifier = gravity;
            main.maxParticles = capacity;
            if (tint.HasValue) main.startColor = tint.Value;

            var emission = chosen.emission;
            emission.enabled = false;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            emission.rateOverDistance = 0f;
            emission.rateOverTime = rate;

            // Deterministic direction: a cone along the emitter's +Z. Local-space velocity/force modules
            // authored for the demo scene would point elsewhere once re-aimed, so they are switched off.
            var shape = chosen.shape;
            shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = cone; shape.radius = radius; shape.radiusThickness = 1f; shape.arc = 360f;
            shape.position = Vector3.zero; shape.rotation = Vector3.zero; shape.scale = Vector3.one;
            shape.alignToDirection = false;
            var velocity = chosen.velocityOverLifetime; velocity.enabled = false;
            var force = chosen.forceOverLifetime; force.enabled = false;
            var inherit = chosen.inheritVelocity; inherit.enabled = false;
            var external = chosen.externalForces; external.enabled = false;
            var subEmitters = chosen.subEmitters; subEmitters.enabled = false;
            var collision = chosen.collision; collision.enabled = false;
            var trigger = chosen.trigger; trigger.enabled = false;
            var lights = chosen.lights; lights.enabled = false;

            var renderer = chosen.GetComponent<ParticleSystemRenderer>();
            if (renderer)
            {
                renderer.maxParticleSize = kind == Kind.Smoke || kind == Kind.Dust || kind == Kind.Steam ? 1.5f : 1f;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.sortMode = ParticleSystemSortMode.Distance;
                renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            Aim(emitter, position, direction);
            layers.Add(new Layer { Kind = kind, System = chosen, Emitter = emitter, BaseRate = rate, Capacity = capacity });
        }

        private void Restart()
        {
            foreach (var layer in layers)
            {
                if (!layer.System) continue;
                layer.System.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                layer.System.Play(false);
                layer.System.Pause(false);
            }
        }

        private void ResetViews()
        {
            if (lastSeconds < 0f && (!views || !views.activeSelf)) return;
            foreach (var layer in layers)
                if (layer.System) layer.System.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (plumeLight) plumeLight.enabled = false;
            if (views) views.SetActive(false);
            lastSeconds = -1f; LiveParticleCount = 0; PresentedSeconds = 0f;
        }

        private void OnDisable() => ResetViews();

        private void OnDestroy()
        {
            Release(views);
            views = null; plumeLight = null; layers.Clear();
        }

        // ---------- helpers ----------

        private static void Aim(Transform emitter, Vector3 position, Vector3 direction)
        {
            if (!emitter) return;
            Vector3 upHint = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > .95f ? Vector3.forward : Vector3.up;
            emitter.SetPositionAndRotation(position, Quaternion.LookRotation(direction, upHint));
        }

        private static bool OpaqueTextureAvailable()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (!pipeline) return false;
            var property = pipeline.GetType().GetProperty("supportsCameraOpaqueTexture");
            return property != null && property.GetValue(pipeline) is bool on && on;
        }

        private static ParticleSystem.MinMaxCurve Range(float min, float max) => new ParticleSystem.MinMaxCurve(min, max);
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        private static void Release(UnityEngine.Object obj)
        {
            if (!obj) return;
            if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj);
        }
    }
}
