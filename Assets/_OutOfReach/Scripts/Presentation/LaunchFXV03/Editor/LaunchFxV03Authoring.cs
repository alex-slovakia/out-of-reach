using System;
using UnityEditor;
using UnityEngine;

namespace OutOfReach.Presentation.LaunchFXV03.Editor
{
    public static class LaunchFxV03Authoring
    {
        public const string Folder="Assets/_OutOfReach/Visuals/LaunchFX_V03";
        public const string PrefabPath=Folder+"/LaunchFX_V03.prefab";
        [MenuItem("OUT OF REACH/Visuals/Build Launch FX V03 Prefab")]
        public static void Build()
        {
            // Asset authoring only: never opens or mutates Bootstrap or the active scene.
            var plume=Material("LaunchPlumeV03","OutOfReach/LaunchV03/Plume");
            var cloud=Material("LaunchCloudV03","OutOfReach/LaunchV03/Cloud");
            var root=new GameObject("Launch FX V03 (bind actual launch anchors)");
            try
            {
                var presenter=root.AddComponent<LaunchFxV03Presenter>();
                presenter.ConfigureMaterials(plume,cloud);
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            Debug.Log("Launch FX V03 prefab authored. Scene wiring and native visual verification remain integration steps.");
        }
        private static Material Material(string name,string shaderName)
        {
            var shader=Shader.Find(shaderName);
            if (!shader || !shader.isSupported) throw new InvalidOperationException("Import and verify shader first: "+shaderName);
            string path=Folder+"/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material=new Material(shader) { name=name }; AssetDatabase.CreateAsset(material,path); }
            else if(material.shader!=shader) throw new InvalidOperationException("Unexpected shader on owned material: "+path);
            material.enableInstancing=true; EditorUtility.SetDirty(material); return material;
        }
    }
}
