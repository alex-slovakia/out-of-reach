using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace OutOfReach.Composition.Editor
{
    public static class I06LandTerrainAuthoring
    {
        private const string AssetsRoot = "Assets/_OutOfReach/Visuals/Models/LandTerrainV01";
        private const string GroupName = "Coastal relief and launch pad detail";

        public static void ApplyToSavedBootstrap()
        {
            if (Application.unityVersion != "6000.3.28f1" || !Application.dataPath.Replace('\\', '/').Contains("/.local/"))
                throw new InvalidOperationException("Use the isolated Unity 6000.3.28f1 checkout.");
            var scene = EditorSceneManager.OpenScene(I01BootstrapBuilder.ScenePath, OpenSceneMode.Single);
            var marine = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<I06MarineBindings>(true)).Single();
            Transform site = marine.MarineRoot.Find("Launch site - stationary");
            if (!site) throw new InvalidOperationException("The authored land site root was not found.");
            Transform pad = site.GetComponentsInChildren<Transform>(true).Single(node => node.name == "Land pad top plane");
            Attach(site, pad);
            I01BootstrapBuilder.ValidateScene(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("The launch site could not be saved.");
        }

        private static void Attach(Transform site, Transform pad)
        {
            var previousGround = site.Find(I06LandHorizonAuthoring.ObjectName);
            var previousSurroundings = site.Find(I06LandSurroundingsAuthoring.InstanceName);
            if (!previousGround || !previousSurroundings)
                throw new InvalidOperationException("Expected the already-authored prior ground and props.");

            var terrain = Import("LT_LandTerrain", 63824);
            var surroundings = Import("LT_Surroundings_v02", 29088);
            string materialFolder = AssetsRoot + "/Materials";
            if (!AssetDatabase.IsValidFolder(materialFolder)) AssetDatabase.CreateFolder(AssetsRoot, "Materials");
            var terrainMaterial = GetMaterial(materialFolder, "LT_Terrain", Color.white);
            terrainMaterial.SetTexture("_BaseMap", Texture("LT_Terrain_Macro"));
            terrainMaterial.SetTexture("_DetailAlbedoMap", Texture("LT_TerrainDetail_Albedo"));
            terrainMaterial.SetTexture("_DetailNormalMap", Texture("LT_TerrainDetail_Normal", TextureImporterType.NormalMap));
            terrainMaterial.SetTextureScale("_DetailAlbedoMap", new Vector2(1280f, 1280f));
            terrainMaterial.SetFloat("_DetailAlbedoMapScale", 0.52f);
            terrainMaterial.SetFloat("_DetailNormalMapScale", 0.28f);
            terrainMaterial.EnableKeyword("_DETAIL_SCALED");

            var materialByName = new Dictionary<string, Material>(StringComparer.Ordinal)
            {
                { "LT_Terrain", terrainMaterial },
                { "LT_TerrainFar", terrainMaterial }
            };
            var sourceMaterialFolders = new[]
            {
                I06LandSurroundingsAuthoring.AssetRoot,
                I06LandSiteAuthoring.AssetRoot,
                "Assets/_OutOfReach/Visuals/Models/Aurora7/Materials"
            };
            foreach (string assetGuid in AssetDatabase.FindAssets("t:Material", sourceMaterialFolders))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(assetGuid));
                if (material && !materialByName.ContainsKey(material.name)) materialByName.Add(material.name, material);
            }
            var rock = GetMaterial(materialFolder, "LT_Rock", new Color(.54f, .51f, .46f));
            if (materialByName.TryGetValue("LS_Concrete", out Material concrete))
            {
                rock.SetTexture("_BaseMap", concrete.GetTexture("_BaseMap"));
                rock.SetTexture("_BumpMap", concrete.GetTexture("_BumpMap"));
                rock.EnableKeyword("_NORMALMAP");
            }
            materialByName["LT_Rock"] = rock;
            materialByName["LT_Rocks"] = rock;
            var finish = GetMaterial(materialFolder, "Pad expansion joint", new Color(.145f, .16f, .15f));
            var paint = GetMaterial(materialFolder, "Pad hazard ochre", new Color(.77f, .50f, .12f));
            var grate = GetMaterial(materialFolder, "Pad drain galvanized", new Color(.27f, .29f, .29f));

            Transform group = site.Find(GroupName);
            if (group) UnityEngine.Object.DestroyImmediate(group.gameObject);
            group = new GameObject(GroupName).transform;
            group.SetParent(site, false);
            AttachModel(terrain, group, materialByName);
            AttachModel(surroundings, group, materialByName);
            AuthorPadDetails(group, site, pad, finish, paint, grate);
            previousGround.gameObject.SetActive(false);
            previousSurroundings.gameObject.SetActive(false);
            Debug.Log("I06_TERRAIN_APPLIED 63,824 terrain + 29,088 detail triangles, original terrain materials, pad markings on actual saved foundation");
        }

        private static GameObject Import(string name, int expectedTriangles)
        {
            string path = AssetsRoot + "/" + name + ".fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (!importer) throw new InvalidOperationException("Missing " + path);
            if (!importer.bakeAxisConversion || importer.importCameras || importer.importLights || importer.addCollider)
            {
                importer.bakeAxisConversion = true;
                importer.importCameras = false;
                importer.importLights = false;
                importer.addCollider = false;
                importer.SaveAndReimport();
            }
            var mesh = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!mesh || mesh.GetComponentsInChildren<MeshFilter>(true).Sum(f =>
                (int)Enumerable.Range(0, f.sharedMesh.subMeshCount).Sum(i => f.sharedMesh.GetIndexCount(i) / 3d)) != expectedTriangles)
                throw new InvalidOperationException("Imported triangle count differs for " + name);
            return mesh;
        }

        private static Texture2D Texture(string name, TextureImporterType type = TextureImporterType.Default)
        {
            string path = AssetsRoot + "/Textures/" + name + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Missing " + path);
            TextureWrapMode wrap = name.EndsWith("Macro", StringComparison.Ordinal) ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            if (importer.textureType != type || importer.wrapMode != wrap)
            {
                importer.textureType = type;
                importer.wrapMode = wrap;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material GetMaterial(string folder, string name, Color color)
        {
            string path = folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader) throw new InvalidOperationException("URP Lit shader unavailable.");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .15f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void AttachModel(GameObject model, Transform group, Dictionary<string, Material> materials)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, group);
            instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string source = slots[i] ? slots[i].name : string.Empty;
                    if (source.StartsWith("MAT_", StringComparison.Ordinal)) source = source.Substring(4);
                    if (!materials.TryGetValue(source, out slots[i]))
                        throw new InvalidOperationException("No authored terrain material for " + renderer.name + " / " + source);
                }
                renderer.sharedMaterials = slots;
            }
        }

        private static void AuthorPadDetails(Transform group, Transform site, Transform pad, Material joint, Material paint, Material grate)
        {
            Physics.SyncTransforms();
            float y = site.InverseTransformPoint(pad.position).y;
            int placed = 0;
            foreach (float z in new[] { -42f, -34f, -26f, -18f })
            {
                for (float x = -48f; x <= 48f; x += 8f)
                    if (TryMark("Concrete pour seam", new Vector3(x, y, z), new Vector3(7.85f, .013f, .065f), joint)) placed++;
                for (float x = -48f; x <= 48f; x += 8f)
                    if (TryMark("Concrete cross joint", new Vector3(x, y, z + 4f), new Vector3(.065f, .013f, 7.85f), joint)) placed++;
            }
            foreach (float x in new[] { -26f, 26f })
            {
                for (int i = 0; i < 5; i++)
                {
                    float z = -38f + 3.6f * i;
                    if (TryMark("Pump trench inset", new Vector3(x, y, z), new Vector3(2.4f, .015f, .94f), joint)) placed++;
                    for (int slot = -4; slot <= 4; ++slot)
                        if (TryMark("Drainage slat", new Vector3(x + .24f * slot, y + .012f, z),
                            new Vector3(.082f, .016f, .86f), grate)) placed++;
                }
            }
            foreach (float x in new[] { -20f, 20f })
            {
                for (int i=0; i<6; i++)
                    if (TryMark("Industrial hazard stripe", new Vector3(x, y, -39f + 4.2f*i),
                        new Vector3(.32f, .015f, 1.9f), paint)) placed++;
            }
            if (placed < 16) throw new InvalidOperationException("Too few pad details had actual foundation support.");
            Debug.Log("I06_PAD_DETAILS placed " + placed + " visual pieces without new gameplay colliders.");

            bool TryMark(string name, Vector3 at, Vector3 size, Material material)
            {
                Vector3 horizontal = new Vector3(size.x * .47f, 0f, size.z * .47f);
                if (!Supported(at) || !Supported(at + horizontal) || !Supported(at - horizontal)) return false;
                var detail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                detail.name = name;
                detail.transform.SetParent(group, false);
                detail.transform.localPosition = at + Vector3.up * .019f;
                detail.transform.localScale = size;
                UnityEngine.Object.DestroyImmediate(detail.GetComponent<Collider>());
                var renderer = detail.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                return true;
            }

            bool Supported(Vector3 at)
            {
                Vector3 origin = site.TransformPoint(new Vector3(at.x, y + .25f, at.z));
                return Physics.Raycast(origin, -site.up, out RaycastHit hit, .32f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && hit.collider.transform.IsChildOf(site) && Mathf.Abs(site.InverseTransformPoint(hit.point).y - y) < .048f;
            }
        }
    }
}

