using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutOfReach.Visuals.InteriorHeroV01.Editor
{
    /// <summary>Read-only survey of Bootstrap: one JSON line per transform (ship space), used to author the hero props
    /// against the real interactables, colliders and anchors. Never saves the scene.</summary>
    public static class InteriorHeroV01SceneDump
    {
        // Unity -batchmode -quit -executeMethod OutOfReach.Visuals.InteriorHeroV01.Editor.InteriorHeroV01SceneDump.DumpBootstrapBatch
        public static void DumpBootstrapBatch()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/_OutOfReach/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            string path = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "Logs", "interior_hero_scene_dump.jsonl");
            File.WriteAllText(path, Dump(scene));
            Debug.Log("INTERIOR_HERO_V01_DUMP " + path);
        }

        public static string Dump(Scene scene)
        {
            var sb = new StringBuilder();
            foreach (GameObject root in scene.GetRootGameObjects()) Walk(root.transform, root.name, sb);
            return sb.ToString();
        }

        private static void Walk(Transform t, string path, StringBuilder sb)
        {
            sb.Append("{\"path\":").Append(Q(path));
            sb.Append(",\"on\":").Append(t.gameObject.activeInHierarchy ? 1 : 0);
            sb.Append(",\"p\":").Append(V(t.position)).Append(",\"r\":").Append(V(t.eulerAngles)).Append(",\"s\":").Append(V(t.lossyScale));
            sb.Append(",\"c\":[");
            bool first = true;
            foreach (Component c in t.GetComponents<Component>())
            {
                if (!c || c is Transform) continue;
                if (!first) sb.Append(',');
                first = false;
                sb.Append(Q(c.GetType().Name));
            }
            sb.Append(']');
            var renderer = t.GetComponent<Renderer>();
            if (renderer)
            {
                sb.Append(",\"ren\":").Append(renderer.enabled ? 1 : 0).Append(",\"rb\":").Append(B(renderer.bounds)).Append(",\"mats\":[");
                Material[] mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) sb.Append(i > 0 ? "," : "").Append(Q(mats[i] ? mats[i].name : "null"));
                sb.Append(']');
            }
            var filter = t.GetComponent<MeshFilter>();
            if (filter && filter.sharedMesh)
            {
                long tris = 0;
                for (int i = 0; i < filter.sharedMesh.subMeshCount; i++) tris += (long)filter.sharedMesh.GetIndexCount(i) / 3;
                sb.Append(",\"mesh\":").Append(Q(filter.sharedMesh.name)).Append(",\"tris\":").Append(tris)
                  .Append(",\"asset\":").Append(Q(AssetDatabase.GetAssetPath(filter.sharedMesh)));
            }
            foreach (Collider col in t.GetComponents<Collider>())
                sb.Append(",\"col\":{\"t\":").Append(Q(col.GetType().Name)).Append(",\"en\":").Append(col.enabled ? 1 : 0)
                  .Append(",\"trig\":").Append(col.isTrigger ? 1 : 0).Append(",\"b\":").Append(B(col.bounds)).Append('}');
            foreach (MonoBehaviour mb in t.GetComponents<MonoBehaviour>())
            {
                if (!mb) continue;
                var so = new SerializedObject(mb);
                SerializedProperty id = so.FindProperty("interactionId");
                if (id != null && id.propertyType == SerializedPropertyType.String) sb.Append(",\"iid\":").Append(Q(id.stringValue));
            }
            var light = t.GetComponent<Light>();
            if (light) sb.Append(",\"light\":{\"type\":").Append(Q(light.type.ToString())).Append(",\"i\":").Append(F(light.intensity))
                .Append(",\"range\":").Append(F(light.range)).Append(",\"en\":").Append(light.enabled ? 1 : 0)
                .Append(",\"col\":").Append(Q(ColorUtility.ToHtmlStringRGB(light.color))).Append('}');
            var text = t.GetComponent<TextMesh>();
            if (text) sb.Append(",\"text\":").Append(Q(text.text));
            sb.Append("}\n");
            foreach (Transform child in t) Walk(child, path + "/" + child.name, sb);
        }

        private static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);
        private static string V(Vector3 v) => "[" + F(v.x) + "," + F(v.y) + "," + F(v.z) + "]";
        private static string B(Bounds b) => "[" + V(b.min) + "," + V(b.max) + "]";
        private static string Q(string s) => "\"" + (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\n").Replace("\r", "") + "\"";
    }
}
