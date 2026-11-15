using System;
using System.Globalization;
using UnityEngine;

namespace OutOfReach.Presentation.Terminal
{
    public enum TerminalExitReason { User, Escape, Pause, Restart, Disabled, Reconfigured, Invalidated, Fault }

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
        private static readonly DateTime ObservationUtc = new DateTime(2026,11,11,11,1,17,DateTimeKind.Utc);
        private static readonly int ObservationId=Shader.PropertyToID("_Observation");
        private static readonly int RotationId=Shader.PropertyToID("_GlobeRotation");
        private static readonly int PointerId=Shader.PropertyToID("_Pointer");
        private static readonly int RippleId=Shader.PropertyToID("_Ripple");
        private static readonly int AvailableId=Shader.PropertyToID("_ObservationAvailable");
        private Func<bool> tryAcquireSession, mustCloseSession;
        private Action releaseSession, acquiredRelease;
        private Func<DateTime> readMissionUtc;
        private readonly TerminalGlovePose glove = new TerminalGlovePose();
        private MaterialPropertyBlock properties, previousProperties;
        private bool active, transitioning, pointerValid, dragging, held, observationAvailable;
        private int configurationGeneration;
        private Vector2 pointerUv = new Vector2(.35f,.50f), previousPointer, rippleUv;
        private Vector3 targetTip, currentTip;
        private Quaternion globeRotation=Quaternion.identity;
        private float contactAge=10, rippleAge=10, displayRefresh;

        public bool IsEntered => active;
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
            if (transitioning || !isActiveAndEnabled || !ReferencesValid() || tryAcquireSession == null ||
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
                if (generation != configurationGeneration || !isActiveAndEnabled || !ReferencesValid() || mustCloseSession())
                    return false;
                if (generation != configurationGeneration) return false;
                if (!glove.Bind(separateGloveInstance, rigJson) || !glove.Begin()) return false;
                if (properties == null) properties=new MaterialPropertyBlock();
                if (previousProperties == null) previousProperties=new MaterialPropertyBlock();
                displayRenderer.GetPropertyBlock(previousProperties);
                displayRenderer.GetPropertyBlock(properties);
                acquiredRelease=release; active=true; acquired=false;
                pointerValid=false; dragging=false; held=false;
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

        public void Exit(TerminalExitReason reason=TerminalExitReason.User)
        {
            if (!active) return;
            active=false; pointerValid=false; dragging=false; held=false;
            var release=acquiredRelease; acquiredRelease=null;
            // Restore before callbacks. Even synchronous re-entry cannot inherit the previous pose or screen state.
            bool wasTransitioning=transitioning;
            transitioning=true;
            try
            {
                glove.Restore();
                if (displayRenderer && previousProperties != null) displayRenderer.SetPropertyBlock(previousProperties);
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
            if (!active || transitioning) return;
            if (!ReferencesValid()) { Exit(TerminalExitReason.Invalidated); return; }
            if (!TryMapPointer(screenPixels,out var uv))
            {
                pointerValid=false; dragging=false; held=false;
                return;
            }
            pointerValid=true; pointerUv=uv; held=isHeld;
            if (pressed)
            {
                contactAge=0; rippleAge=0; rippleUv=uv;
                previousPointer=uv; dragging=IsOverGlobe(uv);
                // Contact and its ripple share the exact screen point; smoothing applies to hover and release only.
                currentTip=PointOnScreen(uv,.001f);
                if (!glove.Place(currentTip,displayFrame.rotation*Quaternion.Euler(0,0,-8),leftGripFrame))
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

        private void Update()
        {
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

        private void PlaceHands(float dt)
        {
            float contact=pointerValid ? (held ? 1 : Mathf.Clamp01(1-contactAge/pressSeconds)) : 0;
            targetTip=PointOnScreen(pointerUv,Mathf.Lerp(hoverDistance,.001f,contact));
            currentTip=dt<=0 ? targetTip : Vector3.Lerp(currentTip,targetTip,1-Mathf.Exp(-35*dt));
            if (!glove.Place(currentTip,displayFrame.rotation*Quaternion.Euler(0,0,-8),leftGripFrame))
                Exit(TerminalExitReason.Invalidated);
        }

        private Vector3 PointOnScreen(Vector2 uv,float frontOffset) => displayFrame.TransformPoint(
            new Vector3((uv.x-.5f)*activeSizeMetres.x,(uv.y-.5f)*activeSizeMetres.y,-frontOffset));

        private void ApplyDisplay()
        {
            if (!active || !displayRenderer) return;
            properties.SetTexture(ObservationId,observationTexture);
            properties.SetVector(RotationId,new Vector4(globeRotation.x,globeRotation.y,globeRotation.z,globeRotation.w));
            properties.SetVector(PointerId,new Vector4(pointerUv.x,pointerUv.y,pointerValid?1:0,held?1:0));
            properties.SetVector(RippleId,new Vector4(rippleUv.x,rippleUv.y,rippleAge,active?1:0));
            properties.SetFloat(AvailableId,observationAvailable?1:0);
            displayRenderer.SetPropertyBlock(properties);
        }

        private void RefreshTime()
        {
            var utc=readMissionUtc();
            // Unspecified/Local clocks are rejected; presentation must never silently substitute PC local time.
            if (utc.Kind != DateTimeKind.Utc) throw new InvalidOperationException("Terminal mission UTC delegate must return DateTimeKind.Utc.");
            observationAvailable=utc>=ObservationUtc;
            SetLabel(missionLabel,"MISSION UTC\n"+utc.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture));
            SetLabel(observationLabel,"OBSERVED UTC\n2026-11-11 11:01:17");
            var age=utc-ObservationUtc;
            SetLabel(ageLabel,observationAvailable ? string.Format(CultureInfo.InvariantCulture,
                "IMAGE AGE\n{0}d {1:00}h {2:00}m {3:00}s",age.Days,age.Hours,age.Minutes,age.Seconds) : "OBSERVATION UNAVAILABLE\nAt this mission time");
            SetLabel(statusLabel,"ARCHIVED OBSERVATION\nNot a live feed or forecast.\nAURORA-7 position: not propagated.");
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
