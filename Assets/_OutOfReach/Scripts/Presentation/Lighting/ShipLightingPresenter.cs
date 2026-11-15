using System;
using System.Collections.Generic;
using OutOfReach.Contracts;
using UnityEngine;

namespace OutOfReach.Presentation.Lighting
{
    /// <summary>Renders committed power condition on dedicated ship fixtures; never changes mission state.</summary>
    [DisallowMultipleComponent]
    public sealed class ShipLightingPresenter : MonoBehaviour
    {
        [Header("Exclusive fixtures; never share with the cinematic presenter")]
        [SerializeField] private Light[] workingLights = Array.Empty<Light>();
        [SerializeField] private Light[] emergencyLights = Array.Empty<Light>();

        [Header("Steady practical lighting; scales multiply each fixture's authored intensity")]
        [SerializeField] private Color workingColor = new Color(1f, 0.93f, 0.84f, 1f);
        [SerializeField] private Color emergencyColor = new Color(1f, 0.38f, 0.07f, 1f);
        [SerializeField, Range(0f, 1f)] private float damagedWorkingIntensityScale = 0.06f;
        [SerializeField, Range(0f, 1f)] private float nominalEmergencyIntensityScale = 0f;
        [SerializeField, Range(0f, 3f)] private float transitionSeconds = 0.8f;

        [Header("Damaged-power flicker (R28). Slow and shallow; off whenever reduced effects is on")]
        [SerializeField] private IndicatorSlot[] indicatorSlots = Array.Empty<IndicatorSlot>();

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private IMissionEventBus eventBus;
        private IMissionEventBus subscribedBus;
        private IGameState gameState;
        private IRepairable powerSystem;
        private ISimulationClock simulationClock;
        private Action<SystemConditionChanged> conditionHandler;
        private Action<PhaseChanged> phaseHandler;
        private int subscriptionGeneration;
        private bool subscribed;
        private bool transitioning;
        private float damageBlend;
        private float fromDamageBlend;
        private float targetDamageBlend;
        private float transitionElapsedSeconds;
        // Fail safe: until the composition publishes the player's setting, no flicker is shown.
        private bool reducedEffects = true;
        private float flickerSeconds;
        private bool indicatorsApplied;
        private MaterialPropertyBlock indicatorBlock;
        private readonly List<IndicatorSnapshot> indicatorSnapshots = new List<IndicatorSnapshot>();
        private readonly List<LightSnapshot> workingSnapshots = new List<LightSnapshot>();
        private readonly List<LightSnapshot> emergencySnapshots = new List<LightSnapshot>();

        /// <summary>One status-LED material slot whose emission breathes while power is damaged.</summary>
        [Serializable]
        public struct IndicatorSlot
        {
            public Renderer renderer;
            public int materialIndex;
        }

        private struct IndicatorSnapshot
        {
            public Renderer Renderer;
            public int MaterialIndex;
            public Color Emission;
            public float Seed;
        }

        private struct LightSnapshot
        {
            public Light Light;
            public Color Color;
            public float Intensity;
            public bool Enabled;
            public bool UseColorTemperature;
        }

        /// <summary>Bind the preassigned serialized groups to one authoritative mission session.</summary>
        public void Configure(IMissionEventBus events, IGameState state, IRepairable power,
            ISimulationClock clock = null)
        {
            Configure(events, state, power, workingLights, emergencyLights, clock);
        }

        /// <summary>
        /// Bind explicit fixtures and contracts. Authored intensities must be positive, including on
        /// initially disabled emergency lights. A light may belong to only one group. Pass Core's
        /// clock to freeze transitions during pause; the transition duration is always in real seconds.
        /// </summary>
        public void Configure(IMissionEventBus events, IGameState state, IRepairable power,
            Light[] working, Light[] emergency, ISimulationClock clock = null)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (power == null) throw new ArgumentNullException(nameof(power));
            if (power.SystemId != ShipSystemId.Power)
                throw new ArgumentException("Ship lighting requires the authoritative Power repairable.", nameof(power));

            // Validate before touching the old binding; defensive copies prevent outside array mutation.
            var unique = new HashSet<Light>();
            Light[] nextWorking = CopyGroup(working, unique, nameof(working));
            Light[] nextEmergency = CopyGroup(emergency, unique, nameof(emergency));
            StopPresenting();
            eventBus = events;
            gameState = state;
            powerSystem = power;
            simulationClock = clock;
            workingLights = nextWorking;
            emergencyLights = nextEmergency;
            StartPresenting();
        }

        /// <summary>
        /// Publish the player's reduced-effects setting. When true every fixture and LED holds its steady
        /// damaged/emergency value exactly as before R28; when false, damaged power adds restrained flicker.
        /// </summary>
        public void SetReducedEffects(bool reduced)
        {
            if (reducedEffects == reduced) return;
            reducedEffects = reduced;
            if (subscribed) ApplyFrame();
        }

        public bool ReducedEffects => reducedEffects;

        /// <summary>True while a flicker multiplier is actually being applied this frame.</summary>
        public bool IsFlickering => subscribed && !reducedEffects && damageBlend > 0f;

        /// <summary>Replace the LED emission slots; restarts presentation if currently bound.</summary>
        public void ConfigureIndicators(IndicatorSlot[] slots)
        {
            bool restart = subscribed;
            if (restart) StopPresenting();
            indicatorSlots = slots == null ? Array.Empty<IndicatorSlot>() : (IndicatorSlot[])slots.Clone();
            if (restart) StartPresenting();
        }

        /// <summary>Detach from a mission and return every controlled light to its captured values.</summary>
        public void ClearBinding()
        {
            StopPresenting();
            eventBus = null;
            gameState = null;
            powerSystem = null;
            simulationClock = null;
        }

        private void OnEnable() => StartPresenting();
        private void OnDisable() => StopPresenting();
        private void OnDestroy() => ClearBinding();

        private void StartPresenting()
        {
            if (subscribed || !isActiveAndEnabled || eventBus == null || gameState == null || powerSystem == null)
                return;

            Capture(workingLights, workingSnapshots);
            Capture(emergencyLights, emergencySnapshots);
            CaptureIndicators();
            // Correct even when enabled midway through a damaged mission, without waiting for an event.
            ReadPowerCondition(true);

            int generation = ++subscriptionGeneration;
            subscribedBus = eventBus;
            conditionHandler = change =>
            {
                if (subscribed && generation == subscriptionGeneration && change.SystemId == ShipSystemId.Power)
                    ReadPowerCondition(false);
            };
            phaseHandler = change =>
            {
                if (subscribed && generation == subscriptionGeneration)
                    ReadPowerCondition(gameState.Phase == GamePhase.Planning);
            };
            subscribed = true;
            subscribedBus.SystemConditionChanged += conditionHandler;
            subscribedBus.PhaseChanged += phaseHandler;
        }

        private void StopPresenting()
        {
            subscribed = false;
            subscriptionGeneration++;
            if (subscribedBus != null)
            {
                subscribedBus.SystemConditionChanged -= conditionHandler;
                subscribedBus.PhaseChanged -= phaseHandler;
            }
            subscribedBus = null;
            conditionHandler = null;
            phaseHandler = null;
            transitioning = false;
            Restore(workingSnapshots);
            Restore(emergencySnapshots);
            RestoreIndicators();
        }

        private void ReadPowerCondition(bool immediate)
        {
            // The repairable is the source of truth, including when another subscriber caused a
            // newer committed change during dispatch. Never infer success from an interaction/phase.
            double condition = powerSystem.Condition01;
            float next = !double.IsNaN(condition) && !double.IsInfinity(condition) && condition >= 1d ? 0f : 1f;
            if (immediate || SafeDuration() <= 0f)
            {
                damageBlend = targetDamageBlend = next;
                transitioning = false;
                ApplyFrame();
                return;
            }
            if (next == targetDamageBlend) return;
            fromDamageBlend = damageBlend;
            targetDamageBlend = next;
            transitionElapsedSeconds = 0f;
            transitioning = true;
        }

        private void LateUpdate()
        {
            if (!subscribed || (simulationClock != null && simulationClock.IsPaused)) return;
            bool flickering = !reducedEffects && damageBlend > 0f;
            // Healthy or reduced-effects lighting costs nothing per frame once any fade has finished.
            if (!transitioning && !flickering) return;
            float delta = Time.unscaledDeltaTime;
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta <= 0f) return;
            float step = Mathf.Min(delta, 0.1f);
            if (flickering) flickerSeconds += step;
            float progress = 1f;
            if (transitioning)
            {
                transitionElapsedSeconds += step;
                float duration = SafeDuration();
                progress = duration <= 0f ? 1f : Mathf.Clamp01(transitionElapsedSeconds / duration);
                damageBlend = Mathf.Lerp(fromDamageBlend, targetDamageBlend, Mathf.SmoothStep(0f, 1f, progress));
            }
            ApplyFrame();
            if (transitioning && progress >= 1f) transitioning = false;
        }

        private void ApplyFrame()
        {
            float amount = reducedEffects ? 0f : damageBlend;
            Apply(workingSnapshots, workingColor,
                Mathf.Lerp(1f, SafeScale(damagedWorkingIntensityScale), damageBlend), FlickerKind.Working, amount, flickerSeconds);
            Apply(emergencySnapshots, emergencyColor,
                Mathf.Lerp(SafeScale(nominalEmergencyIntensityScale), 1f, damageBlend), FlickerKind.Emergency, amount, flickerSeconds);
            ApplyIndicators(amount);
        }

        private float SafeDuration() => float.IsNaN(transitionSeconds) || float.IsInfinity(transitionSeconds)
            ? 0.8f : Mathf.Clamp(transitionSeconds, 0f, 3f);

        private static float SafeScale(float value) => float.IsNaN(value) || float.IsInfinity(value)
            ? 0f : Mathf.Clamp01(value);

        private static Light[] CopyGroup(Light[] group, HashSet<Light> unique, string parameterName)
        {
            if (group == null) throw new ArgumentNullException(parameterName);
            var result = new List<Light>(group.Length);
            foreach (Light fixture in group)
            {
                if (!fixture) continue;
                if (!unique.Add(fixture))
                    throw new ArgumentException("A light may appear only once across the ship lighting groups.", parameterName);
                result.Add(fixture);
            }
            return result.ToArray();
        }

        private static void Capture(Light[] fixtures, List<LightSnapshot> snapshots)
        {
            snapshots.Clear();
            foreach (Light fixture in fixtures)
            {
                if (!fixture) continue;
                snapshots.Add(new LightSnapshot
                {
                    Light = fixture,
                    Color = fixture.color,
                    Intensity = fixture.intensity,
                    Enabled = fixture.enabled,
                    UseColorTemperature = fixture.useColorTemperature
                });
            }
        }

        private enum FlickerKind { Working, Emergency }

        private static void Apply(List<LightSnapshot> snapshots, Color color, float scale,
            FlickerKind kind, float flickerAmount, float seconds)
        {
            float emergency = kind == FlickerKind.Emergency && flickerAmount > 0f
                ? ShipLightFlickerLogic.Blend(ShipLightFlickerLogic.Emergency(seconds), flickerAmount) : 1f;
            for (int i = 0; i < snapshots.Count; i++)
            {
                LightSnapshot snapshot = snapshots[i];
                if (!snapshot.Light) continue;
                float flicker = emergency;
                if (kind == FlickerKind.Working && flickerAmount > 0f)
                    flicker = ShipLightFlickerLogic.Blend(ShipLightFlickerLogic.Working(seconds, i * 0.37f), flickerAmount);
                snapshot.Light.useColorTemperature = false;
                snapshot.Light.color = color;
                snapshot.Light.intensity = snapshot.Intensity * scale * flicker;
                snapshot.Light.enabled = snapshot.Light.intensity > 0f;
            }
        }

        private void CaptureIndicators()
        {
            indicatorSnapshots.Clear();
            indicatorsApplied = false;
            if (indicatorSlots == null) return;
            for (int i = 0; i < indicatorSlots.Length; i++)
            {
                IndicatorSlot slot = indicatorSlots[i];
                if (!slot.renderer) continue;
                Material[] materials = slot.renderer.sharedMaterials;
                if (slot.materialIndex < 0 || slot.materialIndex >= materials.Length) continue;
                Material material = materials[slot.materialIndex];
                if (!material || !material.HasProperty(EmissionColorId)) continue;
                indicatorSnapshots.Add(new IndicatorSnapshot
                {
                    Renderer = slot.renderer,
                    MaterialIndex = slot.materialIndex,
                    Emission = material.GetColor(EmissionColorId),
                    Seed = i * 0.29f
                });
            }
        }

        private void ApplyIndicators(float amount)
        {
            if (indicatorSnapshots.Count == 0) return;
            if (amount <= 0f)
            {
                if (indicatorsApplied) RestoreIndicators();
                return;
            }
            if (indicatorBlock == null) indicatorBlock = new MaterialPropertyBlock();
            foreach (IndicatorSnapshot snapshot in indicatorSnapshots)
            {
                if (!snapshot.Renderer) continue;
                float gain = ShipLightFlickerLogic.Blend(ShipLightFlickerLogic.Indicator(flickerSeconds, snapshot.Seed), amount);
                snapshot.Renderer.GetPropertyBlock(indicatorBlock, snapshot.MaterialIndex);
                indicatorBlock.SetColor(EmissionColorId, snapshot.Emission * gain);
                snapshot.Renderer.SetPropertyBlock(indicatorBlock, snapshot.MaterialIndex);
            }
            indicatorsApplied = true;
        }

        private void RestoreIndicators()
        {
            foreach (IndicatorSnapshot snapshot in indicatorSnapshots)
                if (snapshot.Renderer) snapshot.Renderer.SetPropertyBlock(null, snapshot.MaterialIndex);
            indicatorsApplied = false;
        }

        private static void Restore(List<LightSnapshot> snapshots)
        {
            foreach (LightSnapshot snapshot in snapshots)
            {
                if (!snapshot.Light) continue;
                snapshot.Light.color = snapshot.Color;
                snapshot.Light.intensity = snapshot.Intensity;
                snapshot.Light.enabled = snapshot.Enabled;
                snapshot.Light.useColorTemperature = snapshot.UseColorTemperature;
            }
            snapshots.Clear();
        }
    }
}
