using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OutOfReach.Presentation.LaunchGrassV04
{
    /// <summary>
    /// LAUNCH-GRASS-V04: a passive, GPU-instanced field of tall golden pampas grass around the stationary launch
    /// site. Instances are generated deterministically at enable from the real LT_TerrainInner mesh (heights) and
    /// the authored clear areas (apron, crawlerway, plume corridor, facility footprints), stored once in a
    /// structured buffer and drawn per camera with Graphics.RenderMeshPrimitives per visible chunk:
    /// full clumps near the camera, three-quad cards further out, counts thinned smoothly with distance
    /// (instance rank vs. kept fraction, in the shader) and faded out before <see cref="maxDrawDistance"/>.
    /// No colliders, no gameplay state, no clock: wind uses shader time; the thruster gust is written by
    /// <see cref="LaunchGrassGustPresenter"/> from the actual departure playhead.
    /// Place this component on a child of "Launch site - stationary" with an identity local transform.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class PampasGrassField : MonoBehaviour
    {
        [SerializeField] private Material material;
        [SerializeField, Tooltip("The real LT_TerrainInner ground (readable mesh); heights are sampled from it.")]
        private MeshFilter ground;
        [Header("Field (site-local metres)")]
        [SerializeField] private Vector2 centre = new Vector2(0f, 5f);
        [SerializeField, Range(100f, 600f)] private float outerRadius = 430f;
        [SerializeField, Range(.7f, 3f)] private float spacing = 1.15f;
        [SerializeField, Range(8f, 64f)] private float chunkSize = 24f;
        [SerializeField, Tooltip("x0, z0, x1, z1 rectangles kept clear, site-local; soft edge of `clearRamp` metres.")]
        private Vector4[] clearAreas =
        {
            new Vector4(-78f, -68f, 78f, 78f),     // apron and its mown shoulder
            new Vector4(-24f, -915f, 24f, -55f),   // crawlerway
            new Vector4(-30f, 62f, 30f, 335f),     // flame trench exit and plume corridor
        };
        [SerializeField, Tooltip("Facility footprints found by the installer (x0, z0, x1, z1, site-local).")]
        private Vector4[] footprintClearAreas = Array.Empty<Vector4>();
        [SerializeField, Range(0f, 30f)] private float clearRamp = 10f;
        [SerializeField, Range(0f, 1f), Tooltip("Share of the field thinned into clearings by broad noise.")]
        private float clearings = .18f;
        [SerializeField, Range(0f, 80f), Tooltip("Steeper slopes stay bare (degrees).")]
        private float maxSlope = 38f;
        [SerializeField] private int seed = 1977;
        [Header("Distance and LOD (metres)")]
        [SerializeField, Range(50f, 600f)] private float maxDrawDistance = 260f;
        [SerializeField, Range(5f, 80f)] private float nearMeshDistance = 32f;
        [SerializeField, Range(1f, 12f)] private float lodBand = 4f;
        [SerializeField, Range(.05f, 1f)] private float farCountFraction = .2f;
        [Header("Reduced effects")]
        [SerializeField, Range(.1f, 1f)] private float reducedDrawDistance = .65f;
        [Header("Wind (shared globals)")]
        [SerializeField] private Vector2 windDirection = new Vector2(.82f, .57f);
        [SerializeField, Range(0f, .6f)] private float swayAngle = .12f;
        [SerializeField, Range(0f, 1f)] private float gustAngle = .38f;
        [SerializeField, Range(.5f, 20f)] private float gustSpeed = 6.5f;
        [SerializeField, Range(4f, 120f)] private float gustWavelength = 34f;

        private static readonly int InstancesId = Shader.PropertyToID("_OOR_GrassInstances");
        private static readonly int ChunkStartId = Shader.PropertyToID("_ChunkStart");
        private static readonly int ChunkCountId = Shader.PropertyToID("_ChunkCount");
        private static readonly int IsCardId = Shader.PropertyToID("_IsCard");
        private static readonly int WindId = Shader.PropertyToID("_OOR_GrassWind");
        private static readonly int WindWaveId = Shader.PropertyToID("_OOR_GrassWindWave");
        private static readonly int LodId = Shader.PropertyToID("_OOR_GrassLod");

        private struct Chunk { public int Start, Count; public Bounds Bounds; }

        private GraphicsBuffer buffer;
        private Chunk[] chunks = Array.Empty<Chunk>();
        private Mesh nearMesh, farMesh;
        private readonly List<MaterialPropertyBlock> blocks = new List<MaterialPropertyBlock>();
        private readonly Plane[] planes = new Plane[6];
        private bool reducedEffects;

        public int InstanceCount { get; private set; }
        public int ChunkCount => chunks.Length;
        public int NearTriangles => PampasGrassMeshes.TriangleCount(nearMesh);
        public int CardTriangles => PampasGrassMeshes.TriangleCount(farMesh);
        public float BuildMilliseconds { get; private set; }
        public bool IsReady => buffer != null && chunks.Length > 0 && material;
        public bool ReducedEffects => reducedEffects;
        /// <summary>Last frame's draw statistics for the most recent game/scene camera (diagnostics only).</summary>
        public int LastDrawCalls { get; private set; }
        public int LastNearInstances { get; private set; }
        public int LastCardInstances { get; private set; }
        public long LastTriangles { get; private set; }

        public void SetReducedEffects(bool reduced) => reducedEffects = reduced;

        /// <summary>Authoring entry point used by the editor installer.</summary>
        public void Configure(Material grass, MeshFilter groundMesh, Vector4[] extraClearAreas)
        {
            material = grass; ground = groundMesh;
            footprintClearAreas = extraClearAreas ?? Array.Empty<Vector4>();
            if (isActiveAndEnabled) { Release(); Build(); }
        }

        private void OnEnable()
        {
            Build();
            RenderPipelineManager.beginCameraRendering += Draw;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Draw;
            Release();
        }

        private bool rebuildRequested;
        private void OnValidate() => rebuildRequested = true;

        private void Release()
        {
            buffer?.Release(); buffer = null;
            chunks = Array.Empty<Chunk>();
            DestroyOwned(nearMesh); DestroyOwned(farMesh); nearMesh = farMesh = null;
            InstanceCount = 0;
        }

        private static void DestroyOwned(UnityEngine.Object o)
        {
            if (!o) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        // ---------------- generation ----------------

        private void Build()
        {
            if (!material || !ground || !ground.sharedMesh) return;
            if (!SystemInfo.supportsComputeShaders && SystemInfo.graphicsShaderLevel < 45) return;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var heights = new HeightGrid(ground, transform, centre, outerRadius + chunkSize, 2f);
            if (!heights.Valid) { Debug.LogWarning("LAUNCH_GRASS_V04: ground mesh is not readable; field not built.", this); return; }

            float cosSlope = Mathf.Cos(maxSlope * Mathf.Deg2Rad);
            int cells = Mathf.CeilToInt(outerRadius * 2f / spacing);
            int chunkCells = Mathf.CeilToInt(outerRadius * 2f / chunkSize);
            var perChunk = new Dictionary<int, List<Vector4>>();
            Matrix4x4 toWorld = transform.localToWorldMatrix;
            for (int j = 0; j < cells; j++)
            for (int i = 0; i < cells; i++)
            {
                float hx = Hash(i, j, seed), hz = Hash(i, j, seed + 11);
                var p = new Vector2(centre.x - outerRadius + (i + hx) * spacing, centre.y - outerRadius + (j + hz) * spacing);
                if ((p - centre).sqrMagnitude > outerRadius * outerRadius) continue;
                float keep = 1f;
                foreach (Vector4 r in clearAreas) keep *= 1f - Box(p, r, clearRamp);
                foreach (Vector4 r in footprintClearAreas) keep *= 1f - Box(p, r, 3f);
                float n = ValueNoise(p / 41f + new Vector2(seed * .01f, 3.1f)) * .7f + ValueNoise(p / 12f + new Vector2(7.7f, seed * .02f)) * .3f;
                keep *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(clearings - .12f, clearings + .14f, n));
                if (Hash(i, j, seed + 29) >= keep) continue;
                if (!heights.Sample(p, out float y, out float up) || up < cosSlope) continue;
                Vector3 world = toWorld.MultiplyPoint3x4(new Vector3(p.x, y - .04f, p.y));
                int cx = Mathf.Clamp(Mathf.FloorToInt((p.x - centre.x + outerRadius) / chunkSize), 0, chunkCells - 1);
                int cz = Mathf.Clamp(Mathf.FloorToInt((p.y - centre.y + outerRadius) / chunkSize), 0, chunkCells - 1);
                int key = cz * chunkCells + cx;
                if (!perChunk.TryGetValue(key, out var list)) perChunk.Add(key, list = new List<Vector4>(512));
                list.Add(new Vector4(world.x, world.y, world.z, Hash(i, j, seed + 47)));
            }

            var all = new List<Vector4>(perChunk.Count * 400);
            var built = new List<Chunk>(perChunk.Count);
            var rng = new System.Random(seed);
            var keys = new List<int>(perChunk.Keys); keys.Sort();
            foreach (int key in keys)
            {
                List<Vector4> list = perChunk[key];
                for (int k = list.Count - 1; k > 0; k--) { int s = rng.Next(k + 1); (list[k], list[s]) = (list[s], list[k]); }
                var bounds = new Bounds(list[0], Vector3.zero);
                foreach (Vector4 v in list) bounds.Encapsulate(v);
                bounds.Expand(new Vector3(4f, 0f, 4f));
                bounds.max += Vector3.up * 2.4f;
                built.Add(new Chunk { Start = all.Count, Count = list.Count, Bounds = bounds });
                all.AddRange(list);
            }
            if (all.Count == 0) return;
            buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, all.Count, 16) { name = "LaunchGrassV04 instances" };
            buffer.SetData(all);
            chunks = built.ToArray();
            nearMesh = PampasGrassMeshes.BuildNearClump();
            farMesh = PampasGrassMeshes.BuildFarCard();
            InstanceCount = all.Count;
            BuildMilliseconds = (float)watch.Elapsed.TotalMilliseconds;
            Debug.Log("LAUNCH_GRASS_V04_READY instances=" + InstanceCount + " chunks=" + chunks.Length + " nearTris=" + NearTriangles
                + " cardTris=" + CardTriangles + " bufferKB=" + (InstanceCount * 16 / 1024) + " buildMs=" + BuildMilliseconds.ToString("F1"), this);
        }

        private static float Box(Vector2 p, Vector4 r, float ramp)
        {
            float dx = Mathf.Max(Mathf.Max(r.x - p.x, p.x - r.z), 0f), dz = Mathf.Max(Mathf.Max(r.y - p.y, p.y - r.w), 0f);
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            return ramp <= 0f ? (d <= 0f ? 1f : 0f) : 1f - Mathf.SmoothStep(0f, 1f, d / ramp);
        }

        private static float Hash(int x, int y, int s)
        {
            unchecked
            {
                uint h = (uint)x * 0x8da6b343u ^ (uint)y * 0xd8163841u ^ (uint)s * 0xcb1ab31fu;
                h ^= h >> 15; h *= 0x2c1b3c6du; h ^= h >> 12; h *= 0x297a2d39u; h ^= h >> 15;
                return (h & 0xffffff) / 16777216f;
            }
        }

        private static float ValueNoise(Vector2 p)
        {
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y);
            float fx = p.x - x, fy = p.y - y;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = Hash(x, y, 5), b = Hash(x + 1, y, 5), c = Hash(x, y + 1, 5), d = Hash(x + 1, y + 1, 5);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Ground heights (field-local) rasterised once from the ground mesh triangles.</summary>
        private sealed class HeightGrid
        {
            private readonly float[] h; private readonly float[] up;
            private readonly int size; private readonly float cell; private readonly Vector2 origin;
            public readonly bool Valid;

            public HeightGrid(MeshFilter filter, Transform field, Vector2 centre, float radius, float cellSize)
            {
                Mesh mesh = filter.sharedMesh;
                if (!mesh.isReadable) return;
                cell = cellSize; size = Mathf.CeilToInt(radius * 2f / cell) + 2;
                origin = centre - new Vector2(radius, radius);
                h = new float[size * size]; up = new float[size * size];
                for (int i = 0; i < h.Length; i++) h[i] = float.NaN;
                Matrix4x4 m = field.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Vector3[] v = mesh.vertices;
                for (int i = 0; i < v.Length; i++) v[i] = m.MultiplyPoint3x4(v[i]);
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    if (mesh.GetTopology(sub) != MeshTopology.Triangles) continue;
                    int[] t = mesh.GetTriangles(sub);
                    for (int k = 0; k < t.Length; k += 3) Raster(v[t[k]], v[t[k + 1]], v[t[k + 2]]);
                }
                Valid = true;
            }

            private void Raster(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 nrm = Vector3.Cross(b - a, c - a);
                if (nrm.y < 0) nrm = -nrm;
                float len = nrm.magnitude; if (len < 1e-8f) return;
                float upness = nrm.y / len;
                int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - origin.x) / cell));
                int x1 = Mathf.Min(size - 1, Mathf.CeilToInt((Mathf.Max(a.x, Mathf.Max(b.x, c.x)) - origin.x) / cell));
                int z0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.z, Mathf.Min(b.z, c.z)) - origin.y) / cell));
                int z1 = Mathf.Min(size - 1, Mathf.CeilToInt((Mathf.Max(a.z, Mathf.Max(b.z, c.z)) - origin.y) / cell));
                float det = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(det) < 1e-9f) return;
                for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    float px = origin.x + x * cell, pz = origin.y + z * cell;
                    float w0 = ((b.z - c.z) * (px - c.x) + (c.x - b.x) * (pz - c.z)) / det;
                    float w1 = ((c.z - a.z) * (px - c.x) + (a.x - c.x) * (pz - c.z)) / det;
                    float w2 = 1f - w0 - w1;
                    if (w0 < -1e-4f || w1 < -1e-4f || w2 < -1e-4f) continue;
                    float y = w0 * a.y + w1 * b.y + w2 * c.y;
                    int idx = z * size + x;
                    if (float.IsNaN(h[idx]) || y > h[idx]) { h[idx] = y; up[idx] = upness; }
                }
            }

            public bool Sample(Vector2 p, out float y, out float upness)
            {
                y = 0; upness = 0;
                float gx = (p.x - origin.x) / cell, gz = (p.y - origin.y) / cell;
                int x = Mathf.FloorToInt(gx), z = Mathf.FloorToInt(gz);
                if (x < 0 || z < 0 || x >= size - 1 || z >= size - 1) return false;
                float fx = gx - x, fz = gz - z;
                int i00 = z * size + x, i10 = i00 + 1, i01 = i00 + size, i11 = i01 + 1;
                float a = h[i00], b = h[i10], c = h[i01], d = h[i11];
                if (float.IsNaN(a) || float.IsNaN(b) || float.IsNaN(c) || float.IsNaN(d)) return false;
                y = Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fz);
                upness = Mathf.Min(Mathf.Min(up[i00], up[i10]), Mathf.Min(up[i01], up[i11]));
                return true;
            }
        }

        // ---------------- drawing ----------------

        private void Update()
        {
            if (rebuildRequested && isActiveAndEnabled) { rebuildRequested = false; Release(); Build(); }
            Vector2 wd = windDirection.sqrMagnitude > 1e-6f ? windDirection.normalized : Vector2.right;
            Shader.SetGlobalVector(WindId, new Vector4(wd.x, wd.y, swayAngle * (reducedEffects ? .5f : 1f), gustAngle));
            Shader.SetGlobalVector(WindWaveId, new Vector4(gustSpeed, gustWavelength, reducedEffects ? 1f : 0f, 0f));
        }

        private void Draw(ScriptableRenderContext context, Camera camera)
        {
            if (!isActiveAndEnabled) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) Update();
#endif
            if (!IsReady) return;
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;
            if ((camera.cullingMask & (1 << gameObject.layer)) == 0) return;
            float maxDistance = Mathf.Min(maxDrawDistance * (reducedEffects ? reducedDrawDistance : 1f), camera.farClipPlane);
            float near = Mathf.Min(nearMeshDistance, maxDistance * .5f);
            Shader.SetGlobalVector(LodId, new Vector4(near, lodBand, maxDistance, farCountFraction));
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            Vector3 eye = camera.transform.position;
            var rp = new RenderParams(material)
            {
                layer = gameObject.layer, camera = camera, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true,
                lightProbeUsage = LightProbeUsage.Off, reflectionProbeUsage = ReflectionProbeUsage.Off,
                motionVectorMode = MotionVectorGenerationMode.Camera,
            };
            int used = 0, nearCount = 0, cardCount = 0; long tris = 0;
            int nearTris = NearTriangles, cardTris = CardTriangles;
            float reducedCount = reducedEffects ? .55f : 1f;
            for (int c = 0; c < chunks.Length; c++)
            {
                Bounds b = chunks[c].Bounds;
                float dMin = Mathf.Sqrt(b.SqrDistance(eye));
                if (dMin > maxDistance) continue;
                if (!GeometryUtility.TestPlanesAABB(planes, b)) continue;
                float dMax = Vector3.Distance(eye, b.center) + b.extents.magnitude;
                float keep = KeepFraction(dMin, near, maxDistance) * reducedCount;
                int count = Mathf.Clamp(Mathf.CeilToInt(chunks[c].Count * Mathf.Min(1f, keep + .07f)), 1, chunks[c].Count);
                if (dMin < near + lodBand)
                {
                    Submit(ref rp, ref used, c, count, false, nearMesh);
                    nearCount += count; tris += (long)count * nearTris;
                }
                if (dMax > near - lodBand)
                {
                    Submit(ref rp, ref used, c, count, true, farMesh);
                    cardCount += count; tris += (long)count * cardTris;
                }
            }
            LastDrawCalls = used; LastNearInstances = nearCount; LastCardInstances = cardCount; LastTriangles = tris;
        }

        private float KeepFraction(float d, float near, float max)
        {
            float t = Mathf.Clamp01((d - near) / Mathf.Max(max - near, 1f));
            return Mathf.Lerp(1f, farCountFraction, t * t * (3f - 2f * t));
        }

        private void Submit(ref RenderParams rp, ref int used, int chunk, int count, bool card, Mesh mesh)
        {
            if (used == blocks.Count) blocks.Add(new MaterialPropertyBlock());
            MaterialPropertyBlock block = blocks[used++];
            block.SetFloat(ChunkStartId, chunks[chunk].Start);
            block.SetFloat(ChunkCountId, chunks[chunk].Count);
            block.SetFloat(IsCardId, card ? 1f : 0f);
            block.SetBuffer(InstancesId, buffer);
            rp.matProps = block;
            rp.worldBounds = chunks[chunk].Bounds;
            Graphics.RenderMeshPrimitives(rp, mesh, 0, count);
        }
    }
}
