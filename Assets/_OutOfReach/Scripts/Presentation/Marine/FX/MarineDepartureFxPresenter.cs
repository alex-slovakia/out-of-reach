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
        private Mesh plumeMesh, steamMesh, pressureMesh;
        private MeshRenderer pressureFront;
        private readonly MeshRenderer[] delugeJets = new MeshRenderer[6];
        private readonly MeshRenderer[] delugeNozzles = new MeshRenderer[6];
        private readonly MeshRenderer[] delugeManifolds = new MeshRenderer[2];
        private Material delugeHardwareMaterial;
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
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
            PresentFlame(exhaust, frame, length, frame.RadiusMetres, new Color(1.65f, .79f, .16f), new Color(.92f, .18f, .018f));
            PresentFlame(exhaustCore, frame, length * .58f, frame.RadiusMetres * .22f,
                new Color(2.30f, 1.95f, 1.40f), new Color(1.24f, .52f, .065f));

            // A bounded local pool of flame light sells ignition before the engine clears the pad.
            float shine = frame.Intensity * (1f - Mathf.SmoothStep(0f, 1f, (activeSeconds - 10.65f) / 2.1f));
            ignitionLight.enabled = exhaust.enabled && !reducedEffects && shine > .01f;
            ignitionLight.intensity = 26f * shine;
            PresentDelugeAndPressure(activeSeconds, reducedEffects);
            VisibleSteamLobes = 0;
            for (int i = 0; i < steam.Length; i++)
            {
                var f = MarineDepartureFxEnvelope.Steam(activeSeconds, i, reducedEffects);
                steam[i].enabled = f.Visible;
                if (!f.Visible) continue;
                VisibleSteamLobes++;
                int wave = i / 8;
                int lane = i % 8;
                float angle = lane * Mathf.PI * .25f + .25f;
                if (wave == 0)
                {
                    // Grounded tan dust stays separate from the wet trench cloud.
                    float travel = 3f + f.Age * 5.2f;
                    float height = .55f + f.Age * .17f;
                    steam[i].transform.localPosition = new Vector3(Mathf.Cos(angle) * travel,
                        height * 1.48f + .08f, Mathf.Sin(angle) * travel);
                    steam[i].transform.localScale = new Vector3(4.8f + f.Age * 1.6f,
                        height, 4.5f + f.Age * 1.4f);
                }
                else
                {
                    // Four finite staggered banks: dense low channel, rolling flanks and rising crowns.
                    // Deterministic irregular spacing prevents all lobes becoming one smooth ellipsoid.
                    float age = f.Age;
                    float jitter = Mathf.Sin(i * 2.39996f);
                    float travel = 1.5f + lane * 2.3f + age * (4.3f + .38f * lane) + (wave == 4 ? 3f : 0f);
                    float spread = (2.2f + age * .85f) * (wave == 1 ? .55f : wave == 4 ? .30f : 1.65f);
                    float side = wave == 2 ? -1f : wave == 3 ? 1f : (lane % 2 == 0 ? -1f : 1f);
                    float height = (wave == 4 ? 2.8f : 1.5f) + age * (wave == 4 ? .88f : .60f);
                    float width = (2.5f + age * .85f) * (1f + .16f * jitter);
                    steam[i].transform.localPosition = new Vector3(side * spread + jitter,
                        height * 1.48f + .12f + (wave == 4 ? age * .55f : 0f), travel);
                    steam[i].transform.localScale = new Vector3(width, height,
                        (3f + age * 1.10f) * (1f - .12f * jitter));
                    steam[i].transform.localRotation = Quaternion.Euler(0, i * 47f, 0);
                }
                properties.Clear();
                properties.SetVector(ParametersId, new Vector4(activeSeconds, f.Opacity, reducedEffects ? 0f : 1f, i * 1.731f));
                properties.SetColor(DustColorId, wave == 0 ? new Color(.52f, .36f, .19f) : new Color(.87f, .89f, .90f));
                steam[i].SetPropertyBlock(properties);
            }
        }

        private void PresentDelugeAndPressure(float seconds,bool reducedEffects)
        {
            float wash=MarineDepartureFxEnvelope.DelugeOpacity(seconds,reducedEffects);
            for(int i=0;i<delugeJets.Length;i++)
            {
                delugeNozzles[i].enabled=true;
                delugeJets[i].enabled=wash>.001f;
                if(!delugeJets[i].enabled)continue;
                properties.Clear();properties.SetFloat(ModeId,2);
                properties.SetVector(ParametersId,new Vector4(seconds,wash,reducedEffects?0:1,i*1.71f));
                properties.SetColor(DustColorId,new Color(.91f,.96f,1f));delugeJets[i].SetPropertyBlock(properties);
            }
            foreach(var pipe in delugeManifolds)pipe.enabled=true;
            float strength=MarineDepartureFxEnvelope.PressureOpacity(seconds,reducedEffects);
            pressureFront.enabled=strength>.001f;
            if(pressureFront.enabled)
            {
                float age=Mathf.Clamp(seconds-MarineDepartureFxEnvelope.LiftoffSeconds,0,3.4f);
                float radius=3f+age*14f;
                pressureFront.transform.localPosition=new Vector3(0,.72f,0);
                pressureFront.transform.localScale=new Vector3(radius,.52f,radius);
                properties.Clear();properties.SetFloat(ModeId,3);
                properties.SetVector(ParametersId,new Vector4(seconds,strength,1,29));
                properties.SetColor(DustColorId,new Color(.61f,.43f,.24f));pressureFront.SetPropertyBlock(properties);
            }
        }

        private void PresentFlame(MeshRenderer volume, MarineDepartureFxEnvelope.Frame frame, float length,
            float radius, Color hot, Color tail)
        {
            volume.enabled = length > .02f;
            volume.transform.localScale = new Vector3(radius, radius, Mathf.Max(.001f, length));
            properties.Clear();
            properties.SetVector(ParametersId, new Vector4(frame.Seconds, frame.Intensity, frame.Motion, volume == exhaustCore ? 13.7f : 1.2f));
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
            if (pressureFront) pressureFront.SetPropertyBlock(null);
            foreach(var jet in delugeJets) if(jet)jet.SetPropertyBlock(null);
            for (int i = 0; i < steam.Length; i++) if (steam[i]) steam[i].SetPropertyBlock(null);
        }

        public void ClearBinding()
        {
            ResetEffects(); bound = false;
            Release(exhaustRoot); Release(steamRoot); Release(plumeMesh); Release(steamMesh); Release(pressureMesh); Release(delugeHardwareMaterial);
            pressureMesh=null;pressureFront=null;delugeHardwareMaterial=null;
            for(int i=0;i<delugeJets.Length;i++){delugeJets[i]=null;delugeNozzles[i]=null;}
            for(int i=0;i<delugeManifolds.Length;i++)delugeManifolds[i]=null;
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
            if(pressureFront)pressureFront.enabled=false;
            foreach(var jet in delugeJets)if(jet)jet.enabled=false;
            foreach(var nozzleHead in delugeNozzles)if(nozzleHead)nozzleHead.enabled=false;
            foreach(var pipe in delugeManifolds)if(pipe)pipe.enabled=false;
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
            pressureMesh=MarineDepartureFxMesh.CreatePressureFront();
            pressureFront=Renderer(OwnedRoot("Pad pressure dust front",steamRoot.transform),pressureMesh,steamMaterial);
            Shader lit=Shader.Find("Universal Render Pipeline/Lit");
            if(!lit)throw new InvalidOperationException("Existing URP Lit shader required for original deluge hardware.");
            delugeHardwareMaterial=new Material(lit){name="Original deluge dark steel - transient",hideFlags=HideFlags.DontSave};
            delugeHardwareMaterial.SetColor("_BaseColor",new Color(.12f,.15f,.17f));
            delugeHardwareMaterial.SetFloat("_Metallic",.7f);delugeHardwareMaterial.SetFloat("_Smoothness",.48f);
            for(int side=0;side<2;side++)
            {
                // Fixed authored fictional slot manifolds, just beyond existing x +/-3 m trench lips.
                float sign=side==0?-1:1;
                delugeManifolds[side]=Renderer(OwnedRoot("Deluge stationary manifold "+side,steamRoot.transform),plumeMesh,delugeHardwareMaterial);
                // Downstream z>5.2 clears the existing mount pedestals (end z4.6).
                delugeManifolds[side].transform.localPosition=new Vector3(sign*3.35f,.16f,5.2f);
                delugeManifolds[side].transform.localScale=new Vector3(.13f,.13f,4.5f);
                for(int j=0;j<3;j++)
                {
                    int index=side*3+j;
                    Vector3 riserBase=new Vector3(sign*3.35f,.16f,5.7f+j*1.4f);
                    Vector3 origin=riserBase+Vector3.up*.89f; // 1.05m above apron, clears actual x=+/-3 lip.
                    Vector3 destination=new Vector3(sign*.3f,-2.5f,5.8f+j*.4f),delta=destination-origin;
                    var rotation=Quaternion.LookRotation(delta.normalized,Vector3.up);
                    delugeNozzles[index]=Renderer(OwnedRoot("Deluge fixed nozzle "+index,steamRoot.transform),plumeMesh,delugeHardwareMaterial);
                    delugeNozzles[index].transform.SetLocalPositionAndRotation(riserBase,Quaternion.LookRotation(Vector3.up,Vector3.forward));
                    delugeNozzles[index].transform.localScale=new Vector3(.16f,.16f,.89f);
                    delugeJets[index]=Renderer(OwnedRoot("Deluge water jet "+index,steamRoot.transform),plumeMesh,steamMaterial);
                    delugeJets[index].transform.SetLocalPositionAndRotation(origin+delta.normalized*.08f,rotation);
                    delugeJets[index].transform.localScale=new Vector3(.30f,.30f,delta.magnitude-.08f);
                }
            }
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
