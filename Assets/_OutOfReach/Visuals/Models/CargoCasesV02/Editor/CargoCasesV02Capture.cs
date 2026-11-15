using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutOfReach.Visuals.CargoCasesV02.Editor
{
    /// <summary>Verification stills only (Edit or Play mode): renders a camera into a PNG. Changes nothing in the scene.</summary>
    public static class CargoCasesV02Capture
    {
        /// <summary>The dock frame the supply rack is authored in (parent of "Temporary preflight supply").</summary>
        public static Transform Dock()
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Temporary preflight supply" && t.parent) return t.parent;
            return null;
        }

        /// <summary>Free camera given in dock-local metres.</summary>
        public static string ShotDock(string file, Vector3 localPosition, Vector3 localTarget, float fov = 55f, int width = 1600, int height = 900)
        {
            Transform dock = Dock();
            if (!dock) return "no dock";
            return Shot(file, dock.TransformPoint(localPosition), dock.TransformPoint(localTarget), fov, width, height);
        }

        public static string Shot(string file, Vector3 position, Vector3 target, float fov = 55f, int width = 1600, int height = 900)
        {
            var go = new GameObject("CC2 capture camera") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var cam = go.AddComponent<Camera>();
                Camera main = Camera.main ? Camera.main : Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
                if (main) cam.CopyFrom(main);
                cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
                cam.fieldOfView = fov;
                cam.nearClipPlane = .03f;
                cam.enabled = false;
                return Render(cam, file, width, height);
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>The player's own view: the live main camera exactly as it stands (use in Play mode).</summary>
        public static string ShotMain(string file, int width = 1600, int height = 900)
        {
            Camera main = Camera.main;
            return main ? Render(main, file, width, height) + " from " + main.name + " at " + main.transform.position.ToString("F2") : "no main camera";
        }

        private static string Render(Camera cam, string file, int width, int height)
        {
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = cam.targetTexture;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllBytes(file, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                return "wrote " + file;
            }
            finally
            {
                cam.targetTexture = previous;
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
