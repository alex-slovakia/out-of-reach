using System;
using System.Linq;
using OutOfReach.Presentation.Motion;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace OutOfReach.Composition.Editor
{
    /// <summary>Explicit root-invoked body authoring. Never opens/saves a scene or edits existing glove assets.</summary>
    public static class I06BodyAuthoring
    {
        public const string AssetRoot = "Assets/_OutOfReach/Visuals/Character/FirstPersonBody/FitV02";
        public const string HostName = "I06 Embodied body presenter";
        public const string VisualName = "I06 Embodied body visuals";
        public const string WorkPath = AssetRoot + "/Meshes/A7_BodyFit02_WORK.fbx";
        public const string EvaPath = AssetRoot + "/Meshes/A7_BodyFit02_EVA.fbx";
        public const string FloatPath = "Assets/_OutOfReach/Visuals/Character/FirstPersonBody/FloatNeutralV04/Animations/BODY_FloatNeutral_V04.fbx";
        public const string StandingPath = "Assets/_OutOfReach/Visuals/Character/FirstPersonBody/StandingIdleV02/Animations/BODY_StandIdle_V02.fbx";
        public const string CompatibilityProfilePath = AssetRoot + "/Profiles/A7FitV02.asset";
        public const string SelectionPath = AssetRoot + "/Profiles/AvatarSelection.asset";
        private const string NormalPath = AssetRoot + "/Textures/Body_Weave_Normal.png";
        private static readonly string[] PoseNames = { "BODY_StandRelaxed", "BODY_FloatNeutral", "BODY_SeatedFit" };

        /// <summary>Invoke after copying the reviewed files from import-manifest.json into the isolated project.</summary>
        public static void PrepareAssets()
        {
            RequireEditMode();
            ConfigureModel(WorkPath, true); ConfigureModel(EvaPath, false);
            ConfigureFloatingClip();
            ConfigureStandingClip();
            var normal = AssetImporter.GetAtPath(NormalPath) as TextureImporter;
            if (normal == null) throw new InvalidOperationException("Copy the reviewed body weave normal before authoring.");
            if (normal.textureType != TextureImporterType.NormalMap || normal.sRGBTexture
                || normal.wrapMode != TextureWrapMode.Repeat || normal.maxTextureSize != 2048)
            {
                normal.textureType = TextureImporterType.NormalMap; normal.sRGBTexture = false;
                normal.wrapMode = TextureWrapMode.Repeat; normal.maxTextureSize = 2048; normal.SaveAndReimport();
            }
            EnsureFolder(AssetRoot + "/Materials");
            foreach (MaterialSpec spec in Specs) CreateMaterial(spec);
        }

        /// <summary>Creates two new passive outfit instances. Root owns scene save, session binding and removal.</summary>
        public static EmbodiedBodyPresenter Create(Transform presentationParent, Transform playerFeet, Camera playerCamera)
        {
            RequireEditMode();
            return Create(presentationParent, playerFeet, playerCamera, GetOrCreateSelection().Profile);
        }

        /// <summary>Explicit profile overload. Never changes model import settings or the selected profile.</summary>
        public static EmbodiedBodyPresenter Create(Transform presentationParent, Transform playerFeet, Camera playerCamera,
            AvatarPresentationProfile profile)
        {
            RequireEditMode();
            if (!presentationParent || !playerFeet || !playerCamera || EditorUtility.IsPersistent(presentationParent)
                || EditorUtility.IsPersistent(playerFeet) || EditorUtility.IsPersistent(playerCamera))
                throw new ArgumentException("Supply existing scene presentation parent, sole player feet and camera.");
            if (presentationParent.IsChildOf(playerCamera.transform) || presentationParent == playerCamera.transform)
                throw new ArgumentException("Body parent must not inherit camera pitch or visual bob.");
            if (presentationParent.Find(HostName) || presentationParent.Find(VisualName))
                throw new InvalidOperationException("Body scope already exists; root must explicitly remove/rebuild its owned instance.");
            AvatarPresentationProfileValidator.Validate(profile);
            var host = new GameObject(HostName); host.transform.SetParent(presentationParent, false);
            var visuals = new GameObject(VisualName); visuals.transform.SetParent(presentationParent, false);
            try
            {
                var work = CreateOutfit(profile.Work, "WORK", visuals.transform);
                var eva = CreateOutfit(profile.Eva, "EVA", visuals.transform);
                var presenter = host.AddComponent<EmbodiedBodyPresenter>();
                presenter.ConfigureScene(visuals.transform, playerFeet, playerCamera, work, eva, profile.SeatedOffset);
                presenter.SetVisualFeetOffset(EmbodiedBodyPresenter.BodyPose.Standing, profile.StandingOffset);
                presenter.SetVisualFeetOffset(EmbodiedBodyPresenter.BodyPose.Floating, profile.FloatingOffset);
                ValidateScene(presenter);
                EditorUtility.SetDirty(presenter);
                return presenter;
            }
            catch
            {
                Object.DestroyImmediate(host); Object.DestroyImmediate(visuals); throw;
            }
        }

        public static void ValidateScene(EmbodiedBodyPresenter presenter)
        {
            if (!presenter || !presenter.VisualRoot || !presenter.PlayerFeet || !presenter.PlayerCamera)
                throw new InvalidOperationException("Body scene references are incomplete.");
            Transform root = presenter.VisualRoot;
            if (root.GetComponentsInChildren<Camera>(true).Length != 0
                || root.GetComponentsInChildren<AudioListener>(true).Length != 0
                || root.GetComponentsInChildren<Collider>(true).Length != 0
                || root.GetComponentsInChildren<Rigidbody>(true).Length != 0
                || root.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                throw new InvalidOperationException("Body visuals contain an unexpected component or authority.");
            SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length != 6 || renderers.Any(r => !r.sharedMesh || !r.rootBone || r.bones.Any(b => !b)
                || r.sharedMaterials.Any(m => !m) || r.enabled))
                throw new InvalidOperationException("Expected six complete, initially hidden body skins.");
            if (!presenter.WorkLeftWrist || !presenter.WorkRightWrist || !presenter.WorkLeftUpperArm || !presenter.WorkRightUpperArm)
                throw new InvalidOperationException("Imported WORK contact joints were not cached.");
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
                if (animator.enabled || animator.runtimeAnimatorController || animator.applyRootMotion)
                    throw new InvalidOperationException("Automatic body Animator writing must be disabled.");
        }

        /// <summary>Root executeMethod: create/validate profile assets before starting any read-only scene hash test.</summary>
        [MenuItem("Out of Reach/Avatar/Initialize compatibility profile assets")]
        public static void InitializeAvatarProfiles()
        {
            RequireEditMode();
            AvatarPresentationProfile compatibility = GetOrCreateCompatibilityProfile();
            AvatarPresentationProfileValidator.Validate(compatibility);
            AvatarPresentationSelection selected = GetOrCreateSelection();
            AvatarPresentationProfileValidator.Validate(selected.Profile);
            Debug.Log("Avatar profile assets ready. Existing scene instances were not changed. Selection: " + SelectionPath, selected);
        }

        /// <summary>Root executeMethod: add mappings to matching existing instances, without replacing any scene reference.</summary>
        [MenuItem("Out of Reach/Avatar/Bind selected profile to existing matching body")]
        public static void BindSelectedProfileToExistingBody()
        {
            RequireEditMode();
            var selected = AssetDatabase.LoadAssetAtPath<AvatarPresentationSelection>(SelectionPath);
            if (!selected || !selected.Profile) throw new InvalidOperationException("Initialize and choose the avatar selection asset first.");
            var bodies = Object.FindObjectsByType<EmbodiedBodyPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(b => b.gameObject.scene.IsValid() && b.gameObject.scene.isLoaded && !EditorUtility.IsPersistent(b)).ToArray();
            if (bodies.Length != 1) throw new InvalidOperationException("Load exactly one root-owned scene body before adding its profile bindings.");
            BindProfileToExistingBody(bodies[0], selected.Profile);
        }

        public static void BindProfileToExistingBody(EmbodiedBodyPresenter presenter, AvatarPresentationProfile profile)
        {
            RequireEditMode();
            if (!presenter || EditorUtility.IsPersistent(presenter) || presenter.IsBound)
                throw new InvalidOperationException("Bind only an existing, unbound Edit-mode scene presenter.");
            AvatarPresentationProfileValidator.Validate(profile);
            var serialized = new SerializedObject(presenter);
            var work = MatchExistingOutfit(serialized.FindProperty("work"), profile.Work, true);
            var eva = MatchExistingOutfit(serialized.FindProperty("eva"), profile.Eva, false);
            if ((presenter.GetVisualFeetOffset(EmbodiedBodyPresenter.BodyPose.Standing) - profile.StandingOffset).sqrMagnitude > 1e-10f
                || (presenter.GetVisualFeetOffset(EmbodiedBodyPresenter.BodyPose.Floating) - profile.FloatingOffset).sqrMagnitude > 1e-10f
                || (presenter.GetVisualFeetOffset(EmbodiedBodyPresenter.BodyPose.Seated) - profile.SeatedOffset).sqrMagnitude > 1e-10f)
                throw new InvalidOperationException("Existing visual fit differs from profile. Reconcile it explicitly; mapping-only activation does not change fit.");
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Bind existing avatar profile mappings");
            Undo.RecordObjects(new Object[] { presenter, work.torsoLegs, work.leftArm, work.rightArm,
                eva.torsoLegs, eva.leftArm, eva.rightArm }, "Bind existing avatar profile mappings");
            try
            {
                presenter.ConfigureScene(presenter.VisualRoot, presenter.PlayerFeet, presenter.PlayerCamera,
                    work, eva, profile.SeatedOffset);
                presenter.SetVisualFeetOffset(EmbodiedBodyPresenter.BodyPose.Standing, profile.StandingOffset);
                presenter.SetVisualFeetOffset(EmbodiedBodyPresenter.BodyPose.Floating, profile.FloatingOffset);
                ValidateScene(presenter);
                EditorUtility.SetDirty(presenter);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(presenter.gameObject.scene);
                Undo.CollapseUndoOperations(group);
                Debug.Log("Existing avatar mappings bound with all scene object references preserved. Root must explicitly save and run native acceptance.", presenter);
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        private static EmbodiedBodyPresenter.Outfit MatchExistingOutfit(SerializedProperty old,
            AvatarPresentationProfile.OutfitDefinition definition, bool bareHands)
        {
            Transform instance = old?.FindPropertyRelative("modelRoot").objectReferenceValue as Transform;
            if (!instance || PrefabUtility.GetCorrespondingObjectFromOriginalSource(instance.gameObject) != definition.model)
                throw new InvalidOperationException("Mapping-only activation requires the same original model asset. A different model needs an explicit root-owned body rebuild.");
            var resolved = AvatarPresentationResolver.Resolve(definition, instance, bareHands);
            if (old.FindPropertyRelative("torsoLegs").objectReferenceValue != resolved.torsoLegs
                || old.FindPropertyRelative("leftArm").objectReferenceValue != resolved.leftArm
                || old.FindPropertyRelative("rightArm").objectReferenceValue != resolved.rightArm
                || old.FindPropertyRelative("standing").objectReferenceValue != resolved.standing
                || old.FindPropertyRelative("floating").objectReferenceValue != resolved.floating
                || old.FindPropertyRelative("seated").objectReferenceValue != resolved.seated)
                throw new InvalidOperationException("Mapping-only activation cannot replace existing skin or animation references.");
            MatchExistingSkin(resolved.torsoLegs, definition.torsoLegs, definition.model.transform);
            MatchExistingSkin(resolved.leftArm, definition.leftArm, definition.model.transform);
            MatchExistingSkin(resolved.rightArm, definition.rightArm, definition.model.transform);
            return resolved;
        }

        private static void MatchExistingSkin(SkinnedMeshRenderer skin, AvatarPresentationProfile.SkinRole role, Transform source)
        {
            var expected = EmbodiedBodyPresenter.ResolveModelPath(source, role.path).GetComponent<SkinnedMeshRenderer>();
            if (!expected || skin.sharedMesh != expected.sharedMesh || !skin.sharedMaterials.SequenceEqual(role.materials))
                throw new InvalidOperationException("Mapping-only activation cannot replace existing mesh or material references.");
        }

        public static AvatarPresentationSelection GetOrCreateSelection()
        {
            RequireEditMode();
            var selected = AssetDatabase.LoadAssetAtPath<AvatarPresentationSelection>(SelectionPath);
            if (selected)
            {
                if (!selected.Profile) throw new InvalidOperationException("Choose an explicit avatar profile in " + SelectionPath);
                return selected;
            }
            AvatarPresentationProfile compatibility = GetOrCreateCompatibilityProfile();
            selected = ScriptableObject.CreateInstance<AvatarPresentationSelection>();
            selected.SetInitialProfile(compatibility);
            AssetDatabase.CreateAsset(selected, SelectionPath);
            AssetDatabase.SaveAssetIfDirty(selected);
            return selected;
        }

        /// <summary>Generate once from the current reviewed assets; never overwrite a user's profile mapping.</summary>
        public static AvatarPresentationProfile GetOrCreateCompatibilityProfile()
        {
            RequireEditMode();
            var existing = AssetDatabase.LoadAssetAtPath<AvatarPresentationProfile>(CompatibilityProfilePath);
            if (existing) return existing;
            PrepareAssets();
            EnsureFolder(AssetRoot + "/Profiles");
            var profile = ScriptableObject.CreateInstance<AvatarPresentationProfile>();
            try
            {
                FillCompatibility(profile.Work, WorkPath, "WORK"); FillCompatibility(profile.Eva, EvaPath, "EVA");
                AvatarPresentationProfileValidator.Validate(profile);
                AssetDatabase.CreateAsset(profile, CompatibilityProfilePath);
                AssetDatabase.SaveAssetIfDirty(profile);
                return profile;
            }
            catch { if (!EditorUtility.IsPersistent(profile)) Object.DestroyImmediate(profile); throw; }
        }

        private static void FillCompatibility(AvatarPresentationProfile.OutfitDefinition definition, string path, string variant)
        {
            definition.model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!definition.model) throw new InvalidOperationException("Missing imported compatibility body " + path);
            Transform root = definition.model.transform;
            Transform[] nodes = root.GetComponentsInChildren<Transform>(true);
            if (nodes.GroupBy(t => t.name, StringComparer.Ordinal).Any(g => g.Count() != 1))
                throw new InvalidOperationException("Compatibility generator expects the original unique-name rig; manually map a replacement profile.");
            definition.bones = nodes.Select(t => new EmbodiedBodyPresenter.SemanticBinding(t.name,
                AnimationUtility.CalculateTransformPath(t, root))).ToArray();
            SkinnedMeshRenderer[] skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            FillRole(definition.torsoLegs, ExactlyOne(skins, "A7_" + variant + "_TorsoLegs"), root);
            FillRole(definition.leftArm, ExactlyOne(skins, "A7_" + variant + "_Arm_L"), root);
            FillRole(definition.rightArm, ExactlyOne(skins, "A7_" + variant + "_Arm_R"), root);
            definition.standing = Clip(StandingPath, "BODY_StandIdle_V02");
            definition.floating = Clip(FloatPath, "BODY_FloatNeutral_V04"); definition.seated = Clip(path, PoseNames[2]);
            var curves = new[] { definition.standing, definition.floating, definition.seated }
                .SelectMany(AnimationUtility.GetCurveBindings).ToArray();
            definition.translationPaths = curves.Where(c => c.propertyName.StartsWith("m_LocalPosition.", StringComparison.Ordinal))
                .Select(c => c.path).Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            definition.scalePaths = curves.Where(c => c.propertyName.StartsWith("m_LocalScale.", StringComparison.Ordinal))
                .Select(c => c.path).Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        }

        private static void FillRole(AvatarPresentationProfile.SkinRole role, SkinnedMeshRenderer skin, Transform root)
        {
            role.path = AnimationUtility.CalculateTransformPath(skin.transform, root);
            role.materials = skin.sharedMaterials.Select(m => ResolveMaterial(m ? m.name : string.Empty)).ToArray();
            // One-time compatibility mapping only. Replacement profiles choose indices explicitly, independent of names.
            role.palmSubmeshes = role.materials.Select((m, i) => new { Material = m, Index = i })
                .Where(x => x.Material.name == "MAT_Cabin_BareHands").Select(x => x.Index).ToArray();
        }

        private static EmbodiedBodyPresenter.Outfit CreateOutfit(AvatarPresentationProfile.OutfitDefinition definition,
            string variant, Transform parent)
        {
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(definition.model, parent);
            model.name = "Body " + variant + " - Generic held poses";
            // Preserve imported bind/axis transforms; only the outer wrapper follows authoritative feet.
            foreach (Transform node in model.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 2;
            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true))
            {
                animator.runtimeAnimatorController = null; animator.applyRootMotion = false;
                animator.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            }
            var outfit = AvatarPresentationResolver.Resolve(definition, model.transform, variant == "WORK");
            ConfigureSkin(outfit.torsoLegs, definition.torsoLegs); ConfigureSkin(outfit.leftArm, definition.leftArm);
            ConfigureSkin(outfit.rightArm, definition.rightArm);
            return outfit;
        }

        private static void ConfigureSkin(SkinnedMeshRenderer skin, AvatarPresentationProfile.SkinRole role)
        {
            skin.sharedMaterials = (Material[])role.materials.Clone(); skin.enabled = false; skin.updateWhenOffscreen = true;
            skin.shadowCastingMode = ShadowCastingMode.Off; skin.receiveShadows = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(skin);
        }

        private static void ConfigureStandingClip()
        {
            var importer = AssetImporter.GetAtPath(StandingPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Copy the reviewed unit-correct V02 standing clip first.");
            if (Mathf.Abs(importer.fileScale - 1f) > .000001f)
                throw new InvalidOperationException("Standing V02 must declare metre FBX units; do not compensate a centimetre clip with importer scale.");
            if (importer.animationType != ModelImporterAnimationType.Generic || !importer.importAnimation
                || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel
                || importer.optimizeGameObjects || importer.animationCompression != ModelImporterAnimationCompression.Off
                || importer.globalScale != 1f || !importer.useFileScale || importer.addCollider || !importer.preserveHierarchy)
            {
                importer.animationType = ModelImporterAnimationType.Generic; importer.importAnimation = true;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.optimizeGameObjects = false; importer.animationCompression = ModelImporterAnimationCompression.Off;
                importer.globalScale = 1f; importer.useFileScale = true; importer.addCollider = false;
                importer.preserveHierarchy = true; importer.SaveAndReimport();
            }
            var takes = importer.defaultClipAnimations;
            if (takes.Length != 1 || !takes[0].takeName.EndsWith("BODY_StandIdle_V02", StringComparison.Ordinal))
                throw new InvalidOperationException("Standing V02 requires its single reviewed original take and frame bounds.");
            if (importer.clipAnimations.Length != 1 || importer.clipAnimations[0].name != "BODY_StandIdle_V02")
            {
                takes[0].name = "BODY_StandIdle_V02"; takes[0].loopTime = true; takes[0].loopPose = false;
                importer.clipAnimations = takes; importer.SaveAndReimport();
            }
            Clip(StandingPath, "BODY_StandIdle_V02");
        }

        private static void ConfigureFloatingClip()
        {
            var importer = AssetImporter.GetAtPath(FloatPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Copy the reviewed rest-bind V04 floating clip first.");
            if (importer.animationType != ModelImporterAnimationType.Generic || !importer.importAnimation
                || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel
                || importer.optimizeGameObjects || importer.animationCompression != ModelImporterAnimationCompression.Off
                || importer.globalScale != 1f || !importer.useFileScale || importer.addCollider || !importer.preserveHierarchy)
            {
                importer.animationType = ModelImporterAnimationType.Generic; importer.importAnimation = true;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.optimizeGameObjects = false; importer.animationCompression = ModelImporterAnimationCompression.Off;
                importer.globalScale = 1f; importer.useFileScale = true; importer.addCollider = false;
                // Animation-only FBX must retain the same named rig parent as the multi-mesh outfits.
                importer.preserveHierarchy = true;
                importer.SaveAndReimport();
            }
            var takes = importer.defaultClipAnimations;
            if (takes.Length != 1 || !takes[0].takeName.EndsWith("BODY_FloatNeutral", StringComparison.Ordinal))
                throw new InvalidOperationException("V04 requires its single original floating take and frame bounds.");
            if (importer.clipAnimations.Length != 1 || importer.clipAnimations[0].name != "BODY_FloatNeutral_V04")
            {
                takes[0].name = "BODY_FloatNeutral_V04"; takes[0].loopTime = true; takes[0].loopPose = false;
                importer.clipAnimations = takes; importer.SaveAndReimport();
            }
            Clip(FloatPath, "BODY_FloatNeutral_V04");
        }

        private static void ConfigureModel(string path, bool readable)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Copy reviewed FBX before body authoring: " + path);
            bool changed = importer.animationType != ModelImporterAnimationType.Generic || !importer.importAnimation
                || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel || importer.optimizeGameObjects
                || importer.animationCompression != ModelImporterAnimationCompression.Off || importer.addCollider
                || importer.isReadable != readable || importer.globalScale != 1f || !importer.useFileScale
                || importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard
                || importer.skinWeights != ModelImporterSkinWeights.Custom || importer.maxBonesPerVertex != 4;
            if (changed)
            {
                importer.animationType = ModelImporterAnimationType.Generic; importer.importAnimation = true;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.optimizeGameObjects = false; importer.animationCompression = ModelImporterAnimationCompression.Off;
                importer.addCollider = false; importer.isReadable = readable; importer.globalScale = 1f; importer.useFileScale = true;
                // Import source material slot names as subassets, then explicitly replace them with owned URP materials.
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.skinWeights = ModelImporterSkinWeights.Custom; importer.maxBonesPerVertex = 4;
                importer.SaveAndReimport();
            }
            ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
            if (takes.Length != 3 || PoseNames.Any(n => takes.Count(t => t.takeName.EndsWith(n, StringComparison.Ordinal)) != 1))
                throw new InvalidOperationException("Reviewed body must expose the exact three baked Generic takes: " + path);
            if (importer.clipAnimations.Length != 3 || PoseNames.Any(n => importer.clipAnimations.Count(c => c.name == n) != 1))
            {
                foreach (ModelImporterClipAnimation take in takes)
                {
                    take.name = PoseNames.Single(n => take.takeName.EndsWith(n, StringComparison.Ordinal));
                    take.loopTime = true; take.loopPose = false;
                }
                importer.clipAnimations = takes; importer.SaveAndReimport();
            }
            foreach (string pose in PoseNames) Clip(path, pose);
        }

        private static AnimationClip Clip(string path, string name)
        {
            AnimationClip[] matches = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Where(c => c.name == name && !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1 || matches[0].legacy || matches[0].humanMotion || matches[0].length <= 0f)
                throw new InvalidOperationException("Missing stable Generic body clip " + name + " in " + path);
            return matches[0];
        }
        private static SkinnedMeshRenderer ExactlyOne(SkinnedMeshRenderer[] skins, string name)
        {
            SkinnedMeshRenderer[] matches = skins.Where(s => s.name == name).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("Missing or duplicate reviewed body renderer " + name);
            return matches[0];
        }

        private readonly struct MaterialSpec
        {
            public readonly string Name, Hex;
            public readonly float Roughness, Metallic;
            public readonly bool Cloth;
            public MaterialSpec(string name, string hex, float roughness, float metallic = 0f, bool cloth = false)
            { Name = name; Hex = hex; Roughness = roughness; Metallic = metallic; Cloth = cloth; }
        }
        private static readonly MaterialSpec[] Specs =
        {
            new MaterialSpec("MAT_Workwear_Graphite", "#384650", .83f, 0f, true),
            new MaterialSpec("MAT_Workwear_Cordura", "#303B44", .86f, 0f, true),
            new MaterialSpec("MAT_EVA_WarmIvory", "#D5D3CB", .77f, 0f, true),
            new MaterialSpec("MAT_EVA_FlexGrey", "#6A7379", .84f, 0f, true),
            new MaterialSpec("MAT_Webbing", "#2B2B2C", .86f, 0f, true),
            new MaterialSpec("MAT_Boot_Rubber", "#22272C", .9f),
            new MaterialSpec("MAT_Boot_Reinforcement", "#424B51", .74f),
            new MaterialSpec("MAT_BrushedTitanium", "#919CA3", .39f, .8f),
            new MaterialSpec("MAT_AmberThread", "#D9AA55", .8f),
            new MaterialSpec("MAT_SafetyRed", "#B32A22", .74f),
            new MaterialSpec("MAT_Cabin_BareHands", "#A67A64", .65f)
        };
        private static void CreateMaterial(MaterialSpec spec)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("Existing URP Lit shader is required.");
            string path = AssetRoot + "/Materials/" + spec.Name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader) { name = spec.Name }; AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            if (!ColorUtility.TryParseHtmlString(spec.Hex, out Color color)) throw new InvalidOperationException("Invalid body color.");
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", spec.Metallic);
            material.SetFloat("_Smoothness", 1f - spec.Roughness); material.SetFloat("_Surface", 0f);
            material.SetFloat("_AlphaClip", 0f); material.SetFloat("_Cull", (float)CullMode.Back);
            material.SetFloat("_ZWrite", 1f); material.SetTexture("_BaseMap", null);
            material.SetTexture("_MetallicGlossMap", null); material.DisableKeyword("_METALLICSPECGLOSSMAP");
            material.SetColor("_EmissionColor", Color.black); material.DisableKeyword("_EMISSION");
            material.SetTextureScale("_BaseMap", spec.Cloth ? new Vector2(14, 14) : Vector2.one);
            material.SetTexture("_BumpMap", spec.Cloth ? AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath) : null);
            material.SetFloat("_BumpScale", .18f);
            if (spec.Cloth) material.EnableKeyword("_NORMALMAP"); else material.DisableKeyword("_NORMALMAP");
            material.renderQueue = (int)RenderQueue.Geometry; material.SetOverrideTag("RenderType", "Opaque");
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
        }
        private static Material ResolveMaterial(string importedName)
        {
            string key = importedName.EndsWith(".001", StringComparison.Ordinal) ? importedName.Substring(0, importedName.Length - 4) : importedName;
            if (!Specs.Any(s => s.Name == key)) throw new InvalidOperationException("Unexpected reviewed body material slot: " + importedName);
            Material result = AssetDatabase.LoadAssetAtPath<Material>(AssetRoot + "/Materials/" + key + ".mat");
            if (!result) throw new InvalidOperationException("Prepare body materials before creating the scene view.");
            return result;
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/'); string parent = path.Substring(0, split);
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(split + 1));
        }
        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Author body assets/instances outside Play mode; root owns scene save.");
        }
    }
}
