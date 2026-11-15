using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OutOfReach.Presentation.Hands
{
    /// <summary>
    /// EVA-TETHER-01: padded upper sleeves and elbow covers that join the delivered rigid glove forearms to the suit's
    /// shoulders while the gloves climb the tether, so a reaching or pulling arm reads as one arm, not a floating
    /// forearm. Reads the hands presenter's solved poses after it has posed the rig; never writes rig joints.
    /// Shoulders are the actual EVA body bones when bound (upperarm01.L/R), otherwise the presenter's view shoulders.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(250)]
    public sealed class EvaTetherArmSleeves : MonoBehaviour
    {
        [SerializeField] private AstronautHandsPresenter hands;
        [Tooltip("Optional actual EVA shoulder bones (upperarm01.R, upperarm01.L), read only.")]
        [SerializeField] private Transform rightShoulder;
        [SerializeField] private Transform leftShoulder;
        [SerializeField] private Material cloth;

        public const float ElbowCoverRadius = 0.072f;

        private readonly Transform[] sleeves = new Transform[2];
        private readonly Transform[] elbows = new Transform[2];
        private readonly MeshRenderer[] renderers = new MeshRenderer[4];
        private Mesh sleeveMesh;
        private Mesh elbowMesh;
        private GameObject root;

        public AstronautHandsPresenter Hands => hands;

        public bool IsShowing => renderers[0] && (renderers[0].enabled || renderers[2].enabled);

        /// <summary>Upper sleeve start (shoulder) and elbow centre of a visible arm, for QA; false while hidden.</summary>
        public bool TryGetArm(EvaTetherHand hand, out Vector3 shoulder, out Vector3 elbow)
        {
            int i = (int)hand;
            shoulder = sleeves[i] ? sleeves[i].position : default;
            elbow = elbows[i] ? elbows[i].position : default;
            return renderers[i * 2] && renderers[i * 2].enabled;
        }

        /// <summary>Editor/root wiring (EvaTetherAuthoring.Install).</summary>
        public void Configure(AstronautHandsPresenter owner, Transform actualRightShoulder, Transform actualLeftShoulder,
            Material clothMaterial)
        {
            if (!owner || !clothMaterial)
            {
                throw new ArgumentException("The tether sleeves need the hands presenter and a cloth material.");
            }

            hands = owner;
            rightShoulder = actualRightShoulder;
            leftShoulder = actualLeftShoulder;
            cloth = clothMaterial;
        }

        private void Awake()
        {
            if (Application.isPlaying)
            {
                EnsureVisuals();
            }
        }

        private void OnDisable() => Hide();

        private void OnDestroy()
        {
            if (root)
            {
                Destroy(root);
            }

            if (sleeveMesh)
            {
                Destroy(sleeveMesh);
            }

            if (elbowMesh)
            {
                Destroy(elbowMesh);
            }
        }

        private void LateUpdate()
        {
            if (!hands || !EnsureVisuals())
            {
                return;
            }

            if (!hands.IsVisible || !hands.IsTetherClimbActive || hands.IsToolHoldActive)
            {
                Hide();
                return;
            }

            if (hands.IsToolHoldPaused)
            {
                return; // Paused: keep the frozen geometry, exactly like the gloves.
            }

            for (int i = 0; i < 2; i++)
            {
                EvaTetherArmPose pose = hands.TetherPose((EvaTetherHand)i);
                Transform bone = i == (int)EvaTetherHand.Right ? rightShoulder : leftShoulder;
                Vector3 shoulder = bone && bone.gameObject.activeInHierarchy ? bone.position : pose.Shoulder;
                Vector3 elbow = pose.Elbow;
                Vector3 upper = elbow - shoulder;
                float length = upper.magnitude;
                bool show = hands.TetherBlend > 0.98f && Finite(shoulder) && Finite(elbow) && length > 0.12f && length < 0.75f;
                renderers[i * 2].enabled = show;
                renderers[(i * 2) + 1].enabled = show;
                if (!show)
                {
                    continue;
                }

                Vector3 up = Vector3.ProjectOnPlane(Vector3.up, upper);
                Quaternion rotation = Quaternion.LookRotation(upper, up.sqrMagnitude > 1e-4f ? up : Vector3.forward);
                const float inside = 0.05f; // starts inside the shoulder garment
                sleeves[i].SetPositionAndRotation(shoulder - (upper / length * inside), rotation);
                sleeves[i].localScale = new Vector3(1f, 1f, length + inside);
                elbows[i].SetPositionAndRotation(elbow, rotation);
                elbows[i].localScale = Vector3.one;
            }
        }

        private void Hide()
        {
            foreach (MeshRenderer renderer in renderers)
            {
                if (renderer)
                {
                    renderer.enabled = false;
                }
            }
        }

        private bool EnsureVisuals()
        {
            if (root)
            {
                return true;
            }

            if (!cloth)
            {
                return false;
            }

            sleeveMesh = BuildSleeve();
            elbowMesh = BuildElbow();
            root = new GameObject("EVA tether arm sleeves (generated)") { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(transform, false);
            for (int i = 0; i < 2; i++)
            {
                string side = i == (int)EvaTetherHand.Right ? "Right" : "Left";
                sleeves[i] = Part(side + " upper sleeve", sleeveMesh, out renderers[i * 2]);
                elbows[i] = Part(side + " elbow cover", elbowMesh, out renderers[(i * 2) + 1]);
            }

            Hide();
            return true;
        }

        private Transform Part(string name, Mesh mesh, out MeshRenderer renderer)
        {
            var part = new GameObject(name) { hideFlags = HideFlags.DontSave };
            part.transform.SetParent(root.transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = cloth;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return part.transform;
        }

        /// <summary>Unit-length padded sleeve along +Z (scaled to the shoulder-elbow length), tapering to the elbow.</summary>
        private static Mesh BuildSleeve()
        {
            float[] z = { 0f, 0.04f, 0.12f, 0.3f, 0.5f, 0.7f, 0.85f, 0.95f, 1f };
            float[] radius = { 0.078f, 0.086f, 0.088f, 0.086f, 0.082f, 0.077f, 0.072f, 0.068f, 0.066f };
            const int sides = 24;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int ring = 0; ring < z.Length; ring++)
            {
                for (int k = 0; k <= sides; k++)
                {
                    float a = 2f * Mathf.PI * k / sides;
                    var direction = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    vertices.Add((direction * radius[ring]) + new Vector3(0f, 0f, z[ring]));
                    normals.Add(direction);
                    uvs.Add(new Vector2((float)k / sides, z[ring]));
                }
            }

            int row = sides + 1;
            for (int ring = 0; ring < z.Length - 1; ring++)
            {
                for (int k = 0; k < sides; k++)
                {
                    int a = (ring * row) + k;
                    int c = a + row;
                    // Unity front faces: cross(b - a, c - a) points outward.
                    triangles.Add(a);
                    triangles.Add(a + 1);
                    triangles.Add(c);
                    triangles.Add(a + 1);
                    triangles.Add(c + 1);
                    triangles.Add(c);
                }
            }

            var mesh = new Mesh { name = "EVA tether upper sleeve (unit length)" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildElbow()
        {
            const int sides = 20;
            const int rows = 12;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int r = 0; r <= rows; r++)
            {
                float p = Mathf.PI * r / rows;
                for (int k = 0; k <= sides; k++)
                {
                    float a = 2f * Mathf.PI * k / sides;
                    var n = new Vector3(Mathf.Sin(p) * Mathf.Cos(a), Mathf.Sin(p) * Mathf.Sin(a), Mathf.Cos(p));
                    vertices.Add(n * ElbowCoverRadius);
                    normals.Add(n);
                    uvs.Add(new Vector2((float)k / sides, (float)r / rows));
                }
            }

            int row = sides + 1;
            for (int r = 0; r < rows; r++)
            {
                for (int k = 0; k < sides; k++)
                {
                    int a = (r * row) + k;
                    int c = a + row;
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(a + 1);
                    triangles.Add(a + 1);
                    triangles.Add(c);
                    triangles.Add(c + 1);
                }
            }

            var mesh = new Mesh { name = "EVA tether elbow cover" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsInfinity(v.x) && !float.IsNaN(v.y)
            && !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
    }
}
