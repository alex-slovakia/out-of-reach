using System;
using System.Globalization;
using UnityEngine;

namespace OutOfReach.Presentation.Terminal
{
    public enum TerminalExitReason { User, Escape, Pause, Restart, Disabled, Reconfigured, Invalidated, Fault }

    /// <summary>Immutable actual body geometry, read before the terminal's camera/input acquire hides the body.</summary>
    public readonly struct TerminalReachFrame
    {
        public readonly Vector3 RightShoulder, LeftShoulder, RightElbow, RightWrist, BodyForward;
        public TerminalReachFrame(Vector3 rightShoulder,Vector3 leftShoulder,Vector3 rightElbow,Vector3 rightWrist,Vector3 bodyForward)
        { RightShoulder=rightShoulder; LeftShoulder=leftShoulder; RightElbow=rightElbow; RightWrist=rightWrist;
            BodyForward=Vector3.ProjectOnPlane(bodyForward,Vector3.up).normalized; }
        public Vector3 ShoulderCentre => (RightShoulder+LeftShoulder)*.5f;
        public Vector3 BodyRight => Vector3.Cross(Vector3.up,BodyForward);
        public float UpperLength => Vector3.Distance(RightShoulder,RightElbow);
        public float LowerLength => Vector3.Distance(RightElbow,RightWrist);
        public bool IsValid => Finite(RightShoulder)&&Finite(LeftShoulder)&&Finite(RightElbow)&&Finite(RightWrist)&&Finite(BodyForward)
            && BodyForward.sqrMagnitude>.99f && UpperLength>.15f&&UpperLength<.40f&&LowerLength>.15f&&LowerLength<.40f
            && Vector3.Distance(RightShoulder,LeftShoulder)>.25f&&Vector3.Distance(RightShoulder,LeftShoulder)<.55f;
        static bool Finite(Vector3 v)=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.z);
    }

    /// <summary>Physical display presentation only. Root owns E, cursor, camera and the sole player-input lease.</summary>
    [DisallowMultipleComponent]
    public sealed class PhysicalEarthTerminalPresenter : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform displayFrame;
        [SerializeField] private Vector2 activeSizeMetres = new Vector2(.64f, .40f);
        [SerializeField] private Renderer displayRenderer;
        [SerializeField] private Transform separateGloveInstance;
        [SerializeField] private TextAsset rigJson;
        [SerializeField] private Transform leftGripFrame;
        [SerializeField] private TextMesh missionLabel, observationLabel, ageLabel, statusLabel;
        [SerializeField] private Texture observationTexture;
        [SerializeField, Min(0)] private float hoverDistance = .012f;
        [SerializeField, Range(.03f,.3f)] private float pressSeconds = .11f;
        [Tooltip("R33: static archived Slovakia image. Removes glove reach, touch, drag and the globe projection.")]
        [SerializeField] private bool staticImageMode = true;
        [SerializeField] private Texture staticObservationTexture;
        // NASA GIBS MODIS_Terra_CorrectedReflectance_TrueColor, 2026-11-14. Slovakia lies wholly inside Terra MODIS
        // granule MOD03.A2026318.0815 (08:15:00-08:20:00 UTC, CMR LANCEMODIS:3101040922). Gate and age use the granule
        // END so the image never appears before its pass finished. See Data/slovakia-observation-manifest.json.
        private static readonly DateTime StaticPassEndUtc = new DateTime(2026,11,14,8,20,0,DateTimeKind.Utc);
        // 1280x1012 px plate carree crop, 46.90-50.45 N / 16.30-23.10 E. The panel keeps that pixel aspect on the .64x.40 m screen.
        private static readonly Vector4 StaticPanelUv = new Vector4(.025f,.245f,.515625f,.865f);
        private const string StaticStatus="ARCHIVED SATELLITE IMAGE\nNot live. Earth-orbit view,\nnot taken by AURORA-7.";
        private static readonly DateTime ObservationUtc = new DateTime(2026,11,11,11,1,17,DateTimeKind.Utc);
        private static readonly int ObservationId=Shader.PropertyToID("_Observation");
        private static readonly int RotationId=Shader.PropertyToID("_GlobeRotation");
        private static readonly int PointerId=Shader.PropertyToID("_Pointer");
        private static readonly int RippleId=Shader.PropertyToID("_Ripple");
        private static readonly int AvailableId=Shader.PropertyToID("_ObservationAvailable");
        private static readonly int StaticImageId=Shader.PropertyToID("_StaticImage");
        private static readonly int StaticRectId=Shader.PropertyToID("_StaticRect");
        private Func<bool> tryAcquireSession, mustCloseSession;
        private Action releaseSession, acquiredRelease;
        private Func<DateTime> readMissionUtc;
        private Func<TerminalReachFrame> readReach;
        private bool pointerReachable;
        private readonly TerminalGlovePose glove = new TerminalGlovePose();
        private MaterialPropertyBlock properties, previousProperties;
        private bool active, transitioning, pointerValid, dragging, held, observationAvailable;
        private int configurationGeneration;
        private Vector2 pointerUv = new Vector2(.35f,.50f), previousPointer, rippleUv;
        private Vector3 targetTip, currentTip;
        private Quaternion globeRotation=Quaternion.identity;
        private float contactAge=10, rippleAge=10, displayRefresh;

        public bool IsEntered => active;
        /// <summary>True when the terminal is a look-only static image: no touch, drag, glove or pointer.</summary>
        public bool IsStaticImage => staticImageMode && staticObservationTexture;
        public bool TouchReachable => pointerValid && pointerReachable;
        public TerminalReachFrame ActiveReachFrame { get; private set; }
        public Vector3 RightElbowWorld => glove.SolvedElbow;
        public float RightWristAngleDegrees => glove.WristAngleDegrees;
        public string LastTouchRefusal { get; private set; } = string.Empty;
        /// <summary>Root supplies actual body joints; this view never writes or samples a body pose.</summary>
        public void ConfigureReach(Func<TerminalReachFrame> reader)
        { int generation=++configurationGeneration; Exit(TerminalExitReason.Reconfigured);
            if(generation==configurationGeneration) readReach=reader; }
        public Transform DisplayFrame => displayFrame;
        public Vector2 ActiveSizeMetres => activeSizeMetres;
        public Renderer DisplayRenderer => displayRenderer;
        public Transform LeftGripFrame => leftGripFrame;
        public event Action Entered;
        public event Action<TerminalExitReason> Exited;
        /// <summary>Display UV, bottom-left=(0,0). Fired only for a valid contact during an acquired session.</summary>
        public event Action<Vector2> ContactPressed;

        public void ConfigureScene(Camera camera, Transform frame, Vector2 sizeMetres, Renderer screen,
            Transform gloveInstance, TextAsset gloveRig, Transform gripFrame)
        {
            int generation=++configurationGeneration;
            Exit(TerminalExitReason.Reconfigured);
            if (generation != configurationGeneration) return;
            viewCamera=camera; displayFrame=frame; activeSizeMetres=sizeMetres; displayRenderer=screen;
            separateGloveInstance=gloveInstance; rigJson=gloveRig; leftGripFrame=gripFrame;
        }

        public void ConfigureSession(Func<bool> acquire, Action release, Func<bool> close, Func<DateTime> missionUtc)
        {
            int generation=++configurationGeneration;
            Exit(TerminalExitReason.Reconfigured);
            if (generation != configurationGeneration) return;
            tryAcquireSession=acquire; releaseSession=release; mustCloseSession=close; readMissionUtc=missionUtc;
            if(acquire==null) readReach=null;
            ApplyReadableLabels();
            if(IsStaticImage) RefreshStatic(true);
        }

        /// <summary>Builder/composition hook for the committed static image asset.</summary>
        public void ConfigureStaticImage(Texture image, bool enabled=true)
        {
            int generation=++configurationGeneration;
            Exit(TerminalExitReason.Reconfigured);
            if (generation != configurationGeneration) return;
            staticObservationTexture=image; staticImageMode=enabled;
            ApplyReadableLabels();
            if(IsStaticImage) RefreshStatic(true);
        }

        public void ConfigureDisplay(Texture image, TextMesh mission, TextMesh observed, TextMesh age, TextMesh status)
        {
            int generation=++configurationGeneration;
            Exit(TerminalExitReason.Reconfigured);
            if (generation != configurationGeneration) return;
            observationTexture=image; missionLabel=mission; observationLabel=observed; ageLabel=age; statusLabel=status;
        }

        public bool TryEnter()
        {
            if (active) return true;
            if (IsStaticImage) return TryEnterStatic();
            if (transitioning || !isActiveAndEnabled || !ReferencesValid() || tryAcquireSession == null ||
                releaseSession == null || mustCloseSession == null || readMissionUtc == null) return false;
            int generation=configurationGeneration;
            var release=releaseSession;
            bool acquired=false;
            transitioning=true;
            try
            {
                if (mustCloseSession()) return false;
                // Snapshot before acquire: root legitimately hides/resets the body from that callback.
                TerminalReachFrame actualReach=readReach!=null?readReach():default;
                if(generation!=configurationGeneration) return false;
                if(!actualReach.IsValid) { LastTouchRefusal="Actual body reach is unavailable."; return false; }
                ActiveReachFrame=actualReach;
                acquired=tryAcquireSession();
                if (!acquired) return false;
                if (generation != configurationGeneration || !isActiveAndEnabled || !ReferencesValid() || mustCloseSession())
                    return false;
                if (generation != configurationGeneration) return false;
                if (!glove.Bind(separateGloveInstance, rigJson) || !glove.Begin(actualReach,viewCamera)) return false;
                if(!glove.Place(PointOnScreen(new Vector2(.35f,.5f),hoverDistance),displayFrame.rotation))
                { LastTouchRefusal="Move into a reachable stance before opening the terminal."; return false; }
                if (properties == null) properties=new MaterialPropertyBlock();
                if (previousProperties == null) previousProperties=new MaterialPropertyBlock();
                displayRenderer.GetPropertyBlock(previousProperties);
                displayRenderer.GetPropertyBlock(properties);
                acquiredRelease=release; active=true; acquired=false;
                pointerValid=false; pointerReachable=false; dragging=false; held=false;
                LastTouchRefusal=string.Empty;
                contactAge=rippleAge=10; displayRefresh=0;
                pointerUv=new Vector2(.35f,.5f);
                targetTip=currentTip=PointOnScreen(pointerUv,hoverDistance);
                RefreshTime(); ApplyDisplay(); PlaceHands(0);
            }
            catch (Exception exception) { Debug.LogException(exception,this); Exit(TerminalExitReason.Fault); }
            finally
            {
                transitioning=false;
                if (!active)
                {
                    ActiveReachFrame=default; pointerReachable=false;
                    acquiredRelease=null;
                    try { glove.Restore(); }
                    catch (Exception exception) { Debug.LogException(exception,this); }
                    finally { if (acquired) InvokeSafely(release); }
                }
            }
            if (!active) return false;
            Notify(Entered);
            return active;
        }

        /// <summary>Look-only focus: root's camera lease only. No body reach, glove, cursor contact or drag.</summary>
        private bool TryEnterStatic()
        {
            if (transitioning || !isActiveAndEnabled || !StaticReferencesValid() || tryAcquireSession == null ||
                releaseSession == null || mustCloseSession == null || readMissionUtc == null) return false;
            int generation=configurationGeneration;
            var release=releaseSession;
            bool acquired=false;
            transitioning=true;
            try
            {
                if (mustCloseSession()) return false;
                acquired=tryAcquireSession();
                if (!acquired) return false;
                if (generation != configurationGeneration || !isActiveAndEnabled || !StaticReferencesValid() || mustCloseSession())
                    return false;
                acquiredRelease=release; active=true; acquired=false;
                pointerValid=false; pointerReachable=false; dragging=false; held=false;
                LastTouchRefusal=string.Empty; displayRefresh=0;
                RefreshStatic(true);
            }
            catch (Exception exception) { Debug.LogException(exception,this); Exit(TerminalExitReason.Fault); }
            finally
            {
                transitioning=false;
                if (!active) { acquiredRelease=null; if (acquired) InvokeSafely(release); }
            }
            if (!active) return false;
            Notify(Entered);
            return active;
        }

        public void Exit(TerminalExitReason reason=TerminalExitReason.User)
        {
            if (!active) return;
            active=false; pointerValid=false; pointerReachable=false; dragging=false; held=false; ActiveReachFrame=default;
            var release=acquiredRelease; acquiredRelease=null;
            // Restore before callbacks. Even synchronous re-entry cannot inherit the previous pose or screen state.
            bool wasTransitioning=transitioning;
            transitioning=true;
            try
            {
                if (IsStaticImage) RefreshStatic(true); // The static screen stays lit after focus ends.
                else
                {
                    glove.Restore();
                    if (displayRenderer && previousProperties != null) displayRenderer.SetPropertyBlock(previousProperties);
                }
            }
            catch (Exception exception) { Debug.LogException(exception,this); }
            finally
            {
                InvokeSafely(release); Notify(Exited,reason);
                transitioning=wasTransitioning;
            }
        }

        public void SubmitPointer(Vector2 screenPixels, bool pressed, bool isHeld, bool released)
        {
            if (!active || transitioning || IsStaticImage) return; // R33: static mode has no touch or drag.
            if (!ReferencesValid()) { Exit(TerminalExitReason.Invalidated); return; }
            if (!TryMapPointer(screenPixels,out var uv))
            {
                pointerValid=false; dragging=false; held=false;
                return;
            }
            pointerValid=true; pointerUv=uv;
            Vector3 proposed=PointOnScreen(uv,(pressed||isHeld)?.001f:hoverDistance);
            pointerReachable=glove.Place(proposed,displayFrame.rotation,false);
            if(!pointerReachable)
            {
                held=dragging=false; contactAge=10;
                LastTouchRefusal="Outside the actual arm's touch reach.";
                ApplyDisplay(); return;
            }
            LastTouchRefusal=string.Empty; held=isHeld;
            if (pressed)
            {
                contactAge=0; rippleAge=0; rippleUv=uv;
                previousPointer=uv; dragging=IsOverGlobe(uv);
                // Contact and its ripple share the exact screen point; smoothing applies to hover and release only.
                currentTip=PointOnScreen(uv,.001f);
                if (!glove.Place(currentTip,displayFrame.rotation))
                { Exit(TerminalExitReason.Invalidated); return; }
                Notify(ContactPressed,uv);
                if (!active || transitioning) return;
            }
            if (dragging && isHeld)
            {
                var delta=uv-previousPointer;
                globeRotation=Quaternion.AngleAxis(-delta.x*240,Vector3.up)
                    * Quaternion.AngleAxis(delta.y*150,Vector3.right) * globeRotation;
                globeRotation=globeRotation.normalized;
            }
            previousPointer=uv;
            if (released || !isHeld) dragging=false;
        }

        public bool TryMapPointer(Vector2 screenPixels,out Vector2 uv)
        {
            uv=default;
            if (!active || !ReferencesValid() || !Finite(screenPixels.x) || !Finite(screenPixels.y)) return false;
            var normal=-displayFrame.forward;
            var ray=viewCamera.ScreenPointToRay(screenPixels);
            if (Vector3.Dot(ray.direction,normal) >= -.001f) return false;
            var plane=new Plane(normal,displayFrame.position);
            if (!plane.Raycast(ray,out float distance) || distance < 0 || distance > 4) return false;
            var local=displayFrame.InverseTransformPoint(ray.GetPoint(distance));
            uv=new Vector2(local.x/activeSizeMetres.x+.5f,local.y/activeSizeMetres.y+.5f);
            return uv.x>=0 && uv.x<=1 && uv.y>=0 && uv.y<=1;
        }

        private void OnEnable()
        {
            if (IsStaticImage) RefreshStatic(true);
        }

        private void Update()
        {
            if (IsStaticImage) { UpdateStatic(); return; }
            if (!active) return;
            try
            {
                if (!ReferencesValid()) { Exit(TerminalExitReason.Invalidated); return; }
                if (mustCloseSession == null || mustCloseSession()) { Exit(TerminalExitReason.Invalidated); return; }
                if (!active) return;
                float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
                contactAge+=dt; rippleAge+=dt; displayRefresh-=dt;
                if (displayRefresh<=0) { RefreshTime(); displayRefresh=.25f; }
                PlaceHands(dt); ApplyDisplay();
            }
            catch (Exception exception) { Debug.LogException(exception,this); Exit(TerminalExitReason.Fault); }
        }

        private void UpdateStatic()
        {
            if (active)
            {
                try
                {
                    if (!StaticReferencesValid() || mustCloseSession == null || mustCloseSession())
                    { Exit(TerminalExitReason.Invalidated); return; }
                }
                catch (Exception exception) { Debug.LogException(exception,this); Exit(TerminalExitReason.Fault); return; }
            }
            displayRefresh-=Mathf.Min(Time.unscaledDeltaTime,.05f);
            if (displayRefresh<=0) { RefreshStatic(false); displayRefresh=.25f; }
        }

        /// <summary>The static screen is visible focused or not. It only reads the bound mission calendar.</summary>
        private void RefreshStatic(bool logFault)
        {
            if (!displayRenderer || !staticObservationTexture) return;
            try
            {
                if (readMissionUtc != null) RefreshTime();
                else
                {
                    observationAvailable=false;
                    SetLabel(missionLabel,"MISSION UTC\nAwaiting mission calendar");
                    SetLabel(observationLabel,"OBSERVED UTC\n2026-11-14 08:15-08:20");
                    SetLabel(ageLabel,"IMAGE AGE\nAwaiting mission calendar");
                    SetLabel(statusLabel,StaticStatus);
                }
            }
            catch (Exception exception)
            {
                observationAvailable=false;
                if (logFault) Debug.LogException(exception,this);
            }
            if (properties == null) properties=new MaterialPropertyBlock();
            properties.Clear();
            properties.SetTexture(ObservationId,staticObservationTexture);
            properties.SetFloat(StaticImageId,1);
            properties.SetVector(StaticRectId,StaticPanelUv);
            properties.SetFloat(AvailableId,observationAvailable?1:0);
            properties.SetVector(PointerId,Vector4.zero);
            properties.SetVector(RippleId,new Vector4(0,0,10,0));
            displayRenderer.SetPropertyBlock(properties);
        }

        private void PlaceHands(float dt)
        {
            if(!glove.IsActive) { Exit(TerminalExitReason.Invalidated); return; }
            if(pointerValid && !pointerReachable) return;
            float contact=pointerValid ? (held ? 1 : Mathf.Clamp01(1-contactAge/pressSeconds)) : 0;
            targetTip=PointOnScreen(pointerUv,Mathf.Lerp(hoverDistance,.001f,contact));
            currentTip=dt<=0 ? targetTip : Vector3.Lerp(currentTip,targetTip,1-Mathf.Exp(-35*dt));
            if (!glove.Place(currentTip,displayFrame.rotation))
            { pointerReachable=false; held=dragging=false; LastTouchRefusal="Outside the actual arm's touch reach."; }
        }

        private Vector3 PointOnScreen(Vector2 uv,float frontOffset) => displayFrame.TransformPoint(
            new Vector3((uv.x-.5f)*activeSizeMetres.x,(uv.y-.5f)*activeSizeMetres.y,-frontOffset));

        private void ApplyDisplay()
        {
            if (!active || !displayRenderer) return;
            properties.SetTexture(ObservationId,observationTexture);
            properties.SetVector(RotationId,new Vector4(globeRotation.x,globeRotation.y,globeRotation.z,globeRotation.w));
            properties.SetVector(PointerId,new Vector4(pointerUv.x,pointerUv.y,pointerValid?(pointerReachable?1:.20f):0,held?1:0));
            properties.SetVector(RippleId,new Vector4(rippleUv.x,rippleUv.y,rippleAge,active?1:0));
            properties.SetFloat(AvailableId,observationAvailable?1:0);
            displayRenderer.SetPropertyBlock(properties);
        }

        private void ApplyReadableLabels()
        {
            if(!displayFrame) return;
            foreach(var label in displayFrame.GetComponentsInChildren<TextMesh>(true))
            {
                if(label.transform.parent!=displayFrame) continue;
                float size=label.name=="Title"?.0036f:label.name=="Credit"?.0022f:.0024f;
                label.characterSize=size; label.fontSize=64; label.lineSpacing=1.1f;
                label.color=new Color(.82f,.89f,.92f,1);
                if(IsStaticImage)
                {
                    if(label.name=="Mode")
                    { label.text="SLOVAKIA  /  TERRA MODIS TRUE COLOUR"; label.transform.localPosition=new Vector3(-.296f,-.122f,-.0004f); }
                    if(label.name=="Truth")
                    {
                        label.transform.localPosition=new Vector3(-.296f,-.141f,-.0004f);
                        label.text="46.9-50.45 N, 16.3-23.1 E. Lat/lon grid, unmodified pixels.\nTerra pass 08:15-08:20 UTC (09:15-09:20 CET). Static image.";
                        label.characterSize=.0021f;
                    }
                    if(label.name=="OriginalInset") label.text=string.Empty;
                    if(label.name=="Credit")
                    {
                        label.text="Imagery: NASA GIBS / ESDIS, MODIS Terra (LANCE NRT)\nworldview.earthdata.nasa.gov  /  2026-11-14";
                        label.characterSize=.0021f;
                    }
                    var staticBinder=label.GetComponent<ShipLabelAtlasBinder>();
                    if(staticBinder) staticBinder.RefreshAtlas();
                    continue;
                }
                if(label.name=="Mode")
                { label.text="EARTH IMAGE"; label.transform.localPosition=new Vector3(-.296f,-.130f,-.0004f); }
                if(label.name=="Truth")
                {
                    label.transform.localPosition=new Vector3(-.296f,-.149f,-.0004f);
                    label.text="Visual projection, not a coordinate map.\nReverse side not observed.\nDim pointer: out of reach.";
                    label.characterSize=.0021f;
                }
                var binder=label.GetComponent<ShipLabelAtlasBinder>();
                if(binder) binder.RefreshAtlas();
            }
        }

        private void RefreshTime()
        {
            var utc=readMissionUtc();
            // Unspecified/Local clocks are rejected; presentation must never silently substitute PC local time.
            if (utc.Kind != DateTimeKind.Utc) throw new InvalidOperationException("Terminal mission UTC delegate must return DateTimeKind.Utc.");
            if (IsStaticImage)
            {
                observationAvailable=utc>=StaticPassEndUtc;
                SetLabel(missionLabel,"MISSION UTC\n"+utc.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture));
                SetLabel(observationLabel,"OBSERVED UTC\n2026-11-14 08:15-08:20");
                var sincePass=utc-StaticPassEndUtc;
                SetLabel(ageLabel,observationAvailable ? string.Format(CultureInfo.InvariantCulture,
                    "IMAGE AGE (FROM PASS END)\n{0}d {1:00}h {2:00}m {3:00}s",sincePass.Days,sincePass.Hours,sincePass.Minutes,sincePass.Seconds)
                    : "OBSERVATION UNAVAILABLE\nTerra pass not yet complete");
                SetLabel(statusLabel,StaticStatus);
                return;
            }
            observationAvailable=utc>=ObservationUtc;
            SetLabel(missionLabel,"MISSION UTC\n"+utc.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture));
            SetLabel(observationLabel,"OBSERVED UTC\n2026-11-11 11:01:17");
            var age=utc-ObservationUtc;
            SetLabel(ageLabel,observationAvailable ? string.Format(CultureInfo.InvariantCulture,
                "IMAGE AGE\n{0}d {1:00}h {2:00}m {3:00}s",age.Days,age.Hours,age.Minutes,age.Seconds) : "OBSERVATION UNAVAILABLE\nAt this mission time");
            SetLabel(statusLabel,"ARCHIVED OBSERVATION\nNot a live feed or forecast.\nSpacecraft position unavailable.");
        }

        private static void SetLabel(TextMesh label,string text)
        {
            if (!label || label.text==text) return;
            label.text=text;
            var binder=label.GetComponent<ShipLabelAtlasBinder>();
            if (binder) binder.RefreshAtlas();
        }
        private bool ReferencesValid() => viewCamera && displayFrame && displayRenderer && observationTexture &&
            separateGloveInstance && rigJson && activeSizeMetres.x>.01f && activeSizeMetres.y>.01f &&
            Finite(activeSizeMetres.x) && Finite(activeSizeMetres.y) &&
            (displayFrame.lossyScale-Vector3.one).sqrMagnitude<.00001f;
        private bool StaticReferencesValid() => viewCamera && displayFrame && displayRenderer && staticObservationTexture &&
            activeSizeMetres.x>.01f && activeSizeMetres.y>.01f && Finite(activeSizeMetres.x) && Finite(activeSizeMetres.y);
        private static bool IsOverGlobe(Vector2 uv) => new Vector2((uv.x-.29f)*1.6f,uv.y-.51f).sqrMagnitude<.30f*.30f;
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        private void OnDisable() => Exit(TerminalExitReason.Disabled);
        private void OnDestroy() => Exit(TerminalExitReason.Disabled);
        private void InvokeSafely(Action action)
        {
            try { action?.Invoke(); } catch(Exception exception) { Debug.LogException(exception,this); }
        }
        private void Notify(Action action)
        {
            if (action==null) return;
            foreach(Action handler in action.GetInvocationList()) InvokeSafely(handler);
        }
        private void Notify<T>(Action<T> action,T value)
        {
            if (action==null) return;
            foreach(Action<T> handler in action.GetInvocationList())
                try { handler(value); } catch(Exception exception) { Debug.LogException(exception,this); }
        }
    }
}
