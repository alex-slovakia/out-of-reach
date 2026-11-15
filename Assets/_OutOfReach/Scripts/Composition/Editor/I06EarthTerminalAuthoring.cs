using System;
using OutOfReach.Interaction;
using OutOfReach.Presentation.Terminal;
using OutOfReach.Presentation.Terminal.Editor;
using UnityEditor;
using UnityEngine;

namespace OutOfReach.Composition.Editor
{
    /// <summary>Additive scene authoring only. Root chooses placement and owns the scene save.</summary>
    public static class I06EarthTerminalAuthoring
    {
        public const string PrefabPath="Assets/_OutOfReach/Visuals/Props/MissionTerminal/MissionTerminal.prefab";
        public const string HandsPath="Assets/_OutOfReach/Visuals/Models/Aurora7/Props/AstronautHands/AstronautHands.prefab";
        public const string RigPath="Assets/_OutOfReach/Visuals/Models/Aurora7/Props/AstronautHands/AstronautHands_rig.json";
        public const string RootName="I06 Physical Earth archive";
        public static readonly Vector3 CloseFocusLocalPosition=new Vector3(.012f,.025f,-.400f);

        public static I06EarthTerminalBindings Create(Transform shipParent,Camera soleCamera,
            Vector3 localScreenCentre,Quaternion localScreenRotation)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || !shipParent || !soleCamera
                || EditorUtility.IsPersistent(shipParent) || EditorUtility.IsPersistent(soleCamera))
                throw new InvalidOperationException("Supply existing scene ship parent and sole player camera in Edit mode.");
            if(shipParent.Find(RootName)) throw new InvalidOperationException("Earth archive already exists; root must explicitly replace its owned instance.");
            if((shipParent.lossyScale-Vector3.one).sqrMagnitude>.00001f)
                throw new InvalidOperationException("Terminal parent requires unit scale.");
            PhysicalTerminalBuilder.BuildAndValidate();
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var hands=AssetDatabase.LoadAssetAtPath<GameObject>(HandsPath);
            var rig=AssetDatabase.LoadAssetAtPath<TextAsset>(RigPath);
            if(!prefab || !hands || !rig) throw new InvalidOperationException("Approved terminal and original hands must be imported first.");
            GameObject host=new GameObject(RootName); host.transform.SetParent(shipParent,false);
            host.transform.SetLocalPositionAndRotation(localScreenCentre,localScreenRotation);
            try
            {
                var terminalObject=(GameObject)PrefabUtility.InstantiatePrefab(prefab,host.transform);
                terminalObject.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
                var terminal=terminalObject.GetComponent<PhysicalEarthTerminalPresenter>();
                if(!terminal) throw new InvalidOperationException("Physical terminal presenter missing.");
                var target=terminalObject.AddComponent<InteractableTarget>(); target.InteractionId=I06EarthTerminalBindings.TargetId;
                // The actual casing BoxCollider is the interaction surface; no broad trigger through nearby walls.
                var gloveObject=(GameObject)PrefabUtility.InstantiatePrefab(hands,host.transform);
                gloveObject.name="Dedicated Earth viewer gloves"; gloveObject.transform.localScale=Vector3.one;
                gloveObject.SetActive(false);
                var focus=terminalObject.transform.Find("FocusCameraAnchor");
                if(!focus) throw new InvalidOperationException("Approved terminal focus anchor missing.");
                // Scene-instance fitting override only: source FBX and visual prefab remain unchanged.
                focus.localPosition=CloseFocusLocalPosition;
                PrefabUtility.RecordPrefabInstancePropertyModifications(focus);
                var bindings=host.AddComponent<I06EarthTerminalBindings>();
                bindings.ConfigureScene(terminal,target,focus,
                    soleCamera,gloveObject.transform,rig);
                if(host.GetComponentsInChildren<Camera>(true).Length!=0 || host.GetComponentsInChildren<Light>(true).Length!=0
                    || host.GetComponentsInChildren<AudioListener>(true).Length!=0)
                    throw new InvalidOperationException("Terminal must not add a camera, listener or light.");
                return bindings;
            }
            catch { UnityEngine.Object.DestroyImmediate(host); throw; }
        }
    }
}
