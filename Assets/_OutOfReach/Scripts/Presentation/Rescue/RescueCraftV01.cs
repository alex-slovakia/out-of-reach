using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OutOfReach.Presentation.Rescue
{
    /// <summary>
    /// RESCUE-CRAFT-V01: an original recovery craft built procedurally at runtime from lathed and boxed parts
    /// (no downloaded mesh, no agency insignia, logos or real vehicle livery). Local +Z is the nose with the docking
    /// collar; the craft is about 13 m long. Graphite hull, amber running lights, cyan docking lights and windows.
    /// Visual only: no collider, no physics, no domain reference. The rescue presenter is its only driver.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RescueCraftV01 : MonoBehaviour
    {
        public const float CollarFrontZ = 7.62f;

        [SerializeField] private Material hull, hullLight, dark, amber, cyan, plume, window;
        private readonly List<Transform> retroPlumes = new List<Transform>();
        private readonly List<Transform> sidePlumes = new List<Transform>();
        private readonly List<Renderer> collarLamps = new List<Renderer>();
        private readonly List<Renderer> blinkLamps = new List<Renderer>();
        private Light floodlight, burnLight;
        private bool built;

        public Light Floodlight { get { Build(); return floodlight; } }
        public bool IsBuilt => built;

        public void ConfigureMaterials(Material hullMaterial, Material lightPanel, Material darkMetal, Material amberLamp,
            Material cyanLamp, Material plumeMaterial, Material windowMaterial)
        {
            hull = hullMaterial; hullLight = lightPanel; dark = darkMetal; amber = amberLamp; cyan = cyanLamp;
            plume = plumeMaterial; window = windowMaterial;
        }

        private void Awake() => Build();

        /// <summary>Builds the visual once. Safe to call repeatedly.</summary>
        public void Build()
        {
            if (built) return;
            built = true;
            Transform body = Group("Body");
            // Crew cabin, nose frustum, collar and soft-capture ring (lathed along +Z).
            Lathe("Cabin", body, hull, new[] { 0.5f, 5.6f }, new[] { 1.55f, 1.55f }, 28);
            Lathe("Nose", body, hull, new[] { 5.6f, 7.0f }, new[] { 1.55f, .86f }, 28);
            Lathe("Collar", body, dark, new[] { 7.0f, 7.5f }, new[] { .78f, .78f }, 24);
            Lathe("SoftCaptureRing", body, hullLight, new[] { 7.46f, CollarFrontZ }, new[] { .98f, .98f }, 32);
            Lathe("CollarFace", body, dark, new[] { CollarFrontZ, CollarFrontZ + .01f }, new[] { .62f, .0f }, 24);
            foreach (float z in new[] { 1.4f, 2.7f, 4.0f })
                Lathe("PanelBand", body, hullLight, new[] { z, z + .14f }, new[] { 1.575f, 1.575f }, 28);
            Lathe("AmberBand", body, amber, new[] { 5.2f, 5.42f }, new[] { 1.58f, 1.58f }, 28);
            // Service module, radiator collar and engine bell (opening aft, -Z).
            Lathe("Service", body, dark, new[] { -3.2f, .5f }, new[] { 1.85f, 1.85f }, 28);
            Lathe("ServiceRib", body, hullLight, new[] { -1.7f, -1.5f }, new[] { 1.88f, 1.88f }, 28);
            Lathe("Adapter", body, hull, new[] { .5f, .62f }, new[] { 1.85f, 1.55f }, 28);
            Lathe("EngineBell", body, dark, new[] { -4.7f, -3.2f }, new[] { 1.05f, .45f }, 24);
            Lathe("EngineThroat", body, amber, new[] { -3.3f, -3.25f }, new[] { .42f, .0f }, 16);
            // Cabin windows: a cyan-lit strip on the upper forward cabin.
            for (int i = 0; i < 5; i++)
            {
                float angle = Mathf.Lerp(-38f, 38f, i / 4f) * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Sin(angle) * 1.56f, Mathf.Cos(angle) * 1.56f, 4.75f);
                Box("Window", body, window, pos, Quaternion.LookRotation(Vector3.forward, pos.normalized), new Vector3(.34f, .02f, .5f));
            }
            // Radiator / solar wings on the service module with amber tip lights.
            for (int side = -1; side <= 1; side += 2)
            {
                Box("WingSpar", body, hullLight, new Vector3(side * 2.4f, 0, -1.6f), Quaternion.identity, new Vector3(1.2f, .12f, .22f));
                Box("Wing", body, hull, new Vector3(side * 5.2f, 0, -1.6f), Quaternion.identity, new Vector3(4.6f, .05f, 1.9f));
                for (int k = 0; k < 4; k++)
                    Box("WingCell", body, window, new Vector3(side * (3.6f + k * 1.08f), .03f, -1.6f), Quaternion.identity, new Vector3(.94f, .012f, 1.62f));
                blinkLamps.Add(Box("WingTipLamp", body, amber, new Vector3(side * 7.58f, 0, -1.6f), Quaternion.identity, new Vector3(.16f, .16f, .16f)));
            }
            // Four RCS quads around the forward cabin: forward-facing nozzles brake the approach, side nozzles translate.
            for (int q = 0; q < 4; q++)
            {
                float angle = q * 90f * Mathf.Deg2Rad;
                var radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                Vector3 quadPos = radial * 1.7f + new Vector3(0, 0, 5.0f);
                Box("RcsQuad", body, dark, quadPos, Quaternion.LookRotation(Vector3.forward, radial), new Vector3(.34f, .26f, .46f));
                retroPlumes.Add(Plume("RetroPlume", body, quadPos + new Vector3(0, 0, .26f), Quaternion.identity, .16f));
                sidePlumes.Add(Plume("SidePlume", body, quadPos + radial * .16f, Quaternion.LookRotation(radial), .1f));
            }
            // Docking lights around the collar: amber until capture, then cyan.
            for (int i = 0; i < 8; i++)
            {
                float angle = (i * 45f + 22.5f) * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Cos(angle) * 1.12f, Mathf.Sin(angle) * 1.12f, 7.38f);
                collarLamps.Add(Box("CollarLamp", body, amber, pos, Quaternion.identity, new Vector3(.1f, .1f, .08f)));
            }
            blinkLamps.Add(Box("TopStrobe", body, cyan, new Vector3(0, 1.62f, 2.2f), Quaternion.identity, new Vector3(.14f, .1f, .14f)));
            // Floodlight housing on the nose, with a real (shadowless) spot light.
            Box("FloodHousing", body, dark, new Vector3(0, 1.05f, 6.55f), Quaternion.identity, new Vector3(.42f, .26f, .3f));
            Box("FloodLens", body, window, new Vector3(0, 1.05f, 6.71f), Quaternion.identity, new Vector3(.32f, .18f, .02f));
            floodlight = AddLight("Floodlight", body, LightType.Spot, new Vector3(0, 1.05f, 6.8f), new Color(1f, .96f, .88f), 85f);
            floodlight.spotAngle = 30f;
            burnLight = AddLight("BurnLight", body, LightType.Point, new Vector3(0, 0, 6.2f), new Color(.72f, .9f, 1f), 9f);
            Pose(0f, 0f, false, 0f, false);
        }

        /// <summary>Thruster plumes, captured docking lights and blinking running lights. Values are clamped.</summary>
        public void Pose(float retroBurn, float sidePuff, bool captured, float flood, bool steadyLights, float time = 0f)
        {
            Build();
            retroBurn = Mathf.Clamp01(retroBurn); sidePuff = Mathf.Clamp01(sidePuff); flood = Mathf.Clamp01(flood);
            foreach (Transform p in retroPlumes) SetPlume(p, retroBurn, 1.6f);
            foreach (Transform p in sidePlumes) SetPlume(p, sidePuff, .7f);
            foreach (Renderer lamp in collarLamps) if (lamp) lamp.sharedMaterial = captured ? cyan : amber;
            bool blinkOn = steadyLights || Mathf.Repeat(time, 1.2f) < .22f;
            foreach (Renderer lamp in blinkLamps) if (lamp) lamp.enabled = blinkOn;
            if (floodlight) { floodlight.intensity = 7f * flood; floodlight.enabled = flood > .001f; }
            if (burnLight) { burnLight.intensity = 3.5f * Mathf.Max(retroBurn, sidePuff * .6f); burnLight.enabled = burnLight.intensity > .001f; }
        }

        private static void SetPlume(Transform p, float value, float length)
        {
            if (!p) return;
            bool on = value > .01f;
            if (p.gameObject.activeSelf != on) p.gameObject.SetActive(on);
            if (on) p.localScale = new Vector3(.55f + .45f * value, .55f + .45f * value, Mathf.Max(.01f, length * value));
        }

        private Transform Group(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private Renderer Box(string name, Transform parent, Material material, Vector3 position, Quaternion rotation, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(position, rotation);
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = CubeMesh;
            return Finish(go.AddComponent<MeshRenderer>(), material);
        }

        private void Lathe(string name, Transform parent, Material material, float[] zs, float[] radii, int segments)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = LatheMesh(zs, radii, segments);
            Finish(go.AddComponent<MeshRenderer>(), material);
        }

        private Transform Plume(string name, Transform parent, Vector3 position, Quaternion rotation, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(position, rotation);
            // Unit cone along +Z (base radius at z=0, tip at z=1), scaled by the burn.
            go.AddComponent<MeshFilter>().sharedMesh = LatheMesh(new[] { 0f, 1f }, new[] { radius, 0f }, 12);
            Finish(go.AddComponent<MeshRenderer>(), plume);
            go.SetActive(false);
            return go.transform;
        }

        private static Light AddLight(string name, Transform parent, LightType type, Vector3 position, Color color, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var light = go.AddComponent<Light>();
            light.type = type; light.color = color; light.range = range; light.intensity = 0f;
            light.shadows = LightShadows.None; light.enabled = false;
            return light;
        }

        private static Renderer Finish(MeshRenderer renderer, Material material)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }

        private static Mesh cube;
        private static Mesh CubeMesh
        {
            get
            {
                if (cube) return cube;
                cube = new Mesh { name = "RescueCraftV01 unit box", hideFlags = HideFlags.DontSave };
                var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
                Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
                foreach (Vector3 normal in axes)
                {
                    Vector3 u = Mathf.Abs(normal.y) > .5f ? Vector3.right : Vector3.up;
                    Vector3 w = Vector3.Cross(normal, u);
                    int b = v.Count;
                    v.Add((normal - u - w) * .5f); v.Add((normal - u + w) * .5f); v.Add((normal + u + w) * .5f); v.Add((normal + u - w) * .5f);
                    for (int k = 0; k < 4; k++) n.Add(normal);
                    t.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
                }
                cube.SetVertices(v); cube.SetNormals(n); cube.SetTriangles(FixWinding(v, n, t), 0);
                cube.RecalculateBounds();
                return cube;
            }
        }

        /// <summary>Surface of revolution about +Z through (z, r) profile points, hard-edged per segment, capped.</summary>
        public static Mesh LatheMesh(float[] zs, float[] radii, int segments)
        {
            var mesh = new Mesh { name = "RescueCraftV01 lathe", hideFlags = HideFlags.DontSave };
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            segments = Mathf.Max(3, segments);
            for (int i = 0; i + 1 < zs.Length; i++)
            {
                float dz = zs[i + 1] - zs[i], dr = radii[i + 1] - radii[i];
                var slope = new Vector2(dz, -dr).normalized; // radial, axial normal components
                int b = v.Count;
                for (int s = 0; s <= segments; s++)
                {
                    float a = s * Mathf.PI * 2f / segments;
                    var radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                    Vector3 normal = (radial * slope.x + Vector3.forward * slope.y).normalized;
                    v.Add(radial * radii[i] + Vector3.forward * zs[i]); n.Add(normal);
                    v.Add(radial * radii[i + 1] + Vector3.forward * zs[i + 1]); n.Add(normal);
                }
                for (int s = 0; s < segments; s++)
                {
                    int a0 = b + s * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                    t.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
                }
            }
            Cap(v, n, t, zs[0], radii[0], Vector3.back, segments);
            Cap(v, n, t, zs[zs.Length - 1], radii[radii.Length - 1], Vector3.forward, segments);
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTriangles(FixWinding(v, n, t), 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void Cap(List<Vector3> v, List<Vector3> n, List<int> t, float z, float r, Vector3 normal, int segments)
        {
            if (r <= .0001f) return;
            int centre = v.Count;
            v.Add(Vector3.forward * z); n.Add(normal);
            for (int s = 0; s <= segments; s++)
            {
                float a = s * Mathf.PI * 2f / segments;
                v.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, z)); n.Add(normal);
            }
            for (int s = 0; s < segments; s++) t.AddRange(new[] { centre, centre + 1 + s, centre + 2 + s });
        }

        /// <summary>Orients every triangle so its face normal agrees with its vertex normals (outward, front faces).</summary>
        private static List<int> FixWinding(List<Vector3> v, List<Vector3> n, List<int> t)
        {
            for (int i = 0; i + 2 < t.Count; i += 3)
            {
                Vector3 face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                Vector3 normal = n[t[i]] + n[t[i + 1]] + n[t[i + 2]];
                // Unity front faces wind clockwise as seen from the front; Cross(b - a, c - a) then points to the viewer.
                if (Vector3.Dot(face, normal) < 0f) { int swap = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = swap; }
            }
            return t;
        }
    }
}
