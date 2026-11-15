using System;
using OutOfReach.Presentation.Cinematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace OutOfReach.Presentation.LaunchFXV03
{
    /// <summary>Passive visual sampling of the actual transport playhead. No clock, audio or gameplay authority.</summary>
    [DisallowMultipleComponent]
    public sealed class LaunchFxV03Presenter : MonoBehaviour
    {
        public const int MaximumCloudParticles = 160;
        public const int ReducedCloudParticles = 40;
        private const int BurstParticles = 28;
        [SerializeField] private Material plumeMaterial, cloudMaterial;
        [SerializeField, Range(.2f, 3f)] private float nozzleRadiusMetres = .85f;
        [SerializeField, Range(6f, 40f)] private float maximumPlumeMetres = 30f;
        [SerializeField, Range(0f, 1f)] private float cloudOpacity = .78f;
        [SerializeField, Range(1f, 12f)] private float vehicleDiameterMetres = 4.7f;
        [SerializeField, Range(2f, 20f)] private float trenchWidthMetres = 6f;
        [SerializeField, Range(0f, 200f)] private float lightIntensity = 60f;
        private Transform nozzle, solidPlane, ground, trenchExit;
        private GameObject ownedRoot;
        private Mesh plumeMesh;
        private MeshRenderer envelope, core;
        private ParticleSystem steam;
        private ParticleSystemRenderer steamRenderer;
        private Light practical;
        private MaterialPropertyBlock block;
        private readonly ParticleSystem.Particle[] steamBuffer = new ParticleSystem.Particle[MaximumCloudParticles];
        private Vector3 footprint, flow;
        private bool bound, sampled;
        private static readonly int SampleId = Shader.PropertyToID("_Sample");
        private static readonly int CoreId = Shader.PropertyToID("_Core");
        private static readonly int ContactGlowId = Shader.PropertyToID("_ContactGlow");
        private static readonly int GlowRadiusId = Shader.PropertyToID("_GlowRadius");
        public bool IsBound => bound;
        public float PresentedSeconds { get; private set; }
        public float PresentedPlumeLength { get; private set; }
        public int PresentedSteamCount { get; private set; }
        // Retained for existing diagnostics: the user cancelled every radial burst/shockwave.
        public int PresentedDustCount => 0;

        /// <summary>Called by authoring before save; all references are dedicated assets, never mutated.</summary>
        public void ConfigureMaterials(Material plume, Material cloud)
        {
            if (bound) throw new InvalidOperationException("Set materials before binding launch anchors.");
            plumeMaterial = plume; cloudMaterial = cloud;
        }

        /// <summary>Explicit scene binding. Nozzle +Z points down; stationary +Y planes use metre scale.
        /// Exit is the actual trench outlet and supplies direction, never a new simulated trajectory.</summary>
        public void Configure(Transform engineNozzle, Transform deflectorPlane, Transform dustPlane, Transform exit,
            Material plume = null, Material cloud = null)
        {
            ClearBinding();
            if (plume) plumeMaterial = plume;
            if (cloud) cloudMaterial = cloud;
            if (!engineNozzle || !deflectorPlane || !dustPlane || !exit || !plumeMaterial || !cloudMaterial)
                throw new ArgumentException("Launch FX require the actual nozzle, deflector, ground, trench exit and both materials.");
            if (!ValidScale(engineNozzle) || !ValidScale(deflectorPlane) || !ValidScale(dustPlane)
                || deflectorPlane.IsChildOf(engineNozzle) || dustPlane.IsChildOf(engineNozzle) || exit.IsChildOf(engineNozzle)
                || Vector3.Dot(engineNozzle.forward, deflectorPlane.up) >= -.1f
                || Vector3.Dot(dustPlane.up, Vector3.up) < .999f)
                throw new ArgumentException("Use independent stationary horizontal metre-scale launch anchors and a downward nozzle.");
            if (!plumeMaterial.shader || !cloudMaterial.shader || plumeMaterial.shader.name != "OutOfReach/LaunchV03/Plume" || cloudMaterial.shader.name != "OutOfReach/LaunchV03/Cloud")
                throw new ArgumentException("Launch V03 requires its dedicated plume and cloud shader assets.");
            nozzle=engineNozzle; solidPlane=deflectorPlane; ground=dustPlane; trenchExit=exit;
            float distance=IntersectionDistance();
            if (!Finite(distance) || distance <= 0f) { ClearBinding(); throw new ArgumentException("Nozzle must be above the solid deflector."); }
            footprint=nozzle.position+nozzle.forward*distance;
            footprint.y=ground.position.y+.10f;
            flow=Vector3.ProjectOnPlane(trenchExit.position-footprint,Vector3.up).normalized;
            if (flow.sqrMagnitude < .9f) flow=Vector3.ProjectOnPlane(ground.forward,Vector3.up).normalized;
            BuildOwnedViews(); bound=true; ResetEffects();
        }

        /// <summary>Call AFTER the existing departure presenter moves the vehicle. Paused samples hold
        /// every transform, shader sample and manually populated particle. Repeated/backward samples
        /// deterministically reproduce the supplied frame, without emission accumulators.</summary>
        public void Present(float departureSeconds, TransportDepartureCue cue, bool sequenceActive, bool paused, bool reducedEffects)
        {
            if (!bound || !isActiveAndEnabled || !sequenceActive || !ValidAnchors() || !Finite(departureSeconds)
                || departureSeconds < 0f || departureSeconds >= TransportDepartureTimeline.OrbitSeconds
                || cue == TransportDepartureCue.OrbitalApproach || cue == TransportDepartureCue.Docking
                || cue == TransportDepartureCue.Docked || cue == TransportDepartureCue.BoardingAurora || cue == TransportDepartureCue.LaterAboard)
            { ResetEffects(); return; }
            if (paused) return;
            if (sampled && departureSeconds < PresentedSeconds) ResetEffects();
            sampled=true; PresentedSeconds=departureSeconds;
            float age=departureSeconds-TransportDepartureTimeline.IgnitionSeconds;
            float intensity=Smooth(age/.32f)*(1f-Smooth((departureSeconds-21.1f)/.75f));
            float requested=Mathf.Lerp(7f,maximumPlumeMetres,Smooth((departureSeconds-TransportDepartureTimeline.LiftoffSeconds)/3.8f));
            float clip=IntersectionDistance();
            PresentedPlumeLength=Finite(clip)?Mathf.Max(0f,Mathf.Min(requested,clip-.12f)):0f;
            bool visible=intensity>.001f && PresentedPlumeLength>.03f;
            envelope.enabled=core.enabled=visible;
            if (visible)
            {
                envelope.transform.SetPositionAndRotation(nozzle.position,nozzle.rotation);
                envelope.transform.localScale=new Vector3(nozzleRadiusMetres,nozzleRadiusMetres,PresentedPlumeLength);
                core.transform.SetPositionAndRotation(nozzle.position,nozzle.rotation);
                core.transform.localScale=new Vector3(nozzleRadiusMetres*.34f,nozzleRadiusMetres*.34f,PresentedPlumeLength*.62f);
                SetSample(envelope,departureSeconds,intensity*(reducedEffects?.68f:1f),false,reducedEffects);
                SetSample(core,departureSeconds,intensity*(reducedEffects?.6f:1f),true,reducedEffects);
            }
            float contactLight=intensity*(1f-Smooth((nozzle.position.y-ground.position.y-8f)/28f));
            practical.enabled=visible && !reducedEffects && contactLight>.001f;
            float flicker=.90f+.06f*Mathf.Sin(departureSeconds*37f)+.04f*Mathf.Sin(departureSeconds*23f+1.3f);
            practical.intensity=lightIntensity*contactLight*flicker;
            PresentClouds(age,reducedEffects,reducedEffects?0f:contactLight);
        }

        private void PresentClouds(float age,bool reduced,float contactLight)
        {
            if (!steam.isPaused) { steam.Play(false); steam.Pause(false); }
            int count=0;
            int limit=reduced?ReducedCloudParticles:MaximumCloudParticles;
            Vector3 side=Vector3.Cross(Vector3.up,flow);
            float pad=ground.position.y;
            // Calibrated to the delivered 4.7m booster and 6m open trench; scales if root changes either.
            float scale=Mathf.Max(vehicleDiameterMetres/4.7f,trenchWidthMetres/6f);
            for (int i=0;i<limit;i++)
            {
                // Deterministic schedule: an ignition burst, then steady production that stops once the
                // rising nozzle no longer reaches the deflector. Reduced mode keeps the same spread of births.
                int k=reduced?i*MaximumCloudParticles/limit:i;
                float birth=k<BurstParticles?k*.018f:.5f+(k-BurstParticles)*.052f;
                float a=age-birth;
                if (a<0f) continue;
                float h=Hash(k+1), g=Hash(k+19), w=Hash(k+37);
                float life=7.5f+h*2.5f;
                if (a>=life) continue;
                float produced=Production(birth);
                if (produced<=.02f) continue;
                int group=k%8;
                // Fast roll-out that decelerates like a turbulent ground jet; no constant-speed ring.
                float grow=1f-Mathf.Exp(-a/1.7f);
                Vector3 pos; float width,height;
                if (group<3)
                {
                    // Down the open trench towards the actual outlet, rising out of the slot.
                    float travel=(30f+g*16f)*grow+1.4f*a;
                    float lateral=(g*2f-1f)*(1.2f+.55f*a);
                    pos=footprint+flow*(travel*scale)+side*(lateral*scale);
                    width=Mathf.Min(24f,(5.5f+3.4f*a)*(.85f+w*.35f));
                    height=Mathf.Min(13f,width*(.48f+h*.12f));
                    pos.y=pad+(height*.40f+.55f*a)*scale;
                }
                else if (group<7)
                {
                    // Two broad ground-hugging flanks rolling sideways off the deflector.
                    float sign=group<5?1f:-1f;
                    Vector3 dir=(side*sign+flow*((g-.35f)*.55f)).normalized;
                    float travel=(18f+g*12f)*grow+.9f*a;
                    pos=footprint+dir*(travel*scale);
                    width=Mathf.Min(21f,(4.8f+3.0f*a)*(.85f+w*.35f));
                    height=Mathf.Min(10f,width*(.42f+h*.10f));
                    pos.y=pad+(height*.38f+.30f*a)*scale;
                }
                else
                {
                    // Irregular billows around the engine bay on a wide arc (never a closed ring).
                    float angle=(g*2f-1f)*2.6f;
                    Vector3 dir=(side*Mathf.Sin(angle)+flow*Mathf.Cos(angle)).normalized;
                    pos=footprint+dir*((5f+4f*w)*grow*scale);
                    width=Mathf.Min(16f,(5f+2.2f*a)*(.85f+w*.3f));
                    height=Mathf.Min(14f,width*.85f);
                    pos.y=pad+(height*.45f+1.6f*a)*scale;
                }
                width*=scale; height*=scale;
                float opacity=cloudOpacity*Smooth(a/.30f)*(1f-Smooth((a-life*.55f)/(life*.45f)))
                    *Mathf.Lerp(.35f,1f,produced)*(reduced?.55f:.9f);
                // Cream deluge steam; the flanks pick up more tan pad dust.
                Color color=Color.Lerp(new Color(.66f,.61f,.52f),new Color(.92f,.90f,.84f),h*.6f+(group<3?.32f:.18f));
                color.a=opacity;
                var particle=Particle(pos,width,color,k,a);
                particle.startSize3D=new Vector3(width,height,width);
                particle.rotation=0f;
                steamBuffer[count++]=particle;
            }
            steam.SetParticles(steamBuffer,count); steam.Pause(false); PresentedSteamCount=count;
            block.Clear(); block.SetVector(SampleId,new Vector4(PresentedSeconds,1,reduced?0:1,0));
            Vector3 glowPosition=footprint+Vector3.up*2.2f;
            block.SetVector(ContactGlowId,new Vector4(glowPosition.x,glowPosition.y,glowPosition.z,contactLight));
            block.SetFloat(GlowRadiusId,Mathf.Max(vehicleDiameterMetres*3.2f,trenchWidthMetres*2.4f));
            steamRenderer.SetPropertyBlock(block);
        }

        /// <summary>1 while the jet still reaches the deflector, fading to 0 by about 26 m of climb (actual timeline heights).</summary>
        private static float Production(float birthAfterIgnition)
        {
            float seconds=TransportDepartureTimeline.IgnitionSeconds+birthAfterIgnition;
            if (seconds<=TransportDepartureTimeline.LiftoffSeconds) return 1f;
            float height=TransportDepartureTimeline.Evaluate(seconds,false).HeightMetres;
            return 1f-Smooth((height-4f)/22f);
        }

        private static ParticleSystem.Particle Particle(Vector3 position,float size,Color color,int seed,float age)
        {
            return new ParticleSystem.Particle { position=position, startSize=size, startColor=color,
                rotation=seed*137.508f+age*7f, startLifetime=100f, remainingLifetime=100f,
                randomSeed=(uint)(seed+1), velocity=Vector3.zero };
        }
        private void SetSample(MeshRenderer renderer,float seconds,float opacity,bool hot,bool reduced)
        {
            block.Clear(); block.SetVector(SampleId,new Vector4(seconds,opacity,reduced?0f:1f,0));
            block.SetFloat(CoreId,hot?1f:0f); renderer.SetPropertyBlock(block);
        }
        public void ResetEffects()
        {
            if (envelope) envelope.enabled=false;
            if (core) core.enabled=false;
            if (practical) practical.enabled=false;
            if (steam) steam.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            sampled=false; PresentedSeconds=PresentedPlumeLength=0f; PresentedSteamCount=0;
        }
        public void ClearBinding()
        {
            ResetEffects(); bound=false;
            Release(ownedRoot); Release(plumeMesh);
            ownedRoot=null; plumeMesh=null; envelope=core=null; steam=null; steamRenderer=null; practical=null;
            nozzle=solidPlane=ground=trenchExit=null; block=null;
        }
        private void OnDisable() => ResetEffects();
        private void OnDestroy() => ClearBinding();
        private bool ValidAnchors() => nozzle && solidPlane && ground && trenchExit && ValidScale(nozzle)
            && ValidScale(solidPlane) && ValidScale(ground) && Finite(nozzle.position) && Finite(solidPlane.position)
            && Finite(ground.position) && Finite(trenchExit.position);
        private float IntersectionDistance()
        {
            float dot=Vector3.Dot(nozzle.forward,solidPlane.up);
            return dot<-.1f?Vector3.Dot(solidPlane.position-nozzle.position,solidPlane.up)/dot:0f;
        }
        private static bool ValidScale(Transform t) => (t.lossyScale-Vector3.one).sqrMagnitude<.00001f;
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        private static bool Finite(Vector3 x) => Finite(x.x) && Finite(x.y) && Finite(x.z);
        private static float Smooth(float x) { x=Mathf.Clamp01(x); return x*x*(3f-2f*x); }
        private static float Hash(int n) => Mathf.Repeat(Mathf.Sin(n*127.1f)*43758.5453f,1f);
        private static void Release(UnityEngine.Object obj)
        { if (!obj) return; if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }

        private void BuildOwnedViews()
        {
            ownedRoot=new GameObject("Launch FX V03 — sampled views") { hideFlags=HideFlags.DontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ownedRoot,gameObject.scene);
            // Identity world frame: the owner may be on the moving vehicle; the pad cloud must not be.
            block=new MaterialPropertyBlock(); plumeMesh=LaunchFxV03Mesh.Create();
            envelope=MeshView("Turbulent amber envelope"); core=MeshView("White hot core");
            steam=CloudView("Trench steam",MaximumCloudParticles);
            steamRenderer=steam.GetComponent<ParticleSystemRenderer>();
            var lightObject=new GameObject("Ignition local bounce"); lightObject.transform.SetParent(ownedRoot.transform,false);
            lightObject.transform.position=footprint+Vector3.up*3.0f;
            practical=lightObject.AddComponent<Light>(); practical.type=LightType.Point;
            practical.color=new Color(1f,.55f,.22f); practical.range=30f; practical.shadows=LightShadows.None;
        }
        private MeshRenderer MeshView(string title)
        {
            var go=new GameObject(title); go.transform.SetParent(ownedRoot.transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=plumeMesh;
            var r=go.AddComponent<MeshRenderer>(); r.sharedMaterial=plumeMaterial;
            r.shadowCastingMode=ShadowCastingMode.Off; r.receiveShadows=false;
            r.lightProbeUsage=LightProbeUsage.Off; r.reflectionProbeUsage=ReflectionProbeUsage.Off;
            return r;
        }
        private ParticleSystem CloudView(string title,int capacity)
        {
            var go=new GameObject(title); go.transform.SetParent(ownedRoot.transform,false);
            var system=go.AddComponent<ParticleSystem>(); system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=system.main; main.playOnAwake=false; main.loop=false; main.maxParticles=capacity;
            main.simulationSpace=ParticleSystemSimulationSpace.World; main.startSize3D=true; main.simulationSpeed=0f;
            main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var emission=system.emission; emission.enabled=false;
            var shape=system.shape; shape.enabled=false;
            var r=go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial=cloudMaterial;
            r.renderMode=ParticleSystemRenderMode.Billboard; r.sortMode=ParticleSystemSortMode.Distance;
            r.maxParticleSize=.7f; r.shadowCastingMode=ShadowCastingMode.Off; r.receiveShadows=false;
            r.lightProbeUsage=LightProbeUsage.Off; r.reflectionProbeUsage=ReflectionProbeUsage.Off;
            return system;
        }
    }
}
