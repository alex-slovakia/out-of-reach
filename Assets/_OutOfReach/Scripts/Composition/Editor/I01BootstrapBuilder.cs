using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OutOfReach.Core;
using OutOfReach.Core.Runtime;
using OutOfReach.Interaction;
using OutOfReach.Player;
using OutOfReach.Presentation;
using OutOfReach.Presentation.Audio;
using OutOfReach.Presentation.Cargo;
using OutOfReach.Presentation.Cargo.Editor;
using OutOfReach.Presentation.AirlockPanel.Editor;
using OutOfReach.Presentation.Cinematics;
using OutOfReach.Presentation.Cinematics.Preflight;
using OutOfReach.Presentation.Lighting;
using OutOfReach.Presentation.Motion;
using OutOfReach.Presentation.Hands;
using OutOfReach.Presentation.UI;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Image = UnityEngine.UI.Image;
using Object = UnityEngine.Object;

namespace OutOfReach.Composition.Editor
{
    /// <summary>Integrator-only authoring. Changes Bootstrap in the ignored verification project, never source prefabs/settings.</summary>
    public static class I01BootstrapBuilder
    {
        public const string ScenePath = "Assets/_OutOfReach/Scenes/Bootstrap.unity";
        public const string ShipPath = "Assets/_OutOfReach/Visuals/Ship/Aurora7Interior.prefab";
        public const string ModelPath = "Assets/_OutOfReach/Visuals/Models/Aurora7/Prefabs/Aurora7_Models.prefab";
        public const string PlayerPath = "Assets/_OutOfReach/Scripts/Player/PlayerRig.prefab";
        public const string BoardPath = "Assets/_OutOfReach/UI/Generated/MissionFlightBoard.prefab";
        public const string AudioPath = "Assets/_OutOfReach/Audio/Runtime/MissionAudioPresenter.prefab";
        public const string HandsPath = "Assets/_OutOfReach/Visuals/Models/Aurora7/Props/AstronautHands/AstronautHands.prefab";
        public const string HandsRigPath = "Assets/_OutOfReach/Visuals/Models/Aurora7/Props/AstronautHands/AstronautHands_rig.json";

        [MenuItem("OUT OF REACH/Integration/Build I01 Bootstrap in verification project")]
        public static void BuildAndValidate()
        {
            RequireVerificationProject();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before composing Bootstrap.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // Resolve all owned inputs before replacing the active scene.
            GameObject shipAsset = Required<GameObject>(ShipPath);
            GameObject playerAsset = Required<GameObject>(PlayerPath);
            GameObject boardAsset = Required<GameObject>(BoardPath);
            GameObject audioAsset = Required<GameObject>(AudioPath);
            GameObject handsAsset = Required<GameObject>(HandsPath);
            TextAsset handsRig = Required<TextAsset>(HandsRigPath);
            Font font = Required<Font>("Assets/_OutOfReach/UI/Typography/SourceSans3-Regular.otf");
            if (shipAsset.GetComponentsInChildren<Camera>(true).Length != 0 || shipAsset.GetComponentsInChildren<AudioListener>(true).Length != 0)
                throw new InvalidOperationException("The ship input must not contain a camera or listener.");
            if (playerAsset.GetComponentsInChildren<Camera>(true).Length != 0 || playerAsset.GetComponentsInChildren<AudioListener>(true).Length != 0)
                throw new InvalidOperationException("The PlayerRig input must not contain a camera or listener.");

            string previousGuid = AssetDatabase.AssetPathToGUID(ScenePath);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureEnvironment();
            GameObject ship = (GameObject)PrefabUtility.InstantiatePrefab(shipAsset, scene);
            ship.name = "AURORA-7 interior";
            ship.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            ShipVisualAnchors anchors = ship.GetComponent<ShipVisualAnchors>();
            ValidateAnchors(anchors);
            AttachDeliveredModels(scene, ship, anchors);

            GameObject playerObject = (GameObject)PrefabUtility.InstantiatePrefab(playerAsset, scene);
            FirstPersonController player = playerObject.GetComponent<FirstPersonController>();
            if (!player || !player.ViewPivot || !player.Interaction)
                throw new InvalidOperationException("PlayerRig is missing its controller, ViewPivot or InteractionResolver.");
            // A positive cutoff discards valid movement at very high frame rates. This scene
            // override preserves the authored speed and source prefab, including its GUID.
            CharacterController playerBody = playerObject.GetComponent<CharacterController>();
            if (!playerBody) throw new InvalidOperationException("PlayerRig is missing its CharacterController.");
            playerBody.minMoveDistance = 0f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(playerBody);
            player.Teleport(anchors.PlayerSpawn.position, anchors.PlayerSpawn.eulerAngles.y);
            Transform planningPivot = Child("Planning framing", player.ViewPivot);
            Transform cinematicPivot = Child("Cinematic offset", planningPivot);
            Transform motionPivot = Child("First person visual motion", cinematicPivot);
            Transform menuMotionPivot = Child("Opening menu drift", motionPivot);
            Transform cameraTransform = Child("Player camera", menuMotionPivot);
            Camera camera = cameraTransform.gameObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 68;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 100;
            camera.clearFlags = RenderSettings.skybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.005f, .008f, .015f);
            camera.allowHDR = true;
            camera.allowMSAA = true;
            ConfigureCinematicGrade(camera);
            cameraTransform.gameObject.AddComponent<AudioListener>();
            // Keep the previous aim pose above the camera-only walk offset.
            player.Interaction.Configure(cinematicPivot, player.transform, 2f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.Interaction);

            // Keep the delivered asset untouched. Its model root and both arms remain separate from the camera.
            Transform handsMount = Child("Hands visual motion", cameraTransform);
            GameObject handsObject = (GameObject)PrefabUtility.InstantiatePrefab(handsAsset, scene);
            handsObject.transform.SetParent(handsMount, false);
            handsObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            handsObject.transform.localScale = Vector3.one;
            handsObject.SetActive(false); // No gloved hands in the unsuited cabin or first Planning frame.
            PrefabUtility.RecordPrefabInstancePropertyModifications(handsObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(handsObject.transform);

            Transform overview = Child("Planning cockpit overview", null);
            overview.position = new Vector3(1.8f, 2.15f, 2.15f);
            overview.rotation = Quaternion.LookRotation(new Vector3(-.6f, 1.3f, -1.75f) - overview.position);

            Transform suitStation = Child("Suit locker interaction anchor", null);
            suitStation.SetPositionAndRotation(new Vector3(1.72f, 1.4f, 19f), Quaternion.Euler(0, -90, 0));
            Transform airlockPanel = Child("Airlock controls interaction anchor", null);
            airlockPanel.SetPositionAndRotation(new Vector3(1.45f, 1.375f, 20.83f), Quaternion.identity);
            var targets = new[]
            {
                Target(anchors.CockpitConsole, InteractionIds.CockpitConsole, new Vector3(0, .35f, .3f)),
                Target(anchors.PowerRepair, InteractionIds.RepairPower, new Vector3(0, .3f, .18f)),
                Target(anchors.LifeSupportRepair, InteractionIds.RepairLifeSupport, new Vector3(0, .3f, .18f)),
                Target(suitStation, InteractionIds.SuitLocker, new Vector3(0, 0, .12f)),
                Target(airlockPanel, InteractionIds.AirlockControls, new Vector3(0, 0, -.10f)),
                Target(anchors.AirlockOuter, InteractionIds.EvaExit, new Vector3(0, 1.4f, -.35f)),
                Target(anchors.EvaReturn, InteractionIds.EvaReturn, new Vector3(0, 1.4f, 0)),
                Target(anchors.AntennaRepair, InteractionIds.RepairAntenna, new Vector3(0, 0, -.12f))
            };
            var cargoStage = CargoStageAuthoring.Create(null, cameraTransform, font);
            var allTargets = targets.Concat(cargoStage.Targets).ToArray();
            GameplayGate innerGate = Gate(anchors.AirlockInner, GateIds.AirlockInner);
            GameplayGate outerGate = Gate(anchors.AirlockOuter, GateIds.AirlockOuter);
            innerGate.SetOpen(true); // Nominal editor pose; MissionRuntime reads actual authority at Awake and every frame.
            outerGate.SetOpen(false);
            TetheredRoute route = CreateEvaRoute(anchors);
            Transform reentry = Child("EVA reentry feet", null);
            reentry.SetPositionAndRotation(new Vector3(0, .05f, 19.9f), Quaternion.Euler(0, 180, 0));

            GameObject boardObject = (GameObject)PrefabUtility.InstantiatePrefab(boardAsset, scene);
            MissionFlightBoard board = boardObject.GetComponent<MissionFlightBoard>();
            UIDocument document = boardObject.GetComponent<UIDocument>();
            if (!board || !document || !document.panelSettings)
                throw new InvalidOperationException("Flight board prefab needs its view and PanelSettings.");
            // Overlay canvas is beneath the readable UI Toolkit flight board.
            document.sortingOrder = 100;
            GameObject audioObject = (GameObject)PrefabUtility.InstantiatePrefab(audioAsset, scene);
            MissionAudioPresenter audio = audioObject.GetComponent<MissionAudioPresenter>();
            if (!audio || !audio.ValidateConfiguration(out string audioReason))
                throw new InvalidOperationException("Audio prefab needs valid explicit music/SFX bindings.");

            Transform presentationRoot = Child("I01 presentation", null);
            FootstepAudioPresenter footsteps = presentationRoot.gameObject.AddComponent<FootstepAudioPresenter>();
            footsteps.ConfigureClips(new[] {
                Required<AudioClip>("Assets/_OutOfReach/Audio/Original/DeckContacts/deck-contact-01.wav"),
                Required<AudioClip>("Assets/_OutOfReach/Audio/Original/DeckContacts/deck-contact-02.wav"),
                Required<AudioClip>("Assets/_OutOfReach/Audio/Original/DeckContacts/deck-contact-03.wav")
            });
            FirstPersonMotionPresenter walkMotion = presentationRoot.gameObject.AddComponent<FirstPersonMotionPresenter>();
            var motionFields = new SerializedObject(walkMotion);
            motionFields.FindProperty("cameraVerticalMetres").floatValue = .014f;
            motionFields.ApplyModifiedPropertiesWithoutUndo();
            OpeningMenuMotionPresenter menuMotion = presentationRoot.gameObject.AddComponent<OpeningMenuMotionPresenter>();
            AstronautHandsPresenter hands = presentationRoot.gameObject.AddComponent<AstronautHandsPresenter>();
            hands.ConfigureModel(handsObject.transform, handsMount, handsRig);
            CreateShipLighting(ship, presentationRoot, out Light[] workingLights, out Light[] emergencyLights);
            ShipLightingPresenter shipLighting = presentationRoot.gameObject.AddComponent<ShipLightingPresenter>();
            var lightingFields = new SerializedObject(shipLighting);
            SetArray(lightingFields, "workingLights", workingLights.Cast<Object>().ToArray());
            SetArray(lightingFields, "emergencyLights", emergencyLights.Cast<Object>().ToArray());
            lightingFields.ApplyModifiedPropertiesWithoutUndo();
            I01AirlockDoorView doors = presentationRoot.gameObject.AddComponent<I01AirlockDoorView>();
            doors.ConfigureLeaves(anchors.InnerDoorLeft, anchors.InnerDoorRight, anchors.OuterDoorLeft, anchors.OuterDoorRight,
                anchors.AirlockInner, anchors.AirlockOuter);
            Light cockpitEffect = PointLight("Cinematic cockpit fill", presentationRoot, new Vector3(0, 2.45f, -.6f), 5f, .18f);
            Light engineeringEffect = PointLight("Cinematic engineering fill", presentationRoot, new Vector3(0, 2.45f, 7), 6f, .18f);
            Light powerLight = PointLight("Power status", presentationRoot,
                anchors.PowerRepair.position + anchors.PowerRepair.forward * .5f + Vector3.up * .4f, 1.4f, .28f);
            Light lifeLight = PointLight("Life support status", presentationRoot,
                anchors.LifeSupportRepair.position + anchors.LifeSupportRepair.forward * .5f + Vector3.up * .4f, 1.4f, .28f);
            Canvas canvas = Child("Cinematic overlays", presentationRoot).gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            CanvasGroup blackout = Overlay("Impact fade", canvas.transform, Color.black);
            CanvasGroup emergency = Overlay("Emergency tint", canvas.transform, new Color(.65f, .015f, .005f));
            MissionCinematicPresenter cinematics = presentationRoot.gameObject.AddComponent<MissionCinematicPresenter>();
            var cinematicFields = new SerializedObject(cinematics);
            SetObject(cinematicFields, "cameraOffsetPivot", cinematicPivot);
            SetObject(cinematicFields, "endingFramingAnchor", anchors.FinaleCameraAnchor);
            SetObject(cinematicFields, "blackoutOverlay", blackout);
            SetObject(cinematicFields, "emergencyOverlay", emergency);
            SetArray(cinematicFields, "effectLights", new Object[] { cockpitEffect, engineeringEffect });
            cinematicFields.ApplyModifiedPropertiesWithoutUndo();

            Transform missionRoot = Child("I01 mission composition", null);
            MissionRuntime runtime = missionRoot.gameObject.AddComponent<MissionRuntime>();
            runtime.Configure(player, anchors.PlayerSpawn, allTargets);
            runtime.ConfigurePreflight(true, cargoStage.Spawn);
            runtime.ConfigureImpactMovement(true);
            runtime.ConfigureEva(new[] { innerGate, outerGate }, route, reentry);
            var runtimeFields = new SerializedObject(runtime);
            runtimeFields.FindProperty("startingPresetId").stringValue = AuroraMission.ScienceFocusPresetId;
            runtimeFields.FindProperty("planningDeadlineRealSeconds").doubleValue = 60d;
            runtimeFields.FindProperty("startOnAwake").boolValue = true;
            runtimeFields.FindProperty("driveFromUpdate").boolValue = true;
            runtimeFields.ApplyModifiedPropertiesWithoutUndo();
            I01SessionControls controls = missionRoot.gameObject.AddComponent<I01SessionControls>();
            controls.ConfigureDocument(document, font,
                AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/_OutOfReach/UI/Report/Runtime/FlightRecorder.uss"));
            I01MissionComposition composition = missionRoot.gameObject.AddComponent<I01MissionComposition>();
            composition.Configure(runtime, board, cinematics, audio, player.Interaction, controls,
                planningPivot, overview, powerLight, lifeLight);
            composition.ConfigureShipPresentation(doors, shipLighting, workingLights, emergencyLights);
            composition.ConfigureWalkMotion(walkMotion, motionPivot);
            composition.ConfigureFootsteps(footsteps);
            composition.ConfigureOpeningMenuMotion(menuMotion, menuMotionPivot);
            composition.ConfigurePauseBackdrop(presentationRoot.gameObject.AddComponent<I01PauseBackdrop>(), camera);
            Transform airlockScreenMount = Child("Airlock digital display mount", presentationRoot);
            airlockScreenMount.position = new Vector3(1.45f, 1.375f, 20.829f);
            composition.ConfigureAirlockPanel(AirlockPanelBuilder.Create(airlockScreenMount,
                Required<Font>("Assets/_OutOfReach/UI/Typography/SourceSans3-Semibold.otf")),
                Required<AudioClip>("Assets/_OutOfReach/Audio/Original/Airlock/airlock-latch-motor.wav"),
                Required<AudioClip>("Assets/_OutOfReach/Audio/Original/Airlock/airlock-safe-chirp.wav"));
            composition.ConfigureHands(hands);
            composition.ConfigurePreflightTension(presentationRoot.gameObject.AddComponent<PreflightTensionPresenter>(),
                Required<AudioClip>("Assets/_OutOfReach/Audio/SFX/planning_tick.wav"));
            composition.ConfigureCargoPresentation(cargoStage.Presenter);
            presentationRoot.gameObject.AddComponent<CargoDestinationGuide>().Configure(cargoStage.Presenter, camera,
                document, Required<StyleSheet>("Assets/_OutOfReach/UI/Styles/MissionFlightBoard.uss"), font);
            composition.ConfigureCargoHands(cargoStage.Hands);
            composition.ConfigureSurvivalUi(presentationRoot.gameObject.AddComponent<SurvivalChapterPresenter>(),
                document, Required<StyleSheet>("Assets/_OutOfReach/UI/Styles/SurvivalChapter.uss"));
            BindApprovedDialogue(composition, cinematics);

            // Physical preflight uses the actual body and aim pivot; list-mode retains its authored overview.
            if (runtime.PreflightWalk)
            {
                player.Teleport(cargoStage.Spawn.position, cargoStage.Spawn.eulerAngles.y);
                planningPivot.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
            else planningPivot.SetPositionAndRotation(overview.position, overview.rotation);
            Physics.SyncTransforms();
            ValidateScene(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save Bootstrap.");
            string savedGuid = AssetDatabase.AssetPathToGUID(ScenePath);
            if (!string.IsNullOrEmpty(previousGuid) && previousGuid != savedGuid)
                throw new InvalidOperationException("Bootstrap metadata identity unexpectedly changed.");
            Debug.Log("I01_BOOTSTRAP_CREATED " + ScenePath + " sceneGuid=" + savedGuid + " | no settings or source prefabs changed; Play mode remains unverified");
        }

        [MenuItem("OUT OF REACH/Integration/Validate saved I01 Bootstrap")]
        public static void ValidateSaved()
        {
            RequireVerificationProject();
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ValidateScene(scene);
        }

        [MenuItem("OUT OF REACH/Integration/Build I02 Windows x64 verification player")]
        public static void BuildWindows()
        {
            BuildWindowsCheckpoint("I02");
        }

        public static void BuildI04Windows() => BuildWindowsCheckpoint("I04");
        public static void BuildI05Windows() => BuildWindowsCheckpoint("I05");

        private static void BuildWindowsCheckpoint(string checkpoint)
        {
            RequireVerificationProject();
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ValidateScene(EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single));
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/" + checkpoint + "/OutOfReach.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(checkpoint + " Windows build failed: " + report.summary.result);
            Debug.Log(checkpoint + "_WINDOWS_BUILD_PASS output=" + output + " bytes=" + report.summary.totalSize
                + " | Development Windows x64, Bootstrap only. Executable launch/playthrough not established by this build.");
        }

        public static void BuildAndCapture()
        {
            BuildAndValidate();
            CaptureIntegrated();
        }

        [MenuItem("OUT OF REACH/Integration/Capture actual Bootstrap interiors")]
        public static void CaptureIntegrated()
        {
            RequireVerificationProject();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("CaptureIntegrated is an edit-time scene capture, not a playthrough.");
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ValidateScene(scene);
            GameObject[] roots = scene.GetRootGameObjects();
            Camera camera = ExactlyOne<Camera>(roots);
            var documents = roots.SelectMany(root => root.GetComponentsInChildren<UIDocument>(true)).ToArray();
            var canvases = roots.SelectMany(root => root.GetComponentsInChildren<Canvas>(true)).ToArray();
            bool[] documentStates = documents.Select(document => document.enabled).ToArray();
            bool[] canvasStates = canvases.Select(canvas => canvas.enabled).ToArray();
            Vector3 oldPosition = camera.transform.position;
            Quaternion oldRotation = camera.transform.rotation;
            float oldFov = camera.fieldOfView;
            RenderTexture oldTarget = camera.targetTexture;
            RenderTexture oldActive = RenderTexture.active;
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../.local/integrated-captures"));
            Directory.CreateDirectory(output);
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                foreach (UIDocument document in documents) document.enabled = false;
                foreach (Canvas canvas in canvases) canvas.enabled = false;
                camera.targetTexture = target;
                camera.fieldOfView = 68f;
                string[] names = { "cockpit-nominal", "engineering-nominal", "power-worksite-nominal", "airlock-nominal" };
                Vector3[] eyes = { new Vector3(0, 1.65f, .8f), new Vector3(0, 1.65f, 4.4f),
                    new Vector3(-.4f, 1.65f, 6.1f), new Vector3(0, 1.65f, 18f) };
                Vector3[] aims = { new Vector3(0, 1.45f, -2f), new Vector3(-.35f, 1.5f, 8.5f),
                    new Vector3(-1.8f, 1.3f, 6.1f), new Vector3(0, 1.45f, 20.5f) };
                for (int i = 0; i < names.Length; i++)
                {
                    camera.transform.SetPositionAndRotation(eyes[i], Quaternion.LookRotation(aims[i] - eyes[i]));
                    for (int warmup = 0; warmup < 3; warmup++) camera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                    pixels.Apply();
                    string path = Path.Combine(output, names[i] + ".png");
                    File.WriteAllBytes(path, pixels.EncodeToPNG());
                    Debug.Log("INTEGRATED_SCENE_CAPTURE " + path + " | actual Bootstrap, 1.65 m eye, nominal authored lights, UI hidden; not gameplay evidence");
                }
            }
            finally
            {
                camera.transform.SetPositionAndRotation(oldPosition, oldRotation);
                camera.fieldOfView = oldFov;
                camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                for (int i = 0; i < documents.Length; i++) documents[i].enabled = documentStates[i];
                for (int i = 0; i < canvases.Length; i++) canvases[i].enabled = canvasStates[i];
                ShaderUtil.allowAsyncCompilation = asyncShaders;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
            }
        }

        public static void ValidateScene(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            Camera camera = ExactlyOne<Camera>(roots);
            AudioListener listener = ExactlyOne<AudioListener>(roots);
            MissionRuntime runtime = ExactlyOne<MissionRuntime>(roots);
            FirstPersonController player = ExactlyOne<FirstPersonController>(roots);
            I01MissionComposition composition = ExactlyOne<I01MissionComposition>(roots);
            FirstPersonMotionPresenter walkMotion = ExactlyOne<FirstPersonMotionPresenter>(roots);
            OpeningMenuMotionPresenter menuMotion = ExactlyOne<OpeningMenuMotionPresenter>(roots);
            ExactlyOne<MissionFlightBoard>(roots);
            MissionCinematicPresenter cinematic = ExactlyOne<MissionCinematicPresenter>(roots);
            ExactlyOne<MissionAudioPresenter>(roots);
            AstronautHandsPresenter hands = ExactlyOne<AstronautHandsPresenter>(roots);
            CargoVisualPresenter cargo = ExactlyOne<CargoVisualPresenter>(roots);
            if (!hands.ModelRoot || !hands.MotionMount || !hands.RigDefinition
                || hands.ModelRoot.parent != hands.MotionMount || hands.MotionMount.parent != camera.transform
                || hands.ModelRoot.localPosition != Vector3.zero || hands.ModelRoot.localRotation != Quaternion.identity
                || hands.ModelRoot.localScale != Vector3.one || hands.ModelRoot.gameObject.activeSelf)
                throw new InvalidOperationException("Hands must use the delivered JSON, a separate camera child mount and a neutral, initially hidden prefab root.");
            if (camera.GetComponent<AudioListener>() != listener || !camera.transform.IsChildOf(player.ViewPivot))
                throw new InvalidOperationException("The sole camera and listener must share the PlayerRig ViewPivot hierarchy.");
            var compositionFields = new SerializedObject(composition);
            var cinematicFields = new SerializedObject(cinematic);
            var resolverFields = new SerializedObject(player.Interaction);
            Transform motionPivot = compositionFields.FindProperty("firstPersonMotionPivot").objectReferenceValue as Transform;
            Transform menuMotionPivot = compositionFields.FindProperty("openingMenuMotionPivot").objectReferenceValue as Transform;
            Transform cinematicPivot = cinematicFields.FindProperty("cameraOffsetPivot").objectReferenceValue as Transform;
            if (compositionFields.FindProperty("firstPersonMotion").objectReferenceValue != walkMotion
                || compositionFields.FindProperty("openingMenuMotion").objectReferenceValue != menuMotion
                || !motionPivot || !cinematicPivot || motionPivot == cinematicPivot
                || !menuMotionPivot || menuMotionPivot.parent != motionPivot || camera.transform.parent != menuMotionPivot
                || motionPivot.parent != cinematicPivot
                || resolverFields.FindProperty("viewOrigin").objectReferenceValue != cinematicPivot
                || motionPivot.localPosition != Vector3.zero || motionPivot.localRotation != Quaternion.identity
                || menuMotionPivot.localPosition != Vector3.zero || menuMotionPivot.localRotation != Quaternion.identity
                || camera.transform.localPosition != Vector3.zero || camera.transform.localRotation != Quaternion.identity)
                throw new InvalidOperationException("Walk and menu motion require separate neutral camera pivots and an unaffected interaction origin.");
            if (!runtime.DriveFromUpdate || runtime.Player != player)
                throw new InvalidOperationException("The sole MissionRuntime must drive and bind the player.");
            ShipVisualAnchors anchors = ExactlyOne<ShipVisualAnchors>(roots);
            ValidateAnchors(anchors);
            var targets = roots.SelectMany(root => root.GetComponentsInChildren<InteractableTarget>(true)).ToArray();
            if (targets.Length != 22 || runtime.InteractionTargets.Count != 22
                || runtime.InteractionTargets.Distinct().Count() != 22
                || runtime.InteractionTargets.Any(target => !target || !targets.Contains(target)))
                throw new InvalidOperationException("C05-A requires the eight mission targets and fourteen distinct, explicitly bound cargo targets.");
            var runtimeFields = new SerializedObject(runtime);
            Transform preflightSpawn = runtimeFields.FindProperty("preflightSpawn").objectReferenceValue as Transform;
            Transform planningPivot = compositionFields.FindProperty("planningFramingPivot").objectReferenceValue as Transform;
            if (!runtime.PreflightWalk || !preflightSpawn
                || Vector3.Distance(preflightSpawn.position, new Vector3(0, .05f, 13.5f)) > .001f
                || compositionFields.FindProperty("cargoPresentation").objectReferenceValue != cargo
                || !planningPivot || planningPivot.localPosition != Vector3.zero || planningPivot.localRotation != Quaternion.identity)
                throw new InvalidOperationException("C05-A needs its explicit cargo presenter, storage preflight spawn and body-relative planning camera.");
            string[] expectedIds = { InteractionIds.CockpitConsole, InteractionIds.RepairPower, InteractionIds.RepairLifeSupport,
                InteractionIds.SuitLocker, InteractionIds.AirlockControls, InteractionIds.EvaExit, InteractionIds.EvaReturn, InteractionIds.RepairAntenna };
            expectedIds = expectedIds.Concat(CargoStageAuthoring.EquipmentIds.SelectMany(id =>
                new[] { InteractionIds.CargoSupply(id), InteractionIds.CargoBay(id) })).ToArray();
            foreach (string id in expectedIds)
                if (targets.Count(target => target.InteractionId == id) != 1)
                    throw new InvalidOperationException("Missing or duplicated interaction ID: " + id);
            Physics.SyncTransforms();
            CheckReach(targets.Single(target => target.InteractionId == InteractionIds.CockpitConsole), new Vector3(0, 1.7f, -.1f), player.transform);
            CheckReach(targets.Single(target => target.InteractionId == InteractionIds.RepairPower), new Vector3(0, 1.7f, 6.1f), player.transform);
            CheckReach(targets.Single(target => target.InteractionId == InteractionIds.RepairLifeSupport), new Vector3(0, 1.7f, 8.1f), player.transform);
            CheckReach(targets.Single(target => target.InteractionId == InteractionIds.SuitLocker), new Vector3(.2f, 1.7f, 19), player.transform);
            CheckReach(targets.Single(target => target.InteractionId == InteractionIds.AirlockControls), new Vector3(.3f, 1.7f, 19.8f), player.transform);
            CheckReach(targets.Single(target => target.InteractionId == InteractionIds.EvaExit), new Vector3(0, 1.7f, 19.8f), player.transform);
            CheckReach(targets.Single(target => target.InteractionId == InteractionIds.EvaReturn), runtime.EvaRoute.StartPosition + Vector3.up * 1.65f, player.transform);
            CheckReach(targets.Single(target => target.InteractionId == InteractionIds.RepairAntenna), runtime.EvaRoute.EndPosition + Vector3.up * 1.65f, player.transform);
            // Keep all original eight reach checks above; add the fourteen explicit preflight stations.
            for (int i = 0; i < CargoStageAuthoring.EquipmentIds.Length; i++)
            {
                string id = CargoStageAuthoring.EquipmentIds[i];
                CheckCargoTarget(targets.Single(target => target.InteractionId == InteractionIds.CargoSupply(id)),
                    CargoStageAuthoring.SupplyPosition(i), player.transform);
                CheckCargoTarget(targets.Single(target => target.InteractionId == InteractionIds.CargoBay(id)),
                    CargoStageAuthoring.BayPosition(i), player.transform);
            }
            if (runtime.Gates.Count != 2 || !runtime.EvaRoute || !runtime.EvaRoute.IsValid || !runtime.EvaReentryPoint)
                throw new InvalidOperationException("The complete mission requires two gates, a valid EVA route and a reentry point.");
            if (runtime.Gates.Count(gate => gate.GateId == GateIds.AirlockInner) != 1
                || runtime.Gates.Count(gate => gate.GateId == GateIds.AirlockOuter) != 1)
                throw new InvalidOperationException("Expected one authoritative inner and one outer gameplay gate.");
            foreach (GameObject root in roots)
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                        throw new InvalidOperationException("Missing script on " + child.name);
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    if (renderer.sharedMaterials.Any(material => !material))
                        throw new InvalidOperationException("Missing material on " + renderer.name);
            }
            Debug.Log("I01_SCENE_VALIDATION_PASS cameras=1 listeners=1 runtimes=1 players=1 targets=22 unobstructedWithin2m=22 gates=2 | edit-time structure and physics queries only");
        }

        private static void AttachDeliveredModels(Scene scene, GameObject fallback, ShipVisualAnchors anchors)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (!asset) return; // The original functional checkpoint remains authorable without art delivery.
            if (asset.GetComponentsInChildren<Camera>(true).Length != 0
                || asset.GetComponentsInChildren<Light>(true).Length != 0
                || asset.GetComponentsInChildren<AudioListener>(true).Length != 0
                || asset.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                throw new InvalidOperationException("Model delivery must contain geometry and collision only.");

            // Keep the agreed anchor/light rig, replace its geometry and collision once. Never apply
            // these scene overrides to either team's source prefab.
            foreach (Renderer renderer in fallback.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            foreach (Collider collider in fallback.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            }
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
            model.name = "AURORA-7 delivered Blender models";
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            model.transform.localScale = Vector3.one;
            I05HullClosureBuilder.Build(model.transform,
                Required<Material>("Assets/_OutOfReach/Visuals/Models/Aurora7/Materials/A7_PaintWhite.mat"));
            // Paired play-mode captures show malformed geometry when these imported modules
            // are statically batched. Keep the source prefab intact and disable only this
            // optimization on the integrated scene instances; other static flags remain.
            int clearedBatchingFlags = 0;
            foreach (Transform node in model.GetComponentsInChildren<Transform>(true))
            {
                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(node.gameObject);
                if ((flags & StaticEditorFlags.BatchingStatic) == 0) continue;
                GameObjectUtility.SetStaticEditorFlags(node.gameObject, flags & ~StaticEditorFlags.BatchingStatic);
                PrefabUtility.RecordPrefabInstancePropertyModifications(node.gameObject);
                clearedBatchingFlags++;
            }
            Debug.Log("I02_STATIC_BATCHING_WORKAROUND sceneInstances=" + clearedBatchingFlags
                + " | paired play-mode renderer comparison; model source unchanged");
            Transform Leaf(string name) => model.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
            anchors.InnerDoorLeft = Leaf("DoorLeaf_Inner_L");
            anchors.InnerDoorRight = Leaf("DoorLeaf_Inner_R");
            anchors.OuterDoorLeft = Leaf("DoorLeaf_Outer_L");
            anchors.OuterDoorRight = Leaf("DoorLeaf_Outer_R");
            PrefabUtility.RecordPrefabInstancePropertyModifications(anchors);
            Debug.Log("I01_MODEL_DELIVERY_ATTACHED " + ModelPath + " | old meshes/colliders disabled; source prefabs unchanged");
        }

        private static void RequireVerificationProject()
        {
            if (Application.unityVersion != "6000.3.28f1") throw new InvalidOperationException("I01 requires Unity 6000.3.28f1.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (project.IndexOf("/.local/", StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException("Run the I01 builder only in the ignored .local verification project. Copy a reviewed Bootstrap back through the integrator.");
        }

        private static void ConfigureEnvironment()
        {
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/_OutOfReach/Visuals/Space/DistantStars.mat");
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.22f, .23f, .25f);
            RenderSettings.ambientIntensity = 1;
            RenderSettings.reflectionIntensity = .25f;
            RenderSettings.fog = false;
            // Authored solar direction/exposure, not a sampled Sun ephemeris or calibrated irradiance.
            // It is independent of ship power; exterior sunlight must not vanish with a cabin fault.
            var sun = new GameObject("Sunlight - authored direction").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(34f, -55f, 0f);
            sun.color = new Color(1f, .97f, .91f);
            sun.intensity = 1.05f;
            sun.shadows = LightShadows.Hard;
            sun.shadowStrength = 1f;
            RenderSettings.sun = sun;
        }

        private static void ConfigureCinematicGrade(Camera camera)
        {
            const string path = "Assets/_OutOfReach/Visuals/Ship/CinematicGrade.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "AURORA restrained film grade";
                AssetDatabase.CreateAsset(profile, path);
            }
            T Effect<T>() where T : VolumeComponent
            {
                if (profile.TryGet<T>(out T existing)) return existing;
                T effect = profile.Add<T>(true);
                AssetDatabase.AddObjectToAsset(effect, profile);
                return effect;
            }
            var tone = Effect<Tonemapping>();
            tone.mode.Override(TonemappingMode.ACES);
            var grade = Effect<ColorAdjustments>();
            grade.postExposure.Override(.45f);
            grade.contrast.Override(2f);
            grade.saturation.Override(-5f);
            var vignette = Effect<Vignette>();
            vignette.intensity.Override(.14f);
            vignette.smoothness.Override(.65f);
            var bloom = Effect<Bloom>();
            bloom.threshold.Override(1.25f);
            bloom.intensity.Override(.09f);
            foreach (VolumeComponent effect in profile.components) EditorUtility.SetDirty(effect);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Volume volume = new GameObject("Restrained cinematic grade").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;
            volume.sharedProfile = profile;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            cameraData.volumeLayerMask = 1; // Explicit Default-layer volumes, including pause focus.
        }

        private static T Required<T>(string path) where T : Object
        {
            T value = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!value) throw new InvalidOperationException("Missing required input: " + path);
            return value;
        }

        private static Transform Child(string name, Transform parent)
        {
            Transform child = new GameObject(name).transform;
            if (parent) child.SetParent(parent, false);
            return child;
        }

        private static InteractableTarget Target(Transform anchor, string id, Vector3 localOffset)
        {
            Transform child = Child("Gameplay target - " + id, anchor);
            child.localPosition = localOffset;
            var collider = child.gameObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(.9f, .8f, .25f);
            var target = child.gameObject.AddComponent<InteractableTarget>();
            target.InteractionId = id;
            return target;
        }

        private static GameplayGate Gate(Transform anchor, string id)
        {
            Transform child = Child("Gameplay gate - " + id, anchor);
            child.localPosition = new Vector3(0, 1.25f, 0);
            var blocker = child.gameObject.AddComponent<BoxCollider>();
            blocker.size = new Vector3(1.8f, 2.5f, .18f);
            var gate = child.gameObject.AddComponent<GameplayGate>();
            gate.Configure(id, blocker);
            return gate;
        }

        private static TetheredRoute CreateEvaRoute(ShipVisualAnchors anchors)
        {
            Transform root = Child("Tethered EVA route - outside hull", null);
            var route = root.gameObject.AddComponent<TetheredRoute>();
            Vector3[] positions = { anchors.EvaStart.position, new Vector3(0, 1.2f, 22.8f),
                new Vector3(3.8f, 1.2f, 22.8f), new Vector3(4.35f, .9f, 21.7f),
                new Vector3(4.35f, .9f, 14.35f), new Vector3(3.9f, .9f, 14.1f) };
            var points = new Transform[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                points[i] = Child("Tether station " + i, root);
                points[i].position = positions[i];
            }
            route.Configure(points, .25f, 1f);
            // Existing original material. This is a route guide, not a physical tether simulation.
            var guide = root.gameObject.AddComponent<LineRenderer>();
            guide.sharedMaterial = Required<Material>("Assets/_OutOfReach/Visuals/Ship/Materials/amber.mat");
            guide.useWorldSpace = true;
            guide.widthMultiplier = .022f;
            guide.positionCount = positions.Length;
            guide.SetPositions(positions.Select(point => point + Vector3.up * .75f).ToArray());
            guide.shadowCastingMode = ShadowCastingMode.Off;
            guide.receiveShadows = false;
            // The route is beyond the aft cap before moving outboard, then entirely beyond the radius-3 m hull.
            for (int segment = 1; segment < positions.Length; segment++)
                for (int sample = 0; sample <= 40; sample++)
                {
                    Vector3 feet = Vector3.Lerp(positions[segment - 1], positions[segment], sample / 40f);
                    if (feet.z < 21.6f && feet.x < 3.8f)
                        throw new InvalidOperationException("EVA route or its allowed drift would cross the delivered hull.");
                }
            return route;
        }

        private static void CreateShipLighting(GameObject fallback, Transform parent, out Light[] working, out Light[] emergency)
        {
            // Keep the existing anchor rig, but prevent its unbound old lights bypassing power-state presentation.
            foreach (Light light in fallback.GetComponentsInChildren<Light>(true))
            {
                light.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(light);
            }
            float[] stations = { -1.6f, 1.4f, 4.4f, 7.4f, 10.4f, 12.4f, 15.4f, 18.4f };
            working = new Light[stations.Length];
            emergency = new Light[stations.Length];
            for (int i = 0; i < stations.Length; i++)
            {
                Light light = PointLight("Working practical " + i, parent, new Vector3(0, 2.93f, stations[i]), 6.5f, 7.5f);
                light.type = LightType.Spot;
                light.transform.rotation = Quaternion.Euler(90, 0, 0);
                light.spotAngle = 130;
                light.innerSpotAngle = 88;
                light.color = new Color(1, .93f, .84f);
                light.shadows = i % 2 == 0 ? LightShadows.Soft : LightShadows.None;
                light.shadowStrength = .75f;
                working[i] = light;
                Light amber = PointLight("Steady emergency path " + i, parent, new Vector3(i % 2 == 0 ? -1.5f : 1.5f, .4f, stations[i]), 3.5f, 1.2f);
                amber.color = new Color(1, .38f, .07f);
                amber.enabled = false;
                emergency[i] = amber;
            }
        }

        private static Light PointLight(string name, Transform parent, Vector3 worldPosition, float range, float intensity)
        {
            Light light = Child(name, parent).gameObject.AddComponent<Light>();
            light.transform.position = worldPosition;
            light.type = LightType.Point;
            light.color = new Color(.65f, .78f, .85f);
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        private static CanvasGroup Overlay(string name, Transform parent, Color color)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            child.transform.SetParent(parent, false);
            var rect = (RectTransform)child.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image image = child.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            CanvasGroup group = child.GetComponent<CanvasGroup>();
            group.alpha = 0;
            group.blocksRaycasts = false;
            group.interactable = false;
            return group;
        }

        private static void SetObject(SerializedObject owner, string field, Object value)
        {
            SerializedProperty property = owner.FindProperty(field);
            if (property == null) throw new InvalidOperationException("Missing coordinated field: " + field);
            property.objectReferenceValue = value;
        }

        private static void BindApprovedDialogue(I01MissionComposition composition, MissionCinematicPresenter cinematics)
        {
            // Explicit user-approved clean masters only. Subtitle text is read verbatim from each matching TXT.
            var owner = new SerializedObject(composition);
            BindCue(owner, "recordedBriefing", "vo01_mc_briefing");
            AudioClip impact = BindCue(owner, "impactWarning", "vo02_ship_impact", "Dramatic/02_natural_crisis");
            BindCue(owner, "houstonDistress", "houston_to_earth", "Dramatic/05_houston_to_earth");
            BindCue(owner, "powerRestored", "vo03_ship_power_restored");
            BindCue(owner, "lifeSupportRestored", "vo04_ship_life_support_restored");
            BindCue(owner, "outerHatchOpen", "vo06_ship_eva_exit");
            BindCue(owner, "antennaRestored", "vo07_ship_antenna_restored");
            AudioClip diagnosis = BindCue(owner, "falseSignal", "vo09_ship_buffer_diagnosis");
            BindCue(owner, "transmissionSent", "vo10_ship_transmission_sent");
            AudioClip reply = BindCue(owner, "earthReply", "vo11_mc_reply_received", "Dramatic/03_exhaustion_relief");
            owner.ApplyModifiedPropertiesWithoutUndo();
            var cinematic = new SerializedObject(cinematics);
            cinematic.FindProperty("impactSeconds").floatValue = Mathf.Clamp(impact.length + .3f, 4.5f, 40f);
            cinematic.FindProperty("falseSignalSeconds").floatValue = Mathf.Clamp(diagnosis.length + .3f, 5f, 40f);
            cinematic.FindProperty("communicationRestoredSeconds").floatValue = Mathf.Clamp(reply.length + .3f, 4f, 40f);
            cinematic.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AudioClip BindCue(SerializedObject owner, string field, string id, string relativeVoicePath = null)
        {
            string path = "Assets/_OutOfReach/Audio/Voices/" + (relativeVoicePath ?? ("Clean/" + id));
            AudioClip clip = Required<AudioClip>(path + ".wav");
            TextAsset subtitle = Required<TextAsset>(path + ".txt");
            SerializedProperty cue = owner.FindProperty(field);
            if (cue == null) throw new InvalidOperationException("Missing approved dialogue field: " + field);
            cue.FindPropertyRelative("cueId").stringValue = id;
            cue.FindPropertyRelative("cleanClip").objectReferenceValue = clip;
            cue.FindPropertyRelative("subtitleText").stringValue = subtitle.text;
            return clip;
        }

        private static void SetArray(SerializedObject owner, string field, Object[] values)
        {
            SerializedProperty array = owner.FindProperty(field);
            if (array == null) throw new InvalidOperationException("Missing coordinated array field: " + field);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static T ExactlyOne<T>(GameObject[] roots) where T : Component
        {
            T[] found = roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            if (found.Length != 1) throw new InvalidOperationException("Expected one " + typeof(T).Name + ", found " + found.Length);
            return found[0];
        }

        private static void ValidateAnchors(ShipVisualAnchors anchors)
        {
            if (!anchors) throw new InvalidOperationException("Missing ShipVisualAnchors.");
            Transform[] values = { anchors.PlayerSpawn, anchors.CockpitConsole, anchors.PowerRepair, anchors.LifeSupportRepair,
                anchors.AirlockInner, anchors.AirlockOuter, anchors.EvaStart, anchors.AntennaRepair, anchors.EvaReturn, anchors.FinaleCameraAnchor };
            if (values.Any(value => !value) || values.Distinct().Count() != 10)
                throw new InvalidOperationException("Ship must supply ten distinct stable anchors.");
        }

        private static void CheckCargoTarget(InteractableTarget target, Vector3 expectedPosition, Transform playerRoot)
        {
            BoxCollider trigger = target.GetComponent<BoxCollider>();
            if (!target.gameObject.activeInHierarchy || !trigger || !trigger.enabled || !trigger.isTrigger
                || Vector3.Distance(trigger.size, new Vector3(.12f, .36f, .42f)) > .001f
                || Vector3.Distance(target.transform.position, expectedPosition) > .001f)
                throw new InvalidOperationException("Invalid preflight trigger or placement: " + target.InteractionId);
            CheckReach(target, new Vector3(0, 1.7f, expectedPosition.z), playerRoot);
        }

        private static void CheckReach(InteractableTarget target, Vector3 eye, Transform playerRoot)
        {
            Collider collider = target.GetComponent<Collider>();
            if (!collider || !collider.isTrigger) throw new InvalidOperationException("Gameplay target must have its own trigger collider.");
            Vector3 direction = (collider.bounds.center - eye).normalized;
            RaycastHit[] hits = Physics.RaycastAll(eye, direction, 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(playerRoot)) continue;
                var hitTarget = hit.collider.GetComponentInParent<InteractableTarget>();
                if (hitTarget == target) return;
                if (hitTarget || !hit.collider.isTrigger)
                    throw new InvalidOperationException("Interaction " + target.InteractionId + " is blocked by " + hit.collider.name);
            }
            throw new InvalidOperationException("Interaction is not reachable within 2 m: " + target.InteractionId);
        }
    }
}
