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
            if (!presentationParent || !playerFeet || !playerCamera || EditorUtility.IsPersistent(presentationParent)
                || EditorUtility.IsPersistent(playerFeet) || EditorUtility.IsPersistent(playerCamera))
                throw new ArgumentException("Supply existing scene presentation parent, sole player feet and camera.");
            if (presentationParent.IsChildOf(playerCamera.transform) || presentationParent == playerCamera.transform)
                throw new ArgumentException("Body parent must not inherit camera pitch or visual bob.");
            if (presentationParent.Find(HostName) || presentationParent.Find(VisualName))
                throw new InvalidOperationException("Body scope already exists; root must explicitly remove/rebuild its owned instance.");
            PrepareAssets();
            var host = new GameObject(HostName); host.transform.SetParent(presentationParent, false);
            var visuals = new GameObject(VisualName); visuals.transform.SetParent(presentationParent, false);
            try
            {
                var work = CreateOutfit(WorkPath, "WORK", visuals.transform);
                var eva = CreateOutfit(EvaPath, "EVA", visuals.transform);
                var presenter = host.AddComponent<EmbodiedBodyPresenter>();
                presenter.ConfigureScene(visuals.transform, playerFeet, playerCamera, work, eva, new Vector3(0f, 0f, -.330f));
                // Centered, source-measured V04 neutral pose: the torso/legs fit the
                // actual capsule and relaxed hands start behind a nearby wall.
                // Standing uses the reviewed unit-correct V02 fit; seated retains its original setback.
                presenter.SetVisualFeetOffset(EmbodiedBodyPresenter.BodyPose.Standing, new Vector3(0f, 0f, -.197f));
                presenter.SetVisualFeetOffset(EmbodiedBodyPresenter.BodyPose.Floating, Vector3.zero);
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

        private static EmbodiedBodyPresenter.Outfit CreateOutfit(string path, string variant, Transform parent)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!asset) throw new InvalidOperationException("Missing imported body " + path);
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            model.name = "Body " + variant + " - Generic held poses";
            // Preserve the imported model's local bind/axis transforms. Only the outer visual wrapper follows feet.
            foreach (Transform node in model.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 2;
            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true))
            {
                animator.runtimeAnimatorController = null; animator.applyRootMotion = false;
                animator.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            }
            SkinnedMeshRenderer[] skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (SkinnedMeshRenderer skin in skins)
            {
                Material[] mapped = skin.sharedMaterials.Select(m => ResolveMaterial(m ? m.name : string.Empty)).ToArray();
                skin.sharedMaterials = mapped; skin.enabled = false; skin.updateWhenOffscreen = true;
                skin.shadowCastingMode = ShadowCastingMode.Off; skin.receiveShadows = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(skin);
            }
            return new EmbodiedBodyPresenter.Outfit
            {
                modelRoot = model.transform,
                torsoLegs = ExactlyOne(skins, "A7_" + variant + "_TorsoLegs"),
                leftArm = ExactlyOne(skins, "A7_" + variant + "_Arm_L"),
                rightArm = ExactlyOne(skins, "A7_" + variant + "_Arm_R"),
                standing = Clip(StandingPath, "BODY_StandIdle_V02"), floating = Clip(FloatPath, "BODY_FloatNeutral_V04"), seated = Clip(path, PoseNames[2])
            };
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
