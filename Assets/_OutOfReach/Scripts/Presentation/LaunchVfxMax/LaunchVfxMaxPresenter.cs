using System;
using System.Collections.Generic;
using OutOfReach.Presentation.Cinematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace OutOfReach.Presentation.LaunchVfxMax
{
    /// <summary>
    /// LAUNCH-VFX-MAX-V02 ("MEGA"). Unity Particle Pack layers sampled from the existing departure playhead.
    /// No clock, flight, audio, camera or gameplay authority: it reads TransportDeparturePresenter.PresentedSeconds
    /// and never writes back. Pausing holds the playhead, so every particle holds too; rewinding clears and
    /// replays deterministically (fixed seeds, fixed sub-steps).
    ///
    /// Layers: deluge steam burst at ignition; blinding flash; turbulent fire and embers in the jet; embers raining
    /// off the bells; huge lit exhaust billows rolling out of the trench both ways along the ground and down the
    /// trench (8-15 s, decelerating, lit warm from below by the plume lights); a fast decelerating dust front
    /// racing out across the pad; flank sand and rock debris; deflector sparks; heat shimmer; trench-exit steam
    /// jet; a thick drifting smoke column left behind during the climb; plume and under-cloud lights with flicker.
    /// LaunchFxV03 (plume mesh, trench steam, pad light) is kept underneath. Camera shake already lives in
    /// TransportDeparturePresenter (zero with reduced motion). The `_ThrusterGust` global (xyz pad, w strength)
    /// is published for any grass/vegetation shader that wants to flatten at ignition.
    /// </summary>
    [DefaultExecutionOrder(650), DisallowMultipleComponent]
    public sealed class LaunchVfxMaxPresenter : MonoBehaviour
    {
        public const string Version = "LAUNCH-VFX-MAX-V02";
        private const float StepSeconds = 1f / 30f;
        private const float MaximumCatchUpSeconds = 3f;
        private const float PlumeFadeSeconds = 21.1f;
        private static readonly int ThrusterGustId = Shader.PropertyToID("_ThrusterGust");

        [SerializeField] private TransportDeparturePresenter departure;
        [SerializeField] private Transform nozzle, deflector, pad, trenchExit;
        [SerializeField] private GameObject flamePrefab, smokeColumnPrefab, dustPrefab, sparksPrefab, heatPrefab, steamJetPrefab;
        [SerializeField] private GameObject billowPrefab, delugePrefab;

        [Header("Look (safe to tune live in Play mode)")]
        [SerializeField, Range(0f, 2f)] private float fireScale = 1f;
        [SerializeField, Range(0f, 2f)] private float smokeScale = 1f;
        [SerializeField, Range(0f, 2f)] private float billowScale = 1f;
        [SerializeField, Range(0f, 2f)] private float dustScale = 1f;
        [SerializeField, Range(0f, 1500f)] private float flashIntensity = 700f;
        [SerializeField, Range(0f, 400f)] private float plumeLightIntensity = 120f;
        [SerializeField, Range(0f, 400f)] private float cloudGlowIntensity = 100f;
        [SerializeField] private bool sparks = true, heatShimmer = true, trenchJet = true, groundShockwave = true, publishThrusterGust = false;
        [SerializeField] private Color smokeTint = new Color(.70f, .67f, .62f, .9f);
        [SerializeField] private Color billowTint = new Color(.82f, .78f, .72f, .92f);
        [SerializeField] private Color dustTint = new Color(.58f, .51f, .41f, .85f);
        [SerializeField] private Color steamTint = new Color(.95f, .95f, .93f, .85f);
        [SerializeField] private Color lightColour = new Color(1f, .58f, .26f);

        private enum Kind { Flame, Embers, Rain, Smoke, Billow, Deluge, Shock, Dust, Debris, Sand, Sparks, Heat, Steam }

        private sealed class Layer
        {
            public Kind Kind;
            public ParticleSystem System;
            public Transform Emitter;
            public ParticleSystemRenderer Renderer;
            public bool Ground;
            public float BaseRate;
            public int Capacity;
        }

        private readonly List<Layer> layers = new List<Layer>();
        private GameObject views;
        private Light plumeLight, cloudLight;
        private Vector3 footprint, flow, side;
        private float lastSeconds = -1f;
        private bool lastReduced, heatAvailable, gustPublished, failed;

        public bool IsBuilt => views;
        public float PresentedSeconds { get; private set; }
        public int LiveParticleCount { get; private set; }

        /// <summary>Editor-time binding only (installer). All prefabs are read, never mutated.</summary>
        public void Configure(TransportDeparturePresenter timeline, Transform engineNozzle, Transform deflectorPlane,
            Transform groundPad, Transform outlet, GameObject flame, GameObject smokeColumn, GameObject dust,
            GameObject sparkPrefab, GameObject heat, GameObject steamJet, GameObject billow = null, GameObject deluge = null)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Configure launch VFX outside Play mode.");
            departure = timeline; nozzle = engineNozzle; deflector = deflectorPlane; pad = groundPad; trenchExit = outlet;
            flamePrefab = flame; smokeColumnPrefab = smokeColumn; dustPrefab = dust;
            sparksPrefab = sparkPrefab; heatPrefab = heat; steamJetPrefab = steamJet;
            billowPrefab = billow; delugePrefab = deluge;
        }

        private void LateUpdate()
        {
            if (failed) return;
            if (!departure || !departure.IsPlaying) { ResetViews(); return; }
            if (departure.IsSuspended) return;
            try { Sample(departure.PresentedSeconds, departure.ReducedMotion); }
            catch (Exception exception) { Fail(exception); }
        }

        /// <summary>Evidence/preview hook: presents the supplied playhead instead of the departure's. Call with
        /// increasing seconds (move the nozzle yourself); a smaller value clears and replays. Never used by the game.</summary>
        public void PreviewSample(float seconds, bool reducedEffects) => Sample(seconds, reducedEffects);

        /// <summary>Clears every particle and light (preview teardown).</summary>
        public void PreviewClear() => ResetViews();

        /// <summary>A presentation layer must never take the launch down: on any error it switches itself off.</summary>
        private void Fail(Exception exception)
        {
            failed = true;
            Debug.LogWarning("Launch VFX MAX switched itself off after an error; the launch continues without it. " + exception);
            try { ResetViews(); Release(views); } catch (Exception) { /* nothing left to clean */ }
            views = null; plumeLight = cloudLight = null; layers.Clear();
        }

        private void Sample(float seconds, bool reduced)
        {
            if (!nozzle || !pad || !Finite(seconds) || seconds < DelugeStart || seconds >= TransportDepartureTimeline.OrbitSeconds)
            { ResetViews(); return; }
            if (!views) { BuildViews(); if (!views) return; }
            if (lastSeconds >= 0f && (seconds < lastSeconds || reduced != lastReduced)) ResetViews();
            lastReduced = reduced;

            float from = lastSeconds < 0f ? DelugeStart : lastSeconds;
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
            // The game hides the stationary site for the upper-air shot; pad-bound clouds must not float in the void.
            bool siteVisible = pad.gameObject.activeInHierarchy;
            foreach (var layer in layers)
                if (layer.Ground && layer.Renderer && layer.Renderer.enabled != siteVisible) layer.Renderer.enabled = siteVisible;
            PresentLights(seconds, reduced);
            if (cloudLight && !siteVisible) cloudLight.enabled = false;
            PresentGust(seconds, reduced);
            PresentedSeconds = seconds;
            lastSeconds = seconds;
            int live = 0;
            foreach (var layer in layers) if (layer.System) live += layer.System.particleCount;
            LiveParticleCount = live;
        }

        // ---------- sampled envelopes (pure functions of the playhead) ----------

        private const float DelugeStart = TransportDepartureTimeline.IgnitionSeconds - .45f;

        private static float Thrust(float seconds) =>
            Smooth((seconds - TransportDepartureTimeline.IgnitionSeconds) / .6f) * (1f - Smooth((seconds - PlumeFadeSeconds) / .75f));

        private static float Flash(float seconds)
        {
            float age = seconds - TransportDepartureTimeline.IgnitionSeconds;
            return age < 0f ? 0f : Smooth(age / .04f) * Mathf.Exp(-age * 5f);
        }

        /// <summary>A short pulse after ignition that drives the deluge burst and the dust front.</summary>
        private static float Pulse(float seconds, float start, float length)
        {
            float age = seconds - start;
            return age < 0f ? 0f : Smooth(age / .08f) * (1f - Smooth((age - length * .4f) / (length * .6f)));
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
            Vector3 columnSource = nozzle.position + down * Mathf.Lerp(6f, 18f, climb);

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
                        main.startSize = flame ? Range(2.6f * grow, 5.8f * grow) : Range(.14f, .34f);
                        rate = layer.BaseRate * thrust * (1f + 2.5f * flash);
                        break;
                    }
                    case Kind.Rain:
                        Aim(layer.Emitter, nozzle.position + down * .4f, down);
                        rate = sparks && !reduced ? layer.BaseRate * thrust * (1f + 1.5f * flash) : 0f;
                        break;
                    case Kind.Smoke:
                    {
                        Aim(layer.Emitter, columnSource, down);
                        var main = system.main;
                        main.startSize = Range(9f * smokeScale, 18f * smokeScale);
                        bool climbing = seconds >= TransportDepartureTimeline.LiftoffSeconds - .3f;
                        rate = climbing ? layer.BaseRate * thrust * Mathf.Lerp(.7f, 1f, climb) : 0f;
                        break;
                    }
                    case Kind.Billow:
                        // Dense from ignition, thinning as the jet stops reaching the deflector.
                        rate = layer.BaseRate * billowScale * thrust * Mathf.Lerp(.15f, 1f, contact) * (1f + 1.5f * Pulse(seconds, 9f, 1.4f));
                        break;
                    case Kind.Deluge:
                        // White water-deluge steam flashes up just before and through ignition, then settles.
                        rate = layer.BaseRate * (Pulse(seconds, DelugeStart, 2.2f) + .25f * contact * thrust);
                        break;
                    case Kind.Shock:
                        rate = groundShockwave && !reduced ? layer.BaseRate * dustScale * Pulse(seconds, TransportDepartureTimeline.IgnitionSeconds + .05f, .9f) : 0f;
                        break;
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

        private void PresentLights(float seconds, bool reduced)
        {
            float thrust = Thrust(seconds);
            float flash = reduced ? 0f : Flash(seconds);
            float contact = GroundContact(seconds);
            float flicker = reduced ? 1f
                : .78f + .16f * Mathf.PerlinNoise(seconds * 9.5f, .37f) + .06f * Mathf.Sin(seconds * 41f);
            float dim = reduced ? .45f : 1f;
            if (plumeLight)
            {
                float intensity = flashIntensity * flash + plumeLightIntensity * thrust * flicker * dim;
                plumeLight.transform.position = nozzle.position + nozzle.forward * Mathf.Lerp(4f, 12f, Climb(seconds));
                plumeLight.intensity = intensity;
                plumeLight.range = Mathf.Lerp(80f, 140f, flash);
                plumeLight.enabled = intensity > .01f;
            }
            if (cloudLight)
            {
                // Warm under-light that makes the trench billows read orange underneath while the jet is on the pad.
                float flicker2 = reduced ? 1f : .8f + .2f * Mathf.PerlinNoise(seconds * 6.3f, 1.91f);
                float intensity = (cloudGlowIntensity * thrust * contact * flicker2 + flashIntensity * .35f * flash) * dim;
                cloudLight.intensity = intensity;
                cloudLight.enabled = intensity > .01f;
            }
        }

        private void PresentGust(float seconds, bool reduced)
        {
            if (!publishThrusterGust) return;
            float strength = Thrust(seconds) * Mathf.Lerp(.2f, 1f, GroundContact(seconds)) * (reduced ? .5f : 1f);
            Shader.SetGlobalVector(ThrusterGustId, new Vector4(footprint.x, footprint.y, footprint.z, strength));
            gustPublished = true;
        }

        // ---------- views ----------

        private void BuildViews()
        {
            if (!flamePrefab && !smokeColumnPrefab && !dustPrefab && !billowPrefab) return;
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
            // Embers and sparks raining off the bells.
            AddInstance(sparksPrefab, "SparksEffect", Kind.Rain, nozzle.position, nozzle.forward, ++seed,
                life: Range(1f, 2.4f), cone: 42f, radius: 1.6f, rate: 70f, capacity: 220, gravity: 1f, tint: null,
                speed: Range(3f, 10f), size: Range(.12f, .3f));

            // Thick drifting column: lit soft smoke shed by the climbing nozzle; it stays where it was born and drifts.
            var columnSource = billowPrefab ? billowPrefab : smokeColumnPrefab;
            AddInstance(columnSource, billowPrefab ? "SmokeEffect" : "Smoke", Kind.Smoke, nozzle.position, nozzle.forward, ++seed,
                life: Range(12f, 16f), cone: 40f, radius: 2.6f, rate: 20f, capacity: 300, gravity: -.01f, tint: smokeTint,
                speed: Range(1.5f, 4.5f), drift: new Vector3(1.2f, .25f, .35f), limit: 6f, dampen: .03f);

            // Exhaust billows rolling out of the trench: both ways along the ground and down the trench.
            Vector3[] billowDirections = { side, -side, flow };
            foreach (var d in billowDirections)
            {
                Vector3 aim = (d + Vector3.up * .07f).normalized;
                AddInstance(billowPrefab, "SmokeEffect", Kind.Billow, footprint + d * 4f + Vector3.up * 1.2f, aim, ++seed,
                    life: Range(8f, 15f), cone: 24f, radius: 3.2f, rate: 11f, capacity: 170, gravity: -.025f, tint: billowTint,
                    speed: Range(12f, 22f), size: Range(10f, 22f), limit: 2.2f, dampen: .03f, drift: new Vector3(.4f, .1f, 0f));
            }

            // Deluge: white steam flashing up off the pad at ignition.
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector3 aim = (Vector3.up * .8f + side * (sign * .6f)).normalized;
                AddInstance(delugePrefab, "Steam", Kind.Deluge, footprint + side * (sign * 3f) + Vector3.up * .8f, aim, ++seed,
                    life: Range(4f, 7f), cone: 38f, radius: 3f, rate: 34f, capacity: 110, gravity: -.06f, tint: steamTint,
                    speed: Range(6f, 14f), size: Range(6f, 12f), limit: 3f, dampen: .05f);
            }

            // Dust front racing out across the pad and grass, decelerating hard (irregular arcs, not a crisp ring).
            Vector3[] shockDirections = { side, -side, flow, -flow };
            foreach (var d in shockDirections)
            {
                AddInstance(dustPrefab, "DustStorm", Kind.Shock, footprint + d * 3f + Vector3.up * .4f, (d + Vector3.up * .04f).normalized, ++seed,
                    life: Range(2.5f, 4.5f), cone: 48f, radius: 2.5f, rate: 70f, capacity: 70, gravity: -.01f, tint: dustTint,
                    speed: Range(30f, 48f), size: Range(4f, 8f), limit: 3f, dampen: .05f);
            }

            // Flank sand and rock debris kicked up while the jet reaches the deflector.
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector3 dir = (side * sign + flow * .25f).normalized;
                Vector3 aim = (dir + Vector3.up * .16f).normalized;
                Vector3 at = footprint + dir * 5f + Vector3.up * .6f;
                AddInstance(dustPrefab, "DustStorm", Kind.Dust, at, aim, ++seed,
                    life: Range(3f, 5f), cone: 28f, radius: 2.6f, rate: 18f, capacity: 90, gravity: -.02f, tint: dustTint,
                    speed: Range(9f, 17f), size: Range(5f, 9f), limit: 4f, dampen: .05f);
                AddInstance(dustPrefab, "RockDebris", Kind.Debris, at, (dir + Vector3.up * .5f).normalized, ++seed,
                    life: Range(1.2f, 2f), cone: 30f, radius: 2f, rate: 10f, capacity: 40, gravity: 1f, tint: null,
                    speed: Range(10f, 18f), size: Range(.25f, .7f));
                AddInstance(dustPrefab, "SandParticles", Kind.Sand, at, aim, ++seed,
                    life: Range(.8f, 1.4f), cone: 24f, radius: 2.4f, rate: 110f, capacity: 160, gravity: .4f, tint: dustTint,
                    speed: Range(14f, 22f), size: Range(.1f, .24f));
            }

            // Sparks off the deflector impingement point, thrown up and out.
            AddInstance(sparksPrefab, "SparksEffect", Kind.Sparks, footprint + Vector3.up * .5f, (Vector3.up + flow * .35f).normalized, ++seed,
                life: Range(.3f, .8f), cone: 65f, radius: 1.5f, rate: 150f, capacity: 260, gravity: 1.2f, tint: null,
                speed: Range(12f, 26f), size: Range(.1f, .26f));

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
                    speed: Range(14f, 22f), size: Range(3f, 7f), limit: 5f, dampen: .04f);
            }

            plumeLight = MakeLight("Plume flash and flicker light", 80f);
            cloudLight = MakeLight("Under-cloud glow light", 70f);
            cloudLight.transform.position = footprint + Vector3.up * 5f;
            views.SetActive(false);
        }

        private Light MakeLight(string title, float range)
        {
            var lightObject = new GameObject(title);
            lightObject.transform.SetParent(views.transform, false);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point; light.color = lightColour; light.range = range;
            light.shadows = LightShadows.None; light.renderMode = LightRenderMode.ForcePixel; light.enabled = false;
            return light;
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
            ParticleSystem.MinMaxCurve? speed = null, ParticleSystem.MinMaxCurve? size = null,
            float limit = 0f, float dampen = 0f, Vector3? drift = null)
        {
            if (!prefab) return;
            var instance = Instantiate(prefab, views.transform);
            instance.name = prefab.name + " · " + child + " · " + seed;
            instance.transform.localScale = Vector3.one;
            foreach (var audio in instance.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            foreach (var light in instance.GetComponentsInChildren<Light>(true)) light.enabled = false;

            // The instance root is called "<prefab>(Clone)", so the root system is matched by the prefab's own name.
            ParticleSystem chosen = null;
            var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var system in systems)
            {
                bool root = system.gameObject == instance;
                if (!chosen && (root ? child == prefab.name : system.name == child)) chosen = system;
            }
            foreach (var system in systems)
            {
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var e = system.emission; e.enabled = false;
                if (system != chosen && system.gameObject != instance) system.gameObject.SetActive(false);
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
            // authored for the demo scene would point elsewhere once re-aimed, so they are replaced.
            var shape = chosen.shape;
            shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = cone; shape.radius = radius; shape.radiusThickness = 1f; shape.arc = 360f;
            shape.position = Vector3.zero; shape.rotation = Vector3.zero; shape.scale = Vector3.one;
            shape.alignToDirection = false;
            var velocity = chosen.velocityOverLifetime;
            if (drift.HasValue)
            {
                // World-space wind drift. Unity wants every axis in one curve mode, so the prefab's mode is kept.
                Vector3 w = drift.Value;
                velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
                var mode = velocity.x.mode;
                velocity.x = Match(mode, w.x * .6f, w.x * 1.2f);
                velocity.y = Match(mode, w.y * .5f, w.y * 1.2f);
                velocity.z = Match(mode, -w.z, w.z);
                velocity.orbitalX = Match(velocity.orbitalX.mode, 0f, 0f);
                velocity.orbitalY = Match(velocity.orbitalY.mode, 0f, 0f);
                velocity.orbitalZ = Match(velocity.orbitalZ.mode, 0f, 0f);
                velocity.radial = Match(velocity.radial.mode, 0f, 0f);
                velocity.speedModifier = Match(velocity.speedModifier.mode, 1f, 1f);
            }
            else velocity.enabled = false;
            var limiter = chosen.limitVelocityOverLifetime;
            if (limit > 0f && dampen > 0f)
            {
                // Fast roll-out that decelerates like a turbulent ground jet.
                limiter.enabled = true; limiter.separateAxes = false;
                limiter.limit = limit; limiter.dampen = dampen;
                limiter.space = ParticleSystemSimulationSpace.World;
            }
            else limiter.enabled = false;
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
                bool big = kind == Kind.Smoke || kind == Kind.Billow || kind == Kind.Deluge || kind == Kind.Shock
                    || kind == Kind.Dust || kind == Kind.Steam;
                renderer.maxParticleSize = big ? 2f : 1f;
                // Sparks, embers and grit stay at least a couple of pixels wide from the distant launch cameras.
                if (kind == Kind.Embers || kind == Kind.Rain || kind == Kind.Sparks || kind == Kind.Sand || kind == Kind.Debris)
                    renderer.minParticleSize = .002f;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.sortMode = ParticleSystemSortMode.Distance;
                // Lit pack smoke (URP Particles/Lit) picks up the plume and under-cloud lights per pixel.
                renderer.lightProbeUsage = big ? LightProbeUsage.BlendProbes : LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            Aim(emitter, position, direction);
            bool ground = kind != Kind.Flame && kind != Kind.Embers && kind != Kind.Rain && kind != Kind.Smoke;
            layers.Add(new Layer { Kind = kind, System = chosen, Emitter = emitter, Renderer = renderer, Ground = ground,
                BaseRate = rate, Capacity = capacity });
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
            if (gustPublished) { Shader.SetGlobalVector(ThrusterGustId, Vector4.zero); gustPublished = false; }
            if (lastSeconds < 0f && (!views || !views.activeSelf)) return;
            foreach (var layer in layers)
                if (layer.System) layer.System.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (plumeLight) plumeLight.enabled = false;
            if (cloudLight) cloudLight.enabled = false;
            if (views) views.SetActive(false);
            lastSeconds = -1f; LiveParticleCount = 0; PresentedSeconds = 0f;
        }

        private void OnDisable() => ResetViews();

        private void OnDestroy()
        {
            Release(views);
            views = null; plumeLight = cloudLight = null; layers.Clear();
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

        private static ParticleSystem.MinMaxCurve Match(ParticleSystemCurveMode mode, float min, float max)
        {
            switch (mode)
            {
                case ParticleSystemCurveMode.Constant: return new ParticleSystem.MinMaxCurve((min + max) * .5f);
                case ParticleSystemCurveMode.TwoConstants: return new ParticleSystem.MinMaxCurve(min, max);
                case ParticleSystemCurveMode.Curve:
                    return new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, (min + max) * .5f));
                default:
                    return new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, min), AnimationCurve.Constant(0f, 1f, max));
            }
        }
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        private static void Release(UnityEngine.Object obj)
        {
            if (!obj) return;
            if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj);
        }
    }
}
