using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OutOfReach.Presentation.Hands
{
    /// <summary>
    /// Original fixed-size padded upper sleeve and rounded elbow cover for the explicit tool lease.
    /// Reads the sole hand owner and actual EVA shoulder after the body update; never writes rig bones.
    /// The 350/320 mm view arm is intentionally distinct from the shorter full-body skeleton.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(250)]
    public sealed class EvaToolSleevePresenter : MonoBehaviour
    {
        [SerializeField] private AstronautHandsPresenter hands;
        [SerializeField] private Transform shoulder;
        [SerializeField] private Material sourceCloth;
        private GameObject visual;
        private Mesh mesh;
        private Material cloth;
        private MeshRenderer sleeveRenderer;
        public const float ElbowCoverRadius = .072f;
        public bool IsReady => isActiveAndEnabled && hands && shoulder && sourceCloth && visual && mesh && cloth
            && (transform.lossyScale - Vector3.one).sqrMagnitude < .000001f;
        public bool IsVisible => IsReady && sleeveRenderer && sleeveRenderer.enabled;
        public Transform Shoulder => shoulder;
        public MeshFilter SleeveMesh => visual ? visual.GetComponent<MeshFilter>() : null;

        /// <summary>Editor/root wiring only. Use a sibling of the original model, never add children inside its 34-joint rig.</summary>
        public void Configure(AstronautHandsPresenter owner, Transform actualEvaShoulder, Material existingCloth)
        {
            if (!owner || !owner.MotionMount || !actualEvaShoulder || !existingCloth)
                throw new ArgumentException("Sleeve needs the existing owner, explicit actual EVA shoulder and cloth shader reference.");
            if (owner.ModelRoot && (transform == owner.ModelRoot || transform.IsChildOf(owner.ModelRoot)))
                throw new ArgumentException("The sleeve must remain outside the original 34-joint model.");
            if (hands) hands.EndToolHold();
            hands = owner; shoulder = actualEvaShoulder; sourceCloth = existingCloth;
            hands.ConfigureToolSleeveShoulder(shoulder, this);
            if (Application.isPlaying) EnsureVisual();
        }

        private void Awake() => EnsureVisual();
        private void OnEnable() { if (Application.isPlaying) EnsureVisual(); }
        private void LateUpdate() => RefreshVisual();

        /// <summary>Read-only-to-rig refresh, also used after synchronous source-pose QA sampling.</summary>
        public bool RefreshVisual()
        {
            if (!IsReady || !hands.IsVisible || !hands.IsToolHoldActive || !hands.IsToolHoldSettled)
            { Hide(); return false; }
            Vector3 elbow = hands.ToolElbowWorldPosition, wrist = hands.ToolWristWorldPosition;
            Vector3 upper = elbow - shoulder.position;
            if (!Finite(upper) || Mathf.Abs(upper.magnitude - AstronautHandsPresenter.ToolUpperSleeveMetres) > .002f
                || Mathf.Abs(Vector3.Distance(elbow, wrist) - AstronautHandsPresenter.ToolForearmMetres) > .002f)
            { hands.EndToolHold(); Hide(); return false; }
            if (hands.IsToolHoldPaused)
            {
                // An existing valid pause freezes the same visible world geometry. It cannot create a new pose.
                if (!IsVisible || Vector3.Distance(visual.transform.position, shoulder.position) > .00002f
                    || Vector3.Distance(visual.transform.TransformPoint(new Vector3(0, 0, AstronautHandsPresenter.ToolUpperSleeveMetres)), elbow) > .00002f)
                { hands.EndToolHold(); Hide(); return false; }
                return true;
            }
            Vector3 up = Vector3.ProjectOnPlane(hands.MotionMount.up, upper.normalized);
            if (up.sqrMagnitude < .01f) { hands.EndToolHold(); Hide(); return false; }
            visual.transform.SetPositionAndRotation(shoulder.position, Quaternion.LookRotation(upper, up));
            visual.transform.localScale = Vector3.one;
            sleeveRenderer.enabled = true;
            return true;
        }

        private void EnsureVisual()
        {
            if (visual || !hands || !shoulder || !sourceCloth) return;
            // Do not stretch an unrelated atlas across new geometry or mutate the imported glove material.
            cloth = new Material(sourceCloth.shader) { name = "Original EVA upper sleeve - matte cloth" };
            if (cloth.HasProperty("_BaseColor")) cloth.SetColor("_BaseColor", new Color(.19f, .215f, .235f, 1f));
            if (cloth.HasProperty("_Metallic")) cloth.SetFloat("_Metallic", 0f);
            if (cloth.HasProperty("_Smoothness")) cloth.SetFloat("_Smoothness", .22f);
            mesh = BuildMesh();
            visual = new GameObject("Original padded upper sleeve and elbow cover");
            visual.transform.SetParent(transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            sleeveRenderer = visual.AddComponent<MeshRenderer>();
            sleeveRenderer.sharedMaterial = cloth;
            sleeveRenderer.shadowCastingMode = ShadowCastingMode.On;
            sleeveRenderer.receiveShadows = true;
            Hide();
        }

        private static Mesh BuildMesh()
        {
            // Metres, no deformation/scaling. Profile extends 55 mm inside the shoulder garment.
            // The 72 mm rounded elbow physically encloses every original 60 mm radius rear-cap point.
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var uv = new List<Vector2>(); var triangles = new List<int>();
            float[] z = { -.055f, -.035f, 0f, .04f, .10f, .17f, .23f, .28f, .31f, .35f, .37f };
            float[] radius = { .075f, .086f, .088f, .087f, .084f, .080f, .075f, .070f, .067f, .065f, .060f };
            const int sides = 32;
            for (int ring = 0; ring < z.Length; ring++)
                for (int i = 0; i <= sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    vertices.Add(new Vector3(Mathf.Cos(a) * radius[ring], Mathf.Sin(a) * radius[ring], z[ring]));
                    normals.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0)); uv.Add(new Vector2(i / (float)sides, (z[ring] + .055f) / .425f));
                    if (ring > 0 && i < sides) Quad(triangles, (ring - 1) * (sides + 1) + i, sides + 1);
                }
            Cap(vertices, normals, uv, triangles, 0, sides, z[0], false);
            Cap(vertices, normals, uv, triangles, (z.Length - 1) * (sides + 1), sides, z[z.Length - 1], true);
            int first = vertices.Count; const int latitude = 20;
            for (int row = 0; row <= latitude; row++)
                for (int i = 0; i <= sides; i++)
                {
                    float p = Mathf.PI * row / latitude, a = i * Mathf.PI * 2f / sides;
                    Vector3 n = new Vector3(Mathf.Sin(p) * Mathf.Cos(a), Mathf.Sin(p) * Mathf.Sin(a), -Mathf.Cos(p));
                    vertices.Add(new Vector3(0, 0, AstronautHandsPresenter.ToolUpperSleeveMetres) + n * ElbowCoverRadius);
                    normals.Add(n); uv.Add(new Vector2(i / (float)sides, row / (float)latitude));
                    if (row > 0 && i < sides) Quad(triangles, first + (row - 1) * (sides + 1) + i, sides + 1);
                }
            var result = new Mesh { name = "Original fixed 350mm upper sleeve and 72mm elbow cover" };
            result.SetVertices(vertices); result.SetNormals(normals); result.SetUVs(0, uv); result.SetTriangles(triangles, 0); result.RecalculateBounds();
            return result;
        }
        private static void Quad(List<int> t, int a, int row)
        { t.Add(a); t.Add(a + 1); t.Add(a + row); t.Add(a + 1); t.Add(a + row + 1); t.Add(a + row); }
        private static void Cap(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, int ring, int sides, float z, bool front)
        {
            int centre = v.Count; v.Add(new Vector3(0, 0, z)); n.Add(front ? Vector3.forward : Vector3.back); uv.Add(new Vector2(.5f, .5f));
            for (int i = 0; i < sides; i++) { t.Add(centre); t.Add(ring + (front ? i : i + 1)); t.Add(ring + (front ? i + 1 : i)); }
        }
        private static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsInfinity(p.x) && !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z);
        private void Hide() { if (sleeveRenderer) sleeveRenderer.enabled = false; }
        private void OnDisable() { if (hands) hands.EndToolHold(); Hide(); }
        private void OnDestroy()
        {
            if (hands) hands.EndToolHold();
            if (visual) Destroy(visual); if (mesh) Destroy(mesh); if (cloth) Destroy(cloth);
        }
    }
}
