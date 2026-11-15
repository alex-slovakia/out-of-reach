using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OutOfReach.Presentation.MarineOcean.FX
{
    /// <summary>Optional visual consumer. Root supplies the real departure playhead after vehicle motion.
    /// No Update, independent timer, physics, camera, audio, global material or lighting writes.</summary>
    [DisallowMultipleComponent]
    public sealed class MarineDepartureFxPresenter : MonoBehaviour
    {
        [SerializeField] private Transform nozzle, deckPlane, visiblePad;
        [SerializeField] private Material exhaustMaterial, steamMaterial;
        private GameObject exhaustRoot, steamRoot;
        private MeshRenderer exhaust, exhaustCore;
        private Light ignitionLight;
        private readonly MeshRenderer[] steam = new MeshRenderer[MarineDepartureFxEnvelope.SteamLobes];
        private Mesh plumeMesh, steamMesh;
        private MaterialPropertyBlock properties;
        private bool bound, hasSample;
        private float heldSeconds;
        private Vector3 steamOriginLocal;
        private Transform dustPlane;
        private static readonly int ParametersId = Shader.PropertyToID("_FxParameters");
        private static readonly int HotColorId = Shader.PropertyToID("_HotColor");
        private static readonly int TailColorId = Shader.PropertyToID("_TailColor");
        private static readonly int DustColorId = Shader.PropertyToID("_SteamColor");
        public bool IsBound => bound;
        public bool IsVisible => exhaust && exhaust.enabled;
        public float PresentedSeconds => hasSample ? heldSeconds : 0f;
        public float PresentedPlumeLength { get; private set; }
        public int VisibleSteamLobes { get; private set; }

        /// <summary>Resolve the actual neutral-axis EngineEmitter frame returned by I06MarineAuthoring.
        /// Imported raw FBX empty rotation is not a reliable nozzle direction.</summary>
        public static Transform CalculateAnchor(IReadOnlyDictionary<string, Transform> transportAnchors)
        {
            if (transportAnchors == null || !transportAnchors.TryGetValue("EngineEmitter", out Transform anchor) || !anchor)
                throw new ArgumentException("Actual transportAnchors[EngineEmitter] is required.", nameof(transportAnchors));
            return anchor;
        }

        /// <summary>The solid deflector clips the engine flame. The visible deck carries the dust as it spills out.</summary>
        public void ConfigureDustPlane(Transform padTop)
        {
            if (bound) throw new InvalidOperationException("Configure the visible dust plane before binding FX.");
            if (padTop && (!UnitScale(padTop) || Vector3.Dot(padTop.up, Vector3.up) < .999f))
                throw new ArgumentException("The visible launch deck must be horizontal and metre-scale.", nameof(padTop));
            visiblePad = padTop;
        }

        /// <summary>Explicit binding. No effect is shown until Present receives a running departure sample.
        /// Deck frame origin is on the stationary solid launch deck, +Y its normal. Materials must be dedicated FX assets.</summary>
        public void ConfigureViews(Transform actualNozzle, Transform stationaryDeckPlane, Material exhaustAsset, Material steamAsset)
        {
            ClearBinding();
            if (!actualNozzle || !stationaryDeckPlane || !exhaustAsset || !steamAsset)
                throw new ArgumentException("Nozzle, stationary deck plane and both original FX materials are required.");
            if (actualNozzle == stationaryDeckPlane || stationaryDeckPlane.IsChildOf(actualNozzle))
                throw new ArgumentException("Steam deck must remain stationary when the vehicle ascends.");
            if (!UnitScale(actualNozzle) || !UnitScale(stationaryDeckPlane) || !Finite(actualNozzle.position) || !Finite(stationaryDeckPlane.position))
                throw new ArgumentException("FX anchors require metre scale; preserve the original vehicle scale.");
            float axisDotPlane = Vector3.Dot(actualNozzle.forward, stationaryDeckPlane.up);
            if (axisDotPlane >= -.1f)
                throw new ArgumentException("EngineEmitter forward must point toward the actual deck plane.");
            if (!exhaustAsset.shader || !steamAsset.shader || exhaustAsset.shader.name != "OutOfReach/Marine/DepartureVolume" || steamAsset.shader.name != exhaustAsset.shader.name
                || exhaustAsset.GetFloat("_Mode") != 0 || steamAsset.GetFloat("_Mode") != 1)
                throw new ArgumentException("Use the original Exhaust and IgnitionSteam materials with their correct modes.");
            nozzle = actualNozzle; deckPlane = stationaryDeckPlane; exhaustMaterial = exhaustAsset; steamMaterial = steamAsset;
            float distance = Vector3.Dot(deckPlane.position - nozzle.position, deckPlane.up) / axisDotPlane;
            if (distance <= 0 || !MarineDepartureFxEnvelope.Finite(distance))
            { ClearBinding(); throw new ArgumentException("The actual nozzle must be above the solid launch deck."); }
            dustPlane = visiblePad ? visiblePad : deckPlane;
            if (dustPlane == nozzle || dustPlane.IsChildOf(nozzle) || !UnitScale(dustPlane) || !Finite(dustPlane.position))
            { ClearBinding(); throw new ArgumentException("The visible dust plane must be a stationary scene anchor."); }
            // Anchor to the jet footprint at rest, then project the dust above the visible deck.
            steamOriginLocal = dustPlane.InverseTransformPoint(nozzle.position + nozzle.forward * distance);
            steamOriginLocal.y = 0;
            bound = true;
        }

        /// <summary>Call from root after its actual departure presenter, using Progress01*DurationSeconds.
        /// Paused samples cannot advance shader time or the finite steam sequence. Backward time hard-resets.
        /// No inferred liftoff/launch: sequenceActive is the root's confirmed presentation lease.</summary>
        public void Present(float activeSeconds, bool sequenceActive, bool paused, bool reducedEffects)
        {
            if (!bound || !isActiveAndEnabled || !sequenceActive || !ValidViews()
                || !MarineDepartureFxEnvelope.Finite(activeSeconds) || activeSeconds < 0f || activeSeconds > MarineDepartureFxEnvelope.EndSeconds)
            { ResetEffects(); return; }
            if (paused)
            {
                if (!hasSample) return;
                activeSeconds = heldSeconds;
            }
            else if (hasSample && activeSeconds + .0001f < heldSeconds)
            {
                // Session/replay reset: hide now, root must supply the new sample on its next frame.
                ResetEffects(); return;
            }
            heldSeconds = activeSeconds; hasSample = true;
            var frame = MarineDepartureFxEnvelope.Evaluate(activeSeconds, reducedEffects);
            if (!frame.Visible) { HideRenderers(); return; }
            EnsureGeometry();
            float length = ClipToDeck(frame.LengthMetres);
            PresentedPlumeLength = length;
            PresentFlame(exhaust, frame, length, frame.RadiusMetres, new Color(1.85f, .72f, .19f), new Color(1.31f, .32f, .06f));
            PresentFlame(exhaustCore, frame, length * .60f, frame.RadiusMetres * .44f,
                new Color(2.80f, 1.65f, .71f), new Color(1.62f, .61f, .18f));

            // A bounded local pool of flame light sells ignition before the engine clears the pad.
            float shine = frame.Intensity * (1f - Mathf.SmoothStep(0f, 1f, (activeSeconds - 10.65f) / 2.1f));
            ignitionLight.enabled = exhaust.enabled && !reducedEffects && shine > .01f;
            ignitionLight.intensity = 26f * shine;
            VisibleSteamLobes = 0;
            for (int i = 0; i < steam.Length; i++)
            {
                var f = MarineDepartureFxEnvelope.Steam(activeSeconds, i, reducedEffects);
                steam[i].enabled = f.Visible;
                if (!f.Visible) continue;
                VisibleSteamLobes++;
                int wave = i / 8;
                float angle = (i % 8) * Mathf.PI * .25f + wave * .38f + .25f;
                float travel = 1.2f + f.Age * (3.8f + wave * .45f);
                // Slower on the upwind side, with soft catch-up and clear space at the nozzle.
                steam[i].transform.localPosition = new Vector3(Mathf.Cos(angle) * travel + f.Age * .42f,
                    1.15f + f.Age * (.72f + wave * .11f), Mathf.Sin(angle) * travel);
                steam[i].transform.localScale = new Vector3(3.3f + f.Age * 1.20f, 1.3f + f.Age * .52f, 3.3f + f.Age * 1.15f);
                properties.Clear();
                properties.SetVector(ParametersId, new Vector4(activeSeconds, f.Opacity, 1f, i * 1.731f));
                properties.SetColor(DustColorId, wave == 0 ? new Color(.67f, .59f, .49f) : new Color(.88f, .85f, .78f));
                steam[i].SetPropertyBlock(properties);
            }
        }

        private void PresentFlame(MeshRenderer volume, MarineDepartureFxEnvelope.Frame frame, float length,
            float radius, Color hot, Color tail)
        {
            volume.enabled = length > .02f;
            volume.transform.localScale = new Vector3(radius, radius, Mathf.Max(.001f, length));
            properties.Clear();
            properties.SetVector(ParametersId, new Vector4(frame.Seconds, frame.Intensity, frame.Motion, 0f));
            properties.SetColor(HotColorId, hot);
            properties.SetColor(TailColorId, tail);
            volume.SetPropertyBlock(properties);
        }

        /// <summary>Exact inactive state: hide all owned geometry, clear property blocks and presentation history.
        /// Retains bounded mesh buffers while bound for a possible restart; does not touch the source assets.</summary>
        public void ResetEffects()
        {
            HideRenderers(); hasSample = false; heldSeconds = 0;
            if (exhaust) exhaust.SetPropertyBlock(null);
            if (exhaustCore) exhaustCore.SetPropertyBlock(null);
            for (int i = 0; i < steam.Length; i++) if (steam[i]) steam[i].SetPropertyBlock(null);
        }

        public void ClearBinding()
        {
            ResetEffects(); bound = false;
            Release(exhaustRoot); Release(steamRoot); Release(plumeMesh); Release(steamMesh);
            exhaustRoot = steamRoot = null; plumeMesh = steamMesh = null; exhaust = exhaustCore = null;
            ignitionLight = null;
            dustPlane = null;
            for (int i = 0; i < steam.Length; i++) steam[i] = null;
            nozzle = deckPlane = null; exhaustMaterial = steamMaterial = null; properties = null;
        }

        private void OnDisable() => ClearBinding();
        private void OnDestroy() => ClearBinding();
        private bool ValidViews() => nozzle && deckPlane && dustPlane && exhaustMaterial && steamMaterial &&
            UnitScale(nozzle) && UnitScale(deckPlane) && UnitScale(dustPlane) &&
            Finite(nozzle.position) && Finite(deckPlane.position) && Finite(dustPlane.position);
        private static bool UnitScale(Transform t) => (t.lossyScale - Vector3.one).sqrMagnitude < .000001f;
        private static bool Finite(Vector3 value) => MarineDepartureFxEnvelope.Finite(value.x) && MarineDepartureFxEnvelope.Finite(value.y)
            && MarineDepartureFxEnvelope.Finite(value.z);
        private float ClipToDeck(float length)
        {
            Vector3 normal = deckPlane.up;
            float direction = Vector3.Dot(nozzle.forward, normal);
            return MarineDepartureFxEnvelope.ClipPlumeToDeck(length,
                Vector3.Dot(deckPlane.position - nozzle.position, normal), direction);
        }
        private void HideRenderers()
        {
            if (exhaust) exhaust.enabled = false;
            if (exhaustCore) exhaustCore.enabled = false;
            if (ignitionLight) ignitionLight.enabled = false;
            for (int i = 0; i < steam.Length; i++) if (steam[i]) steam[i].enabled = false;
            PresentedPlumeLength = 0; VisibleSteamLobes = 0;
        }
        private void EnsureGeometry()
        {
            if (exhaust) return;
            properties = new MaterialPropertyBlock();
            plumeMesh = MarineDepartureFxMesh.CreatePlume(); steamMesh = MarineDepartureFxMesh.CreateSteam();
            exhaustRoot = OwnedRoot("Departure exhaust - owned transient", nozzle);
            exhaust = Renderer(OwnedRoot("Expansion flame", exhaustRoot.transform), plumeMesh, exhaustMaterial);
            exhaustCore = Renderer(OwnedRoot("Hot exhaust core", exhaustRoot.transform), plumeMesh, exhaustMaterial);
            steamRoot = OwnedRoot("Ignition dust - owned stationary transient", dustPlane);
            steamRoot.transform.localPosition = steamOriginLocal;
            var lightMount = OwnedRoot("Pad ignition practical", steamRoot.transform);
            lightMount.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            ignitionLight = lightMount.AddComponent<Light>();
            ignitionLight.type = LightType.Point;
            ignitionLight.color = new Color(1f, .51f, .23f);
            ignitionLight.range = 11f;
            ignitionLight.shadows = LightShadows.None;
            ignitionLight.enabled = false;
            for (int i = 0; i < steam.Length; i++)
                steam[i] = Renderer(OwnedRoot("Ignition lobe " + i, steamRoot.transform), steamMesh, steamMaterial);
        }
        private static GameObject OwnedRoot(string name, Transform parent)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false); return go;
        }
        private static MeshRenderer Renderer(GameObject go, Mesh mesh, Material material)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion; renderer.enabled = false;
            return renderer;
        }
        private static void Release(UnityEngine.Object value)
        { if (!value) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    }
}
