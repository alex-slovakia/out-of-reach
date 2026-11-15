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
        [SerializeField] private Material plumeMaterial, cloudMaterial;
        [SerializeField, Range(.2f, 3f)] private float nozzleRadiusMetres = .85f;
        [SerializeField, Range(6f, 40f)] private float maximumPlumeMetres = 30f;
        [SerializeField, Range(0f, 1f)] private float cloudOpacity = .78f;
        [SerializeField, Range(1f, 12f)] private float vehicleDiameterMetres = 4.7f;
        [SerializeField, Range(2f, 20f)] private float trenchWidthMetres = 6f;
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
                core.transform.localScale=new Vector3(nozzleRadiusMetres*.40f,nozzleRadiusMetres*.40f,PresentedPlumeLength*.72f);
                SetSample(envelope,departureSeconds,intensity*(reducedEffects?.68f:1f),false,reducedEffects);
                SetSample(core,departureSeconds,intensity*(reducedEffects?.6f:1f),true,reducedEffects);
            }
            float contactLight=intensity*(1f-Smooth((nozzle.position.y-ground.position.y-8f)/28f));
            practical.enabled=visible && !reducedEffects && contactLight>.001f;
            practical.intensity=14f*contactLight;
            PresentClouds(age,reducedEffects,reducedEffects?0f:contactLight);
        }

        private void PresentClouds(float age,bool reduced,float contactLight)
        {
            if (!steam.isPaused) { steam.Play(false); steam.Pause(false); }
            int count=0;
            int limit=reduced?40:MaximumCloudParticles;
            Vector3 side=Vector3.Cross(Vector3.up,flow);
            // Actual delivered transport is 4.7m across; the open trench is 6m wide.
            // Calibrate broad low billows to those dimensions, not to a tiny nozzle puff.
            float baseWidth=Mathf.Max(vehicleDiameterMetres*1.05f,trenchWidthMetres*.8f);
            for (int i=0;i<limit;i++)
            {
                int stream=i%5, bank=i/5;
                float branch=stream==0?0f:stream<3?-1f:1f;
                float birth=bank*.115f+stream*.032f;
                float a=age-birth, life=8.4f+Hash(i+90)*2.2f;
                if (a<0f || a>=life) continue;
                float h=Hash(i+1), s=Hash(i+19)*2f-1f;
                float spread=branch*(trenchWidthMetres*.42f+a*(2.1f+h*.7f))+s*(.35f+a*.22f);
                float travel=1f+(bank%4)*.55f+a*(stream==0?5.8f:4.1f+h*1.3f);
                float width=Mathf.Min(19f,baseWidth*(.9f+h*.32f)+a*(stream==0?1.45f:1.7f));
                float height=Mathf.Min(6.8f,baseWidth*(.38f+h*.12f)+a*.38f);
                Vector3 pos=footprint+flow*travel+side*(spread+Mathf.Sin(a*.7f+i)*a*.13f);
                pos.y+=height*.55f;
                float opacity=cloudOpacity*Smooth(a/.38f)*(1f-Smooth((a-life*.58f)/(life*.42f)))*(reduced?.28f:.82f);
                // Cream deluge steam with modest tan contamination along the outer flanks.
                Color color=Color.Lerp(new Color(.64f,.60f,.51f),new Color(.89f,.87f,.80f),h*.65f+.25f);
                color.a=opacity;
                var particle=Particle(pos,width,color,i,a);
                particle.startSize3D=new Vector3(width,height,width);
                particle.rotation=0f;
                steamBuffer[count++]=particle;
            }
            steam.SetParticles(steamBuffer,count); steam.Pause(false); PresentedSteamCount=count;
            block.Clear(); block.SetVector(SampleId,new Vector4(PresentedSeconds,1,reduced?0:1,0));
            Vector3 glowPosition=footprint+Vector3.up*1.6f;
            block.SetVector(ContactGlowId,new Vector4(glowPosition.x,glowPosition.y,glowPosition.z,contactLight));
            block.SetFloat(GlowRadiusId,Mathf.Max(vehicleDiameterMetres*2.5f,trenchWidthMetres*1.8f));
            steamRenderer.SetPropertyBlock(block);
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
            lightObject.transform.position=footprint+Vector3.up*1.4f;
            practical=lightObject.AddComponent<Light>(); practical.type=LightType.Point;
            practical.color=new Color(1f,.52f,.19f); practical.range=12f; practical.shadows=LightShadows.None;
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
            r.maxParticleSize=.45f; r.shadowCastingMode=ShadowCastingMode.Off; r.receiveShadows=false;
            r.lightProbeUsage=LightProbeUsage.Off; r.reflectionProbeUsage=ReflectionProbeUsage.Off;
            return system;
        }
    }
}
