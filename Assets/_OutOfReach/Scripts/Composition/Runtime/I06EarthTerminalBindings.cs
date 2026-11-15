using System;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Core.Runtime;
using OutOfReach.Interaction;
using OutOfReach.Player;
using OutOfReach.Presentation.Terminal;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OutOfReach.Composition
{
    /// <summary>Exclusive physical viewer lease. No movement, domain command, clock or alternate input source.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(350)]
    public sealed class I06EarthTerminalBindings : MonoBehaviour, IInteractable
    {
        public const string TargetId = "earth-archive-terminal";
        public const float MaximumEntryDistanceMetres = .550f;
        public const float MaximumFocusTranslationMetres = .120f;
        [SerializeField] PhysicalEarthTerminalPresenter terminal;
        [SerializeField] InteractableTarget interactionTarget;
        [SerializeField] Transform focusAnchor, dedicatedGloves;
        [SerializeField] TextAsset gloveRig;
        [SerializeField] Camera playerCamera;
        [SerializeField, Range(40,85)] float focusFieldOfView = 68;
        readonly RaycastHit[] hits = new RaycastHit[64];
        readonly Collider[] overlaps = new Collider[32];
        readonly ActionLease actions = new ActionLease();
        MissionRuntime runtime;
        MissionSession session;
        IMissionEventBus events;
        PlayerInputActions source;
        Func<bool> contextAllowed;
        Action beforeFocus;
        Transform savedCameraParent;
        Vector3 savedPosition;
        Quaternion savedRotation;
        float savedFov;
        CursorLockMode savedCursorLock;
        bool savedCursorVisible, focused, subscribed, pointerReady;
        int enteredFrame;
        public string InteractionId => TargetId;
        public string Prompt
        {
            get
            {
                if(runtime && runtime.Player && runtime.Player.ViewPivot && terminal && terminal.DisplayFrame
                    && focusAnchor && playerCamera && !IsEntered
                    && (Vector3.Distance(runtime.Player.ViewPivot.position,terminal.DisplayFrame.position)>MaximumEntryDistanceMetres
                        || Vector3.Distance(playerCamera.transform.position,focusAnchor.position)>MaximumFocusTranslationMetres))
                    return string.Empty;
                return string.Empty; // R33: wall display only; no prompt.
            }
        }
        public bool IsEntered => focused && terminal && terminal.IsEntered;
        // Root adds this condition to its EXISTING control/presentation policies; no delegate replacement here.
        public bool BlocksPlayerInput => focused || actions.Pending;
        public bool CanInteract => false; // R33: wall display only.
        bool WasInteractable => !BlocksPlayerInput && ContextValid() && runtime.Player.ControlAllowed
            && runtime.Player.InputSource is PlayerInputActions;
        public string LastRefusal { get; private set; } = string.Empty;
        public PhysicalEarthTerminalPresenter Terminal => terminal;
        public InteractableTarget Target => interactionTarget;
        public Transform FocusAnchor => focusAnchor;
        public Transform DedicatedGloves => dedicatedGloves;

        public void ConfigureScene(PhysicalEarthTerminalPresenter view, InteractableTarget target,
            Transform focus, Camera camera, Transform gloveInstance, TextAsset rig)
        {
            ClearBinding();
            if (!view || !target || !focus || !camera || !gloveInstance || !rig)
                throw new ArgumentException("Supply authored terminal, separate target, focus frame, sole camera and dedicated original gloves.");
            terminal=view; interactionTarget=target; focusAnchor=focus; playerCamera=camera;
            dedicatedGloves=gloveInstance; gloveRig=rig;
            interactionTarget.InteractionId=TargetId;
            terminal.ConfigureScene(camera,view.DisplayFrame,view.ActiveSizeMetres,view.DisplayRenderer,
                gloveInstance,rig,view.LeftGripFrame);
        }

        /// <summary>Bind BEFORE MissionCinematicPresenter.Configure so camera restoration precedes its snapshot.</summary>
        public void ConfigureSession(MissionRuntime actualRuntime, Func<bool> canPresentContext,
            Action cancelCompetingContact = null)
        {
            ClearBinding();
            if (!terminal || !interactionTarget || !playerCamera || !focusAnchor || !dedicatedGloves || !gloveRig
                || !actualRuntime || actualRuntime.Session == null || !actualRuntime.Player || canPresentContext == null)
                throw new ArgumentException("Bind complete authored references to the current real mission session.");
            runtime=actualRuntime; session=runtime.Session; events=runtime.Events;
            contextAllowed=canPresentContext; beforeFocus=cancelCompetingContact;
            // R33: the Earth tablet is a non-interactable live wall display; the target stays unbound and disabled.
            interactionTarget.enabled=false;
            terminal.ConfigureSession(Acquire,Release,() => !ContextValid(),() => runtime.Calendar.NowUtc);
            events.CinematicRequested+=OnCinematic; events.PhaseChanged+=OnPhase; events.MissionEnded+=OnEnded;
            runtime.PauseChanged+=OnPause; runtime.SessionEnding+=OnSessionEnding;
            subscribed=true;
        }

        public CommandResult TryInteract()
        {
            LastRefusal=string.Empty;
            bool ok=CanInteract && terminal.TryEnter();
            if (!ok && string.IsNullOrEmpty(LastRefusal)) LastRefusal="The Earth viewer is unavailable in the current context.";
            return new CommandResult(ok,ok?CommandFailure.None:CommandFailure.PreconditionsNotMet,
                ok?"Earth archive opened. Escape closes the viewer.":LastRefusal);
        }

        bool Acquire()
        {
            if (!CanInteract || !EntryVisible() || !CameraPathClear()) return false;
            source=runtime.Player.InputSource as PlayerInputActions;
            if (!actions.Acquire(source)) { source=null; return Refuse("The default Interact/Pause actions are unavailable or already leased."); }
            savedCameraParent=playerCamera.transform.parent;
            savedPosition=playerCamera.transform.localPosition; savedRotation=playerCamera.transform.localRotation;
            savedFov=playerCamera.fieldOfView; savedCursorLock=Cursor.lockState; savedCursorVisible=Cursor.visible;
            focused=true; enteredFrame=Time.frameCount;
            pointerReady=Mouse.current!=null && !Mouse.current.leftButton.isPressed;
            try
            {
                beforeFocus?.Invoke(); // Root releases actual micro contact; no fake surface or gameplay success.
                if (!ContextValid()) { Release(); return false; }
                ApplyCamera(); Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
                return true;
            }
            catch { Release(); throw; }
        }

        void Release()
        {
            if (!focused) return;
            focused=false; pointerReady=false;
            if (playerCamera && playerCamera.transform.parent==savedCameraParent)
            {
                playerCamera.transform.SetLocalPositionAndRotation(savedPosition,savedRotation);
                playerCamera.fieldOfView=savedFov;
            }
            Cursor.lockState=savedCursorLock; Cursor.visible=savedCursorVisible;
            // Escape must not become Player.Pause or Interact in the same input frame, including disable/restart.
            actions.ReleaseAfterCurrentFrame(); source=null; savedCameraParent=null;
        }

        public void Exit(TerminalExitReason reason=TerminalExitReason.User)
        { if (terminal) terminal.Exit(reason); Release(); }

        public void ClearBinding()
        {
            Exit(TerminalExitReason.Reconfigured);
            if (subscribed)
            {
                events.CinematicRequested-=OnCinematic; events.PhaseChanged-=OnPhase; events.MissionEnded-=OnEnded;
                if (runtime) { runtime.PauseChanged-=OnPause; runtime.SessionEnding-=OnSessionEnding; }
            }
            subscribed=false;
            if (interactionTarget && ReferenceEquals(interactionTarget.Interactable,this)) interactionTarget.Unbind();
            if (terminal) terminal.ConfigureSession(null,null,null,null);
            runtime=null; session=null; events=null; contextAllowed=null; beforeFocus=null;
        }
        void OnDisable() => ClearBinding();
        void OnDestroy() => ClearBinding();
        void OnApplicationFocus(bool value) { if (!value) Exit(TerminalExitReason.Invalidated); }
        void OnPause(bool value) { if (value) Exit(TerminalExitReason.Pause); }
        void OnSessionEnding(MissionSession value) { if (ReferenceEquals(value,session)) ClearBinding(); }
        void OnCinematic(CinematicRequested value) => Exit(TerminalExitReason.Invalidated);
        void OnPhase(PhaseChanged value) { if (focused) Exit(TerminalExitReason.Invalidated); }
        void OnEnded(MissionEnded value) => Exit(TerminalExitReason.Invalidated);

        void Update()
        {
            if (!focused) return;
            if (!ContextValid() || !ReferenceEquals(runtime.Player.InputSource,source) || !actions.Owned)
            { Exit(TerminalExitReason.Invalidated); return; }
            if ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame))
            { Exit(TerminalExitReason.Escape); return; }
            if (Time.frameCount==enteredFrame) return; // Opening Interact/LMB cannot also press the screen.
            var mouse=Mouse.current;
            if (mouse==null) return;
            if(!pointerReady)
            {
                if(!mouse.leftButton.isPressed) pointerReady=true;
                else { terminal.SubmitPointer(new Vector2(float.NaN,float.NaN),false,false,true); return; }
            }
            Vector2 pixels=mouse.position.ReadValue();
            if (!PointerVisible(pixels))
            { terminal.SubmitPointer(new Vector2(float.NaN,float.NaN),false,false,true); return; }
            terminal.SubmitPointer(pixels,mouse.leftButton.wasPressedThisFrame,
                mouse.leftButton.isPressed,mouse.leftButton.wasReleasedThisFrame);
        }
        void LateUpdate()
        {
            if (!focused) return;
            if (!ContextValid()) { Exit(TerminalExitReason.Invalidated); return; }
            ApplyCamera(); Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
        }
        void ApplyCamera()
        {
            playerCamera.transform.SetPositionAndRotation(focusAnchor.position,focusAnchor.rotation);
            playerCamera.fieldOfView=focusFieldOfView;
        }
        bool ContextValid()
        {
            if (!isActiveAndEnabled || !runtime || !runtime.isActiveAndEnabled || session==null
                || !ReferenceEquals(runtime.Session,session) || !runtime.Player || !runtime.Player.isActiveAndEnabled
                || runtime.IsPaused || runtime.GameState.Outcome!=MissionOutcome.None
                || runtime.GameState.Phase==GamePhase.Planning || runtime.GameState.Phase==GamePhase.Intro
                || runtime.Player.IsOnRoute || runtime.EvaStatus.IsOutside
                || (runtime.SurvivalRecovery?.IsResting ?? false)
                || !string.IsNullOrEmpty(runtime.Cargo?.CarriedEquipmentId)
                || !terminal || !terminal.isActiveAndEnabled || !focusAnchor || !playerCamera) return false;
            try { return contextAllowed!=null && contextAllowed(); }
            catch (Exception error) { Debug.LogException(error,this); return false; }
        }
        bool EntryVisible()
        {
            var eye=runtime.Player.ViewPivot;
            if (!eye || terminal.DisplayFrame.InverseTransformPoint(eye.position).z>=-.05f)
                return Refuse("Approach the front of the physical display.");
            if(Vector3.Distance(eye.position,terminal.DisplayFrame.position)>MaximumEntryDistanceMetres)
                return Refuse("Move closer to view Earth archive.");
            float reach=runtime.Player.Interaction ? runtime.Player.Interaction.ReachMetres : 0;
            if (!Nearest(eye.position,eye.forward,reach,QueryTriggerInteraction.Collide,out var hit)
                || !hit.collider.transform.IsChildOf(terminal.transform))
                return Refuse("The terminal is not the nearest visible interaction surface within reach.");
            return true;
        }
        bool CameraPathClear()
        {
            int count=Physics.OverlapSphereNonAlloc(focusAnchor.position,.055f,overlaps,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if (count==overlaps.Length) return Refuse("Focus clearance query saturated.");
            for(int i=0;i<count;i++) if (!Ignored(overlaps[i])) return Refuse("The terminal focus frame overlaps a solid surface.");
            Vector3 delta=focusAnchor.position-playerCamera.transform.position;
            if (delta.magnitude>MaximumFocusTranslationMetres) return Refuse("Move closer to view Earth archive.");
            if (delta.sqrMagnitude>.000001f)
            {
                count=Physics.SphereCastNonAlloc(playerCamera.transform.position,.035f,delta.normalized,hits,delta.magnitude,
                    Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                if(count==hits.Length) return Refuse("Focus path query saturated.");
                for(int i=0;i<count;i++) if (!Ignored(hits[i].collider)) return Refuse("A solid surface blocks the camera approach.");
            }
            return ScreenPointVisible(focusAnchor.position,terminal.DisplayFrame.position);
        }
        bool PointerVisible(Vector2 pixels)
        {
            if (!terminal.TryMapPointer(pixels,out var uv)) return false;
            var size=terminal.ActiveSizeMetres;
            Vector3 point=terminal.DisplayFrame.TransformPoint(new Vector3((uv.x-.5f)*size.x,(uv.y-.5f)*size.y,0));
            return ScreenPointVisible(playerCamera.transform.position,point);
        }
        bool ScreenPointVisible(Vector3 origin,Vector3 point)
        {
            Vector3 delta=point-origin;
            if (delta.sqrMagnitude<.0025f || !Nearest(origin,delta.normalized,delta.magnitude+.012f,
                QueryTriggerInteraction.Ignore,out var hit)) return false;
            return hit.collider.transform.IsChildOf(terminal.transform) && Vector3.Distance(hit.point,point)<.015f;
        }
        bool Nearest(Vector3 origin,Vector3 direction,float distance,QueryTriggerInteraction query,out RaycastHit nearest)
        {
            nearest=default;
            int count=Physics.RaycastNonAlloc(origin,direction,hits,distance,Physics.DefaultRaycastLayers,query);
            if(count==hits.Length) return false;
            float best=float.PositiveInfinity;
            for(int i=0;i<count;i++) if (!Ignored(hits[i].collider) && hits[i].distance<best)
            { best=hits[i].distance; nearest=hits[i]; }
            return nearest.collider;
        }
        bool Ignored(Collider collider) => !collider || (runtime && runtime.Player && collider.transform.IsChildOf(runtime.Player.transform))
            || (dedicatedGloves && collider.transform.IsChildOf(dedicatedGloves));
        bool Refuse(string reason) { LastRefusal=reason; return false; }

        // Survives host disable until the following input frame; unregisters immediately after that one release.
        sealed class ActionLease
        {
            InputAction interact,pause;
            InputActionMap map;
            bool held,scheduled;
            int releaseFrame;
            public bool Pending => scheduled;
            public bool Owned => held && interact!=null && pause!=null && map.enabled && !interact.enabled && !pause.enabled;
            public bool Acquire(PlayerInputActions source)
            {
                if (held || scheduled || source==null || !source.Map.enabled) return false;
                var i=source.Map.FindAction("Interact",false); var p=source.Map.FindAction("Pause",false);
                if(i==null || p==null || !i.enabled || !p.enabled) return false;
                map=source.Map; interact=i; pause=p; held=true;
                try { interact.Disable(); pause.Disable(); if(Owned) return true; Restore(); return false; }
                catch { Restore(); throw; }
            }
            public void ReleaseAfterCurrentFrame()
            {
                if(!held || scheduled) return;
                held=false; scheduled=true; releaseFrame=Time.frameCount;
                InputSystem.onAfterUpdate+=RestoreNextFrame;
            }
            void RestoreNextFrame()
            {
                if(Time.frameCount<=releaseFrame) return;
                Restore();
            }
            void Restore()
            {
                if(scheduled) InputSystem.onAfterUpdate-=RestoreNextFrame;
                var i=interact; var p=pause; var m=map;
                held=scheduled=false; interact=pause=null; map=null;
                // A disabled/disposed whole map belongs to Player teardown; never resurrect it.
                try { if(m!=null && m.enabled) { if(i!=null && !i.enabled) i.Enable(); if(p!=null && !p.enabled) p.Enable(); } }
                catch(ObjectDisposedException) { }
            }
        }
    }
}
