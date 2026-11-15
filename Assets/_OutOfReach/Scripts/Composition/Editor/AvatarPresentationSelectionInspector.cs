using UnityEditor;
using UnityEngine;

namespace OutOfReach.Composition.Editor
{
    [CustomEditor(typeof(AvatarPresentationSelection))]
    public sealed class AvatarPresentationSelectionInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("This one field selects the next root-owned I06 body rebuild in Edit mode. It does not replace an existing body or retarget clips. Validate the profile, then run native contact/seat/clearance QA before acceptance.", MessageType.Info);
            var selection = (AvatarPresentationSelection)target;
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || !selection.Profile))
                if (GUILayout.Button("Validate selected profile structure"))
                {
                    AvatarPresentationProfileValidator.Validate(selection.Profile);
                    Debug.Log("Avatar profile structure PASS. Native gameplay/skin acceptance pending.", selection.Profile);
                }
        }

        [MenuItem("Out of Reach/Avatar/Open configured profile selection")]
        private static void OpenSelection() => Selection.activeObject = I06BodyAuthoring.GetOrCreateSelection();
    }
}
