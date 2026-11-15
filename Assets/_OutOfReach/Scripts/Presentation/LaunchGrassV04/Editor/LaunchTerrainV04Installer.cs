using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OutOfReach.Presentation.LaunchGrassV04.Editor
{
    /// <summary>
    /// LAUNCH-TERRAIN-V04 installer: the reworked landscape around the launch site (see
    /// <see cref="LaunchTerrainV04Builder"/>). It generates the meshes from the scene's own LT_TerrainInner, adds
    /// them under "Launch site - stationary" with the existing ground material, and switches the two things they
    /// replace off without deleting them: the LT_TerrainInner renderer (its object, mesh and any collider stay,
    /// and the pampas field keeps reading its heights) and the TERRAIN-V03 far land group.
    /// No collider is added or changed; the install fails and rolls back if the active collider set differs.
    /// "Remove" restores the previous terrain. Independent of the grass installer; either order works.
    /// </summary>
    public static class LaunchTerrainV04Installer
    {
        public const string GroupName = "Launch terrain V04 - steppe, dry river and ranges";
        public const string MeshFolder = LaunchGrassV04Installer.Folder + "/GeneratedTerrain";
        public const string OldFarGroupName = "Launch terrain V03 - far land and mountains";
        private const string InnerPath = "Coastal relief and launch pad detail/LT_LandTerrain/LT_TerrainInner";

        public static string LastReport { get; private set; }

        [MenuItem("Tools/OUT OF REACH/Launch Terrain V04 - install into open scene (no save)")]
        public static void InstallMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            Apply(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("Launch Terrain V04", "Installed. Review the landscape, then save the scene.\n\n" + LastReport, "OK");
        }

        [MenuItem("Tools/OUT OF REACH/Launch Terrain V04 - remove (previous terrain back, no save)")]
        public static void RemoveMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            bool removed = Remove(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("Launch Terrain V04", removed ? "Removed. The previous terrain is visible again." : "Nothing to remove.", "OK");
        }

        /// <summary>Integrator batch entry: -executeMethod OutOfReach.Presentation.LaunchGrassV04.Editor.LaunchTerrainV04Installer.InstallIntoBootstrap</summary>
        public static void InstallIntoBootstrap()
        {
            Scene scene = EditorSceneManager.OpenScene(LaunchGrassV04Installer.BootstrapPath, OpenSceneMode.Single);
            Apply(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Bootstrap could not be saved.");
            Debug.Log("LAUNCH_TERRAIN_V04_INSTALLED " + LaunchGrassV04Installer.BootstrapPath + " " + LastReport);
        }

        public static GameObject Apply(Scene scene)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Install in Edit mode.");
            Transform site = LaunchGrassV04Installer.FindSite(scene);
            if ((site.lossyScale - Vector3.one).sqrMagnitude > .0001f) throw new InvalidOperationException("The launch site must keep metre scale 1.");
            Transform innerNode = site.Find(InnerPath);
            MeshFilter inner = innerNode ? innerNode.GetComponent<MeshFilter>() : null;
            MeshRenderer innerRenderer = innerNode ? innerNode.GetComponent<MeshRenderer>() : null;
            if (!inner || !innerRenderer || !inner.sharedMesh || !inner.sharedMesh.isReadable || !inner.gameObject.activeInHierarchy)
                throw new InvalidOperationException("The active, readable LT_TerrainInner ground mesh is required.");
            Material ground = innerRenderer.sharedMaterial;
            if (!ground) throw new InvalidOperationException("LT_TerrainInner has no ground material.");
            List<int> collidersBefore = LaunchGrassV04Installer.ActiveColliders(scene);

            // The old surface in site-local space.
            Mesh oldMesh = inner.sharedMesh;
            Matrix4x4 toSite = site.worldToLocalMatrix * inner.transform.localToWorldMatrix;
            Vector3[] vertices = oldMesh.vertices, normals = oldMesh.normals;
            if (normals == null || normals.Length != vertices.Length) throw new InvalidOperationException("LT_TerrainInner has no normals.");
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = toSite.MultiplyPoint3x4(vertices[i]);
                normals[i] = toSite.MultiplyVector(normals[i]).normalized;
            }
            var triangles = new List<int>();
            for (int sub = 0; sub < oldMesh.subMeshCount; sub++)
                if (oldMesh.GetTopology(sub) == MeshTopology.Triangles) triangles.AddRange(oldMesh.GetTriangles(sub));

            LaunchTerrainV04Builder.Result result = LaunchTerrainV04Builder.Build(vertices, normals, triangles.ToArray());
            if (result.MaxSeamError > 1e-4f || result.OldSamplesMissing > 0)
                throw new InvalidOperationException("The new terrain does not join the old ground cleanly: " + result.Report);

            EnsureFolder(MeshFolder);
            Transform previous = site.Find(GroupName);
            Transform oldFar = site.Find(OldFarGroupName);
            bool innerWasEnabled = innerRenderer.enabled, oldFarWasActive = oldFar && oldFar.gameObject.activeSelf;
            var group = new GameObject(GroupName);
            group.transform.SetParent(site, false);
            group.layer = inner.gameObject.layer;
            try
            {
                foreach (LaunchTerrainV04Builder.Part part in result.Parts)
                {
                    var go = new GameObject(part.Name) { layer = inner.gameObject.layer };
                    go.transform.SetParent(group.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(part);
                    var renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = ground;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = true;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
                // Replaced, not deleted: the old ground's renderer and the V03 far land.
                innerRenderer.enabled = false;
                if (oldFarWasActive) oldFar.gameObject.SetActive(false);
                if (group.GetComponentsInChildren<Collider>(true).Length != 0)
                    throw new InvalidOperationException("The new terrain must be passive geometry only.");
                if (!collidersBefore.SequenceEqual(LaunchGrassV04Installer.ActiveColliders(scene)))
                    throw new InvalidOperationException("Installer changed the set of active colliders.");
                if (previous) Object.DestroyImmediate(previous.gameObject);
                LastReport = result.Report + " material=" + ground.name + " oldInnerRendererOff=" + innerWasEnabled
                    + " v03FarLandOff=" + oldFarWasActive + " collidersChanged=0";
                Debug.Log("LAUNCH_TERRAIN_V04_READY " + LastReport);
                return group;
            }
            catch
            {
                innerRenderer.enabled = innerWasEnabled;
                if (oldFarWasActive) oldFar.gameObject.SetActive(true);
                Object.DestroyImmediate(group);
                throw;
            }
        }

        /// <summary>Puts the previous terrain back: removes the V04 group, re-enables what it replaced.</summary>
        public static bool Remove(Scene scene)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Remove in Edit mode.");
            Transform site = LaunchGrassV04Installer.FindSite(scene);
            Transform group = site.Find(GroupName);
            if (!group) return false;
            Transform innerNode = site.Find(InnerPath);
            MeshRenderer innerRenderer = innerNode ? innerNode.GetComponent<MeshRenderer>() : null;
            if (innerRenderer) innerRenderer.enabled = true;
            Transform oldFar = site.Find(OldFarGroupName);
            if (oldFar) oldFar.gameObject.SetActive(true);
            Object.DestroyImmediate(group.gameObject);
            Debug.Log("LAUNCH_TERRAIN_V04_REMOVED previous terrain restored");
            return true;
        }

        private static Mesh SaveMesh(LaunchTerrainV04Builder.Part part)
        {
            string path = MeshFolder + "/" + part.Name + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool create = !mesh;
            if (create) mesh = new Mesh(); else mesh.Clear();
            mesh.name = part.Name;
            mesh.indexFormat = part.Vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(part.Vertices);
            mesh.SetNormals(part.Normals);
            mesh.SetTriangles(part.Triangles, 0, true);
            mesh.RecalculateBounds();
            if (create) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
