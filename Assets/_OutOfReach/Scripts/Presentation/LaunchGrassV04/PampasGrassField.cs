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
    /// structured buffer and drawn per camera with Graphics.RenderMeshPrimitives per visible chunk, in three levels
    /// of detail that hand over by dither per instance: the modeled clump (PampasClumpV04.fbx, LOD0) close to the
    /// camera, its simpler model (LOD1) out to <see cref="modelDistance"/>, then cards whose count thins smoothly
    /// with distance (instance rank vs. kept fraction, in the shader) and fades out before <see cref="maxDrawDistance"/>.
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
        [SerializeField, Tooltip("PampasClumpV04.fbx: PampasClump_LOD0 (detailed model, near the camera).")]
        private Mesh detailMesh;
        [SerializeField, Tooltip("PampasClumpV04.fbx: PampasClump_LOD1 (simpler model, mid range).")]
        private Mesh simpleMesh;
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
        private float clearings = .12f;
        [SerializeField, Range(0f, 80f), Tooltip("Steeper slopes stay bare (degrees).")]
        private float maxSlope = 38f;
        [SerializeField] private int seed = 1977;
        [Header("Distance and LOD (metres)")]
        [SerializeField, Range(50f, 600f)] private float maxDrawDistance = 440f;
        [SerializeField, Range(4f, 40f), Tooltip("Detailed model (LOD0) up to this distance.")]
        private float detailDistance = 11f;
        [SerializeField, Range(.5f, 8f)] private float detailBand = 2f;
        [SerializeField, Range(10f, 120f), Tooltip("Simpler model (LOD1) up to this distance; cards beyond.")]
        private float modelDistance = 46f;
        [SerializeField, Range(1f, 12f)] private float modelBand = 4f;
        [SerializeField, Range(.05f, 1f)] private float farCountFraction = .3f;
        [SerializeField, Range(.2f, 1f), Tooltip("Far count scale for a camera at eye level, where nearer grass hides most far cards.")]
        private float lowCameraFarScale = .5f;
        [Header("Reduced effects")]
        [SerializeField, Range(.1f, 1f)] private float reducedDrawDistance = .65f;
        [Header("Wind (shared globals)")]
        [SerializeField] private Vector2 windDirection = new Vector2(.82f, .57f);
        [SerializeField, Range(0f, .6f)] private float swayAngle = .16f;
        [SerializeField, Range(0f, 1f)] private float gustAngle = .62f;
        [SerializeField, Range(.5f, 20f)] private float gustSpeed = 4.2f;
        [SerializeField, Range(4f, 120f)] private float gustWavelength = 62f;

        private static readonly int InstancesId = Shader.PropertyToID("_OOR_GrassInstances");
        private static readonly int ChunkStartId = Shader.PropertyToID("_ChunkStart");
        private static readonly int ChunkCountId = Shader.PropertyToID("_ChunkCount");
        private static readonly int LodLevelId = Shader.PropertyToID("_Lod");
        private static readonly int Lod2Id = Shader.PropertyToID("_OOR_GrassLod2");
        private static readonly int WindId = Shader.PropertyToID("_OOR_GrassWind");
        private static readonly int WindWaveId = Shader.PropertyToID("_OOR_GrassWindWave");
        private static readonly int LodId = Shader.PropertyToID("_OOR_GrassLod");

        /// <summary>A 24 m chunk: its instances shuffled (cards draw the first N), plus its range of 8 m cells.</summary>
        private struct Chunk { public int Start, Count, CellStart, CellCount; public Bounds Bounds; }
        /// <summary>An 8 m cell: a second, spatially grouped copy of its instances for the modeled levels of detail,
        /// so the heavy meshes are only submitted for clumps that are actually near the camera.</summary>
        private struct Cell { public int Start, Count; public Bounds Bounds; }
        private const int CellsPerChunkSide = 3;

        private GraphicsBuffer buffer;
        private Chunk[] chunks = Array.Empty<Chunk>();
        private Cell[] cells = Array.Empty<Cell>();
        private Mesh farMesh;
        private bool warnedMissingModel;
        private readonly List<MaterialPropertyBlock> blocks = new List<MaterialPropertyBlock>();
        private readonly Plane[] planes = new Plane[6];
        private bool reducedEffects;
        private float meanGroundY, farFraction;

        public int InstanceCount { get; private set; }
        public int ChunkCount => chunks.Length;
        public int DetailTriangles => PampasGrassMeshes.TriangleCount(detailMesh);
        public int ModelTriangles => PampasGrassMeshes.TriangleCount(simpleMesh);
        public int CardTriangles => PampasGrassMeshes.TriangleCount(farMesh);
        public bool HasModel => PampasGrassMeshes.HasClumpChannels(detailMesh) && PampasGrassMeshes.HasClumpChannels(simpleMesh);
        public float BuildMilliseconds { get; private set; }
        public bool IsReady => buffer != null && chunks.Length > 0 && material;
        public bool ReducedEffects => reducedEffects;
        /// <summary>Mean world height of the field's ground (the launch haze uses it as its datum).</summary>
        public float MeanGroundY => meanGroundY;
        /// <summary>Last frame's draw statistics for the most recent game/scene camera (diagnostics only).</summary>
        public int LastDrawCalls { get; private set; }
        public int LastDetailInstances { get; private set; }
        public int LastModelInstances { get; private set; }
        public int LastCardInstances { get; private set; }
        public long LastTriangles { get; private set; }

        public void SetReducedEffects(bool reduced) => reducedEffects = reduced;

        /// <summary>Edit-mode preview / evidence capture: freezes the wind at a given time (negative = live time).</summary>
        public void PreviewWindTime(float seconds) => previewWindTime = seconds;
        private float previewWindTime = -1f;

        /// <summary>Authoring entry point used by the editor installer.</summary>
        public void Configure(Material grass, MeshFilter groundMesh, Vector4[] extraClearAreas, Mesh detail, Mesh simple)
        {
            material = grass; ground = groundMesh; detailMesh = detail; simpleMesh = simple;
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
            pending = null;
            buffer?.Release(); buffer = null;
            chunks = Array.Empty<Chunk>(); cells = Array.Empty<Cell>();
            DestroyOwned(farMesh); farMesh = null;
            InstanceCount = 0;
        }

        private static void DestroyOwned(UnityEngine.Object o)
        {
            if (!o) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        // ---------------- generation ----------------

        private sealed class Generated
        {
            public Vector4[] Instances; public Chunk[] Chunks; public Cell[] Cells; public int Count; public double Milliseconds; public string Error;
        }
        private System.Threading.Tasks.Task<Generated> pending;

        /// <summary>Reads the ground mesh on the main thread, then generates the instances: on a worker thread in
        /// Play mode (no load hitch; the field appears when ready), synchronously in Edit mode and the installer.</summary>
        private void Build()
        {
            if (!material || !ground || !ground.sharedMesh) return;
            if (SystemInfo.graphicsShaderLevel < 45) return;
            Mesh mesh = ground.sharedMesh;
            if (!mesh.isReadable) { Debug.LogWarning("LAUNCH_GRASS_V04: ground mesh is not readable; field not built.", this); return; }
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Matrix4x4 m = transform.worldToLocalMatrix * ground.transform.localToWorldMatrix;
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = m.MultiplyPoint3x4(vertices[i]);
            var triangles = new List<int>();
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
                if (mesh.GetTopology(sub) == MeshTopology.Triangles) triangles.AddRange(mesh.GetTriangles(sub));
            int[] tris = triangles.ToArray();
            Matrix4x4 toWorld = transform.localToWorldMatrix;
            Vector4[] clear = clearAreas ?? Array.Empty<Vector4>(), footprints = footprintClearAreas ?? Array.Empty<Vector4>();
            Vector2 c = centre; float radius = outerRadius, step = spacing, chunk = chunkSize, ramp = clearRamp, holes = clearings;
            float cosSlope = Mathf.Cos(maxSlope * Mathf.Deg2Rad); int fieldSeed = seed;
            Generated Run()
            {
                try { return Generate(vertices, tris, toWorld, clear, footprints, c, radius, step, chunk, ramp, holes, cosSlope, fieldSeed, watch); }
                catch (Exception e) { return new Generated { Error = e.ToString() }; }
            }
            if (Application.isPlaying) pending = System.Threading.Tasks.Task.Run(Run);
            else Finish(Run());
        }

        private void Finish(Generated g)
        {
            pending = null;
            if (g.Error != null) { Debug.LogError("LAUNCH_GRASS_V04: generation failed: " + g.Error, this); return; }
            if (g.Instances.Length == 0) return;
            buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, g.Instances.Length, 16) { name = "LaunchGrassV04 instances" };
            buffer.SetData(g.Instances);
            chunks = g.Chunks; cells = g.Cells;
            double ground = 0; foreach (Chunk c in chunks) ground += c.Bounds.min.y;
            meanGroundY = (float)(ground / chunks.Length);
            farMesh = PampasGrassMeshes.BuildFarCard();
            InstanceCount = g.Count;
            BuildMilliseconds = (float)g.Milliseconds;
            Debug.Log("LAUNCH_GRASS_V04_READY instances=" + InstanceCount + " chunks=" + chunks.Length + " detailTris=" + DetailTriangles
                + " modelTris=" + ModelTriangles + " cardTris=" + CardTriangles + " cells=" + cells.Length + " bufferKB=" + (g.Instances.Length * 16 / 1024)
                + " generateMs=" + BuildMilliseconds.ToString("F1") + " async=" + Application.isPlaying, this);
        }

        private static Generated Generate(Vector3[] vertices, int[] tris, Matrix4x4 toWorld, Vector4[] clearAreas, Vector4[] footprints,
            Vector2 centre, float outerRadius, float spacing, float chunkSize, float clearRamp, float clearings, float cosSlope, int seed,
            System.Diagnostics.Stopwatch watch)
        {
            var heights = new HeightGrid(vertices, tris, centre, outerRadius + chunkSize, 2f);
            int cells = Mathf.CeilToInt(outerRadius * 2f / spacing);
            int chunkCells = Mathf.CeilToInt(outerRadius * 2f / chunkSize);
            var perChunk = new Dictionary<int, List<Vector4>>();
            var perChunkCell = new Dictionary<int, List<byte>>();
            for (int j = 0; j < cells; j++)
            for (int i = 0; i < cells; i++)
            {
                float hx = Hash(i, j, seed), hz = Hash(i, j, seed + 11);
                var p = new Vector2(centre.x - outerRadius + (i + hx) * spacing, centre.y - outerRadius + (j + hz) * spacing);
                if ((p - centre).sqrMagnitude > outerRadius * outerRadius) continue;
                float keep = 1f;
                foreach (Vector4 r in clearAreas) keep *= 1f - Box(p, r, clearRamp);
                foreach (Vector4 r in footprints) keep *= 1f - Box(p, r, 3f);
                if (keep <= 0f) continue;
                // Feathered, irregular outer edge instead of a circle.
                float edgeRadius = (p - centre).magnitude + (ValueNoise(p / 37f + new Vector2(11.3f, 4.1f)) - .5f) * 90f;
                keep *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(outerRadius * .68f, outerRadius, edgeRadius));
                float n = ValueNoise(p / 41f + new Vector2(seed * .01f, 3.1f)) * .7f + ValueNoise(p / 12f + new Vector2(7.7f, seed * .02f)) * .3f;
                keep *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(clearings - .12f, clearings + .14f, n));
                if (Hash(i, j, seed + 29) >= keep) continue;
                if (!heights.Sample(p, out float y, out float up) || up < cosSlope) continue;
                Vector3 world = toWorld.MultiplyPoint3x4(new Vector3(p.x, y - .04f, p.y));
                int cx = Mathf.Clamp(Mathf.FloorToInt((p.x - centre.x + outerRadius) / chunkSize), 0, chunkCells - 1);
                int cz = Mathf.Clamp(Mathf.FloorToInt((p.y - centre.y + outerRadius) / chunkSize), 0, chunkCells - 1);
                int key = cz * chunkCells + cx;
                if (!perChunk.TryGetValue(key, out var list))
                {
                    perChunk.Add(key, list = new List<Vector4>(640));
                    perChunkCell.Add(key, new List<byte>(640));
                }
                list.Add(new Vector4(world.x, world.y, world.z, Hash(i, j, seed + 47)));
                float lx = (p.x - centre.x + outerRadius) / chunkSize - cx, lz = (p.y - centre.y + outerRadius) / chunkSize - cz;
                int fx = Mathf.Clamp((int)(lx * CellsPerChunkSide), 0, CellsPerChunkSide - 1);
                int fz = Mathf.Clamp((int)(lz * CellsPerChunkSide), 0, CellsPerChunkSide - 1);
                perChunkCell[key].Add((byte)(fz * CellsPerChunkSide + fx));
            }

            var all = new List<Vector4>(perChunk.Count * 500);        // first copy: per chunk, shuffled (cards)
            var grouped = new List<Vector4>(perChunk.Count * 500);    // second copy: per 8 m cell (modeled levels)
            var built = new List<Chunk>(perChunk.Count);
            var builtCells = new List<Cell>(perChunk.Count * 9);
            var rng = new System.Random(seed);
            var keys = new List<int>(perChunk.Keys); keys.Sort();
            var buckets = new List<Vector4>[CellsPerChunkSide * CellsPerChunkSide];
            for (int k = 0; k < buckets.Length; k++) buckets[k] = new List<Vector4>(96);
            foreach (int key in keys)
            {
                List<Vector4> list = perChunk[key];
                List<byte> cellOf = perChunkCell[key];
                // Cells first (the cell indices follow the generation order, which the shuffle below destroys).
                foreach (List<Vector4> bucket in buckets) bucket.Clear();
                for (int k = 0; k < list.Count; k++) buckets[cellOf[k]].Add(list[k]);
                int cellStart = builtCells.Count;
                foreach (List<Vector4> bucket in buckets)
                {
                    if (bucket.Count == 0) continue;
                    Shuffle(bucket, rng);
                    builtCells.Add(new Cell { Start = grouped.Count, Count = bucket.Count, Bounds = PlantBounds(bucket) });
                    grouped.AddRange(bucket);
                }
                Shuffle(list, rng);
                built.Add(new Chunk
                {
                    Start = all.Count, Count = list.Count, Bounds = PlantBounds(list),
                    CellStart = cellStart, CellCount = builtCells.Count - cellStart,
                });
                all.AddRange(list);
            }
            int count = all.Count;
            for (int k = 0; k < builtCells.Count; k++)
            {
                Cell cell = builtCells[k];
                cell.Start += count;                                   // the second copy follows the first in the buffer
                builtCells[k] = cell;
            }
            all.AddRange(grouped);
            return new Generated
            {
                Instances = all.ToArray(), Chunks = built.ToArray(), Cells = builtCells.ToArray(), Count = count,
                Milliseconds = watch.Elapsed.TotalMilliseconds,
            };
        }

        private static void Shuffle(List<Vector4> list, System.Random rng)
        {
            for (int k = list.Count - 1; k > 0; k--) { int swap = rng.Next(k + 1); (list[k], list[swap]) = (list[swap], list[k]); }
        }

        /// <summary>World bounds of a group of clumps: roots plus the tallest clump (2.9 m) leaning fully over.</summary>
        private static Bounds PlantBounds(List<Vector4> roots)
        {
            var bounds = new Bounds(roots[0], Vector3.zero);
            foreach (Vector4 v in roots) bounds.Encapsulate(v);
            bounds.Expand(new Vector3(6f, 0f, 6f));
            bounds.max += Vector3.up * 3.1f;
            return bounds;
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

            public HeightGrid(Vector3[] v, int[] t, Vector2 centre, float radius, float cellSize)
            {
                cell = cellSize; size = Mathf.CeilToInt(radius * 2f / cell) + 2;
                origin = centre - new Vector2(radius, radius);
                h = new float[size * size]; up = new float[size * size];
                for (int i = 0; i < h.Length; i++) h[i] = float.NaN;
                for (int k = 0; k + 2 < t.Length; k += 3) Raster(v[t[k]], v[t[k + 1]], v[t[k + 2]]);
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
            if (pending != null && pending.IsCompleted) Finish(pending.Result);
            Vector2 wd = windDirection.sqrMagnitude > 1e-6f ? windDirection.normalized : Vector2.right;
            Shader.SetGlobalVector(WindId, new Vector4(wd.x, wd.y, swayAngle * (reducedEffects ? .5f : 1f), gustAngle));
            // Scaled game time in Play mode (the wind holds when the game is paused by time scale).
            float windTime = previewWindTime >= 0f ? previewWindTime : Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            Shader.SetGlobalVector(WindWaveId, new Vector4(gustSpeed, gustWavelength, reducedEffects ? 1f : 0f, windTime));
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
            bool model = HasModel;
            if (!model && !warnedMissingModel)
            {
                warnedMissingModel = true;
                Debug.LogWarning("LAUNCH_GRASS_V04: the modeled clump meshes are missing (run the installer); drawing cards only.", this);
            }
            float maxDistance = Mathf.Min(maxDrawDistance * (reducedEffects ? reducedDrawDistance : 1f), camera.farClipPlane);
            // Without the model every instance is a card: both hand-over distances sit just behind the camera.
            float d1 = model ? Mathf.Min(modelDistance, maxDistance * .5f) : -modelBand - .1f;
            float d0 = model ? Mathf.Min(detailDistance, d1 * .6f) : -detailBand - .1f;
            Vector3 eye = camera.transform.position;
            farFraction = farCountFraction * Mathf.Lerp(lowCameraFarScale, 1f, Mathf.InverseLerp(3f, 20f, eye.y - meanGroundY));
            Shader.SetGlobalVector(LodId, new Vector4(d0, detailBand, maxDistance, farFraction));
            Shader.SetGlobalVector(Lod2Id, new Vector4(d1, modelBand, 0f, 0f));
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            var rp = new RenderParams(material)
            {
                layer = gameObject.layer, camera = camera, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true,
                lightProbeUsage = LightProbeUsage.Off, reflectionProbeUsage = ReflectionProbeUsage.Off,
                motionVectorMode = MotionVectorGenerationMode.Camera,
            };
            int used = 0, detailCount = 0, modelCount = 0, cardCount = 0; long tris = 0;
            int detailTris = DetailTriangles, modelTris = ModelTriangles, cardTris = CardTriangles;
            float reducedCount = reducedEffects ? .55f : 1f;
            for (int c = 0; c < chunks.Length; c++)
            {
                Chunk chunk = chunks[c];
                float dMin = Mathf.Sqrt(chunk.Bounds.SqrDistance(eye));
                if (dMin > maxDistance) continue;
                if (!GeometryUtility.TestPlanesAABB(planes, chunk.Bounds)) continue;
                float dMax = Vector3.Distance(eye, chunk.Bounds.center) + chunk.Bounds.extents.magnitude;
                if (dMax > d1 - modelBand)
                {
                    // Cards: the first N of the shuffled chunk; the shader thins by rank against the same curve.
                    float keep = KeepFraction(dMin, d1, maxDistance) * reducedCount;
                    int count = Mathf.Clamp(Mathf.CeilToInt(chunk.Count * Mathf.Min(1f, keep + .07f)), 1, chunk.Count);
                    Submit(ref rp, ref used, chunk.Start, chunk.Count, count, 2f, farMesh, chunk.Bounds);
                    cardCount += count; tris += (long)count * cardTris;
                }
                if (!model || dMin > d1 + modelBand) continue;
                for (int k = chunk.CellStart; k < chunk.CellStart + chunk.CellCount; k++)
                {
                    Cell cell = cells[k];
                    float cMin = Mathf.Sqrt(cell.Bounds.SqrDistance(eye));
                    if (cMin > d1 + modelBand) continue;
                    if (!GeometryUtility.TestPlanesAABB(planes, cell.Bounds)) continue;
                    float cMax = Vector3.Distance(eye, cell.Bounds.center) + cell.Bounds.extents.magnitude;
                    // Modeled levels keep every submitted clump (rank 0); reduced effects just submits fewer.
                    int count = Mathf.Clamp(Mathf.CeilToInt(cell.Count * reducedCount), 1, cell.Count);
                    if (cMin < d0 + detailBand)
                    {
                        Submit(ref rp, ref used, cell.Start, ModelRankBase, count, 0f, detailMesh, cell.Bounds);
                        detailCount += count; tris += (long)count * detailTris;
                    }
                    if (cMax > d0 - detailBand)
                    {
                        Submit(ref rp, ref used, cell.Start, ModelRankBase, count, 1f, simpleMesh, cell.Bounds);
                        modelCount += count; tris += (long)count * modelTris;
                    }
                }
            }
            LastDrawCalls = used; LastDetailInstances = detailCount; LastModelInstances = modelCount; LastCardInstances = cardCount;
            LastTriangles = tris;
        }

        /// <summary>_ChunkCount for the modeled levels: so large that every instance's rank is ~0 (always kept).</summary>
        private const int ModelRankBase = 1000000000;

        private float KeepFraction(float d, float modelRange, float max)
        {
            float t = Mathf.Clamp01((d - modelRange) / Mathf.Max(max - modelRange, 1f));
            float e = 1f - t;                                   // same curve as KeepFraction in the shader
            return Mathf.Lerp(1f, farFraction, 1f - e * e);
        }

        private void Submit(ref RenderParams rp, ref int used, int start, int rankBase, int count, float lod, Mesh mesh, Bounds bounds)
        {
            if (used == blocks.Count) blocks.Add(new MaterialPropertyBlock());
            MaterialPropertyBlock block = blocks[used++];
            block.SetFloat(ChunkStartId, start);
            block.SetFloat(ChunkCountId, rankBase);
            block.SetFloat(LodLevelId, lod);
            block.SetBuffer(InstancesId, buffer);
            rp.matProps = block;
            rp.worldBounds = bounds;
            Graphics.RenderMeshPrimitives(rp, mesh, 0, count);
        }
    }
}
