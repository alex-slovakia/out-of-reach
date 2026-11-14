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
        [SerializeField] private Transform nozzle, deckPlane;
        [SerializeField] private Material exhaustMaterial, steamMaterial;
        private GameObject exhaustRoot, steamRoot;
        private MeshRenderer exhaust;
        private readonly MeshRenderer[] steam = new MeshRenderer[MarineDepartureFxEnvelope.SteamLobes];
        private Mesh plumeMesh, steamMesh;
        private MaterialPropertyBlock properties;
        private bool bound, hasSample;
        private float heldSeconds;
        private Vector3 steamOriginLocal;
        private static readonly int ParametersId = Shader.PropertyToID("_FxParameters");
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
            // Capture the real jet/plane intersection now; steam stays here when the nozzle later ascends.
            steamOriginLocal = deckPlane.InverseTransformPoint(nozzle.position + nozzle.forward * distance);
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
            exhaust.enabled = length > .02f;
            exhaust.transform.localScale = new Vector3(frame.RadiusMetres, frame.RadiusMetres, Mathf.Max(.001f, length));
            properties.Clear(); properties.SetVector(ParametersId, new Vector4(activeSeconds, frame.Intensity, frame.Motion, 0));
            exhaust.SetPropertyBlock(properties);
            VisibleSteamLobes = 0;
            for (int i = 0; i < steam.Length; i++)
            {
                var f = MarineDepartureFxEnvelope.Steam(activeSeconds, i, reducedEffects);
                steam[i].enabled = f.Visible;
                if (!f.Visible) continue;
                VisibleSteamLobes++;
                float angle = i * Mathf.PI * .5f + .35f, travel = .85f + f.Age * .72f;
                steam[i].transform.localPosition = new Vector3(Mathf.Cos(angle) * travel + f.Age * .22f,
                    .33f + f.Age * .25f, Mathf.Sin(angle) * travel);
                // Closed low-profile volumes remain within6m of the launch centre, above the actual deck.
                steam[i].transform.localScale = new Vector3(.65f + f.Age * .24f, .29f + f.Age * .17f, .55f + f.Age * .22f);
                properties.Clear(); properties.SetVector(ParametersId, new Vector4(activeSeconds, f.Opacity, 1, i * 1.731f));
                steam[i].SetPropertyBlock(properties);
            }
        }

        /// <summary>Exact inactive state: hide all owned geometry, clear property blocks and presentation history.
        /// Retains bounded mesh buffers while bound for a possible restart; does not touch the source assets.</summary>
        public void ResetEffects()
        {
            HideRenderers(); hasSample = false; heldSeconds = 0;
            if (exhaust) exhaust.SetPropertyBlock(null);
            for (int i = 0; i < steam.Length; i++) if (steam[i]) steam[i].SetPropertyBlock(null);
        }

        public void ClearBinding()
        {
            ResetEffects(); bound = false;
            Release(exhaustRoot); Release(steamRoot); Release(plumeMesh); Release(steamMesh);
            exhaustRoot = steamRoot = null; plumeMesh = steamMesh = null; exhaust = null;
            for (int i = 0; i < steam.Length; i++) steam[i] = null;
            nozzle = deckPlane = null; exhaustMaterial = steamMaterial = null; properties = null;
        }

        private void OnDisable() => ClearBinding();
        private void OnDestroy() => ClearBinding();
        private bool ValidViews() => nozzle && deckPlane && exhaustMaterial && steamMaterial && UnitScale(nozzle) && UnitScale(deckPlane)
            && Finite(nozzle.position) && Finite(deckPlane.position);
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
            for (int i = 0; i < steam.Length; i++) if (steam[i]) steam[i].enabled = false;
            PresentedPlumeLength = 0; VisibleSteamLobes = 0;
        }
        private void EnsureGeometry()
        {
            if (exhaust) return;
            properties = new MaterialPropertyBlock();
            plumeMesh = MarineDepartureFxMesh.CreatePlume(); steamMesh = MarineDepartureFxMesh.CreateSteam();
            exhaustRoot = OwnedRoot("Departure exhaust - owned transient", nozzle);
            exhaust = Renderer(exhaustRoot, plumeMesh, exhaustMaterial);
            steamRoot = OwnedRoot("Ignition steam - owned stationary transient", deckPlane);
            steamRoot.transform.localPosition = steamOriginLocal;
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
