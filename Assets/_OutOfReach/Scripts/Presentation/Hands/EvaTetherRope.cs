using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OutOfReach.Presentation.Hands
{
    /// <summary>
    /// EVA-TETHER-01: the visible tether rope the crew climbs hand over hand. It follows the actual EVA route line
    /// (the same waypoint transforms as the gameplay route) at shoulder height, offset to the hull side, with clips
    /// on bracket posts at every corner and at most every <see cref="MaxAnchorSpacingMetres"/>, and a slight sag
    /// between anchors. The rope is parameterized by route distance (feet distance along the line), so a glove that
    /// holds "rung" d holds <see cref="PointAt"/>(d). Presentation only: no collider, physics or gameplay state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EvaTetherRope : MonoBehaviour
    {
        [Tooltip("The EVA route waypoints (feet positions) in order: the same transforms as the gameplay TetheredRoute.")]
        [SerializeField] private Transform[] waypoints = new Transform[0];

        [Header("Placement relative to the feet line")]
        [Tooltip("Rope height above the feet line, metres (the eye is 1.65 m above it).")]
        [SerializeField, Range(1.1f, 1.6f)] private float heightAboveFeetMetres = 1.40f;

        [Tooltip("Sideways offset, metres; positive = right of the outbound direction (the hull side of the delivered route).")]
        [SerializeField, Range(-0.6f, 0.6f)] private float lateralOffsetMetres = 0.36f;

        [Tooltip("Rope beyond the route start (outer hatch) to its first anchor, metres.")]
        [SerializeField, Range(0f, 0.6f)] private float startExtensionMetres = 0.12f;

        [Tooltip("Rope beyond the route end (work site) to its last anchor, metres.")]
        [SerializeField, Range(0f, 0.8f)] private float endExtensionMetres = 0.08f;

        [Header("Anchors and sag")]
        [SerializeField, Range(0.8f, 4f)] private float maxAnchorSpacingMetres = 2.2f;
        [SerializeField, Range(0f, 0.04f)] private float sagPerSpanMetre = 0.014f;
        [SerializeField, Range(0f, 0.12f)] private float maxSagMetres = 0.05f;

        [Tooltip("Colliders searched for the hull surface each bracket post stands on.")]
        [SerializeField] private LayerMask hullMask = ~0;
        [SerializeField, Range(0.1f, 2f)] private float maxStandoffMetres = 1.5f;

        [Tooltip("Bracket feet on the visible hull, baked by the installer (EvaTetherAuthoring) against the hull meshes.")]
        [SerializeField, HideInInspector] private float[] bakedAnchorParams = new float[0];
        [SerializeField, HideInInspector] private Vector3[] bakedBracketPoints = new Vector3[0];
        [SerializeField, HideInInspector] private Vector3[] bakedBracketNormals = new Vector3[0];

        [Header("Rope")]
        [Tooltip("Rope radius, metres. 15 mm suits the delivered EVA glove grip (closed around a 34 mm tube).")]
        [SerializeField, Range(0.008f, 0.02f)] private float radiusMetres = 0.015f;
        [SerializeField, Range(6, 24)] private int radialSegments = 12;
        [SerializeField, Range(0.01f, 0.1f)] private float sampleSpacingMetres = 0.03f;
        [Tooltip("Rope length per texture repeat along it, metres (about one circumference keeps the braid square).")]
        [SerializeField, Range(0.03f, 0.5f)] private float metresPerTextureRepeat = 0.094f;

        [Header("Hand-over-hand lattice (copied from the gameplay TetheredRoute by the installer)")]
        [SerializeField, Range(0.3f, 0.7f)] private float handStrokeMetres = 0.5f;
        [SerializeField, Range(0.05f, 0.3f)] private float nearReachMetres = 0.14f;

        [Header("Materials (Visuals/Props/EvaTetherV02)")]
        [SerializeField] private Material ropeMaterial;
        [SerializeField] private Material bracketMaterial;
        [SerializeField] private Material clipMaterial;

        private readonly List<Vector3> feet = new List<Vector3>();
        private readonly List<float> cumulative = new List<float>();
        private readonly List<Vector3> offsets = new List<Vector3>();
        private readonly List<Vector3> segmentDirections = new List<Vector3>();
        private readonly List<float> anchorParams = new List<float>();
        private readonly List<Vector3> anchorPoints = new List<Vector3>();
        private readonly List<Vector3> bracketBases = new List<Vector3>();
        private readonly List<bool> bracketOnHull = new List<bool>();
        private float length;
        private bool geometryReady;
        private GameObject generated;
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();

        public const float EyeHeightMetres = 1.65f;

        public bool IsGeometryReady => geometryReady || RefreshGeometry();

        public bool IsBuilt => generated != null;

        public float RouteLengthMetres => IsGeometryReady ? length : 0f;

        public float MinParam => -Mathf.Max(0f, startExtensionMetres);

        public float MaxParam => RouteLengthMetres + Mathf.Max(0f, endExtensionMetres);

        /// <summary>
        /// Furthest rope station a glove may hold: the route end (the work site). The short rope tail beyond it only
        /// runs to its end anchor and stays clear of the work-site hardware.
        /// </summary>
        public float MaxGripParam => RouteLengthMetres;

        public float RadiusMetres => radiusMetres;

        public float HeightAboveFeetMetres => heightAboveFeetMetres;

        public float LateralOffsetMetres => lateralOffsetMetres;

        public float MaxAnchorSpacingMetres => maxAnchorSpacingMetres;

        public float MaxSagMetres => maxSagMetres;

        public float HandStrokeMetres => Mathf.Clamp(handStrokeMetres, 0.3f, 0.7f);

        public float NearReachMetres => Mathf.Clamp(nearReachMetres, 0.05f, 0.3f);

        public IReadOnlyList<Transform> Waypoints => waypoints ?? Array.Empty<Transform>();

        /// <summary>Anchor stations (route distance) in order, ends included.</summary>
        public IReadOnlyList<float> AnchorParams
        {
            get
            {
                RefreshGeometry();
                return anchorParams;
            }
        }

        /// <summary>World centre of the rope at each anchor.</summary>
        public IReadOnlyList<Vector3> AnchorPoints
        {
            get
            {
                RefreshGeometry();
                return anchorPoints;
            }
        }

        /// <summary>Where each anchor's bracket post meets a surface (the hull), or its free standoff end.</summary>
        public IReadOnlyList<Vector3> BracketBases => bracketBases;

        /// <summary>True for an anchor whose bracket stands on a real collider surface.</summary>
        public IReadOnlyList<bool> BracketOnHull => bracketOnHull;

        public Transform GeneratedRoot => generated != null ? generated.transform : null;

        /// <summary>Editor/composition wiring: the same waypoint transforms as the gameplay route, lattice and materials.</summary>
        public void Configure(Transform[] routeWaypoints, float strokeMetres, float nearMetres, Material rope,
            Material bracket, Material clip)
        {
            waypoints = routeWaypoints ?? Array.Empty<Transform>();
            handStrokeMetres = Mathf.Clamp(strokeMetres, 0.3f, 0.7f);
            nearReachMetres = Mathf.Clamp(nearMetres, 0.05f, 0.3f);
            ropeMaterial = rope;
            bracketMaterial = bracket;
            clipMaterial = clip;
            geometryReady = false;
            if (Application.isPlaying && isActiveAndEnabled)
            {
                Build();
            }
        }

        /// <summary>Placement tuning for code-built scenes and tests.</summary>
        public void ConfigurePlacement(float heightAboveFeet, float lateralOffset, float maxAnchorSpacing, float sagPerSpan,
            float maxSag)
        {
            heightAboveFeetMetres = Mathf.Clamp(heightAboveFeet, 1.1f, 1.6f);
            lateralOffsetMetres = Mathf.Clamp(lateralOffset, -0.6f, 0.6f);
            maxAnchorSpacingMetres = Mathf.Clamp(maxAnchorSpacing, 0.8f, 4f);
            sagPerSpanMetre = Mathf.Clamp(sagPerSpan, 0f, 0.04f);
            maxSagMetres = Mathf.Clamp(maxSag, 0f, 0.12f);
            geometryReady = false;
        }

        private bool started;

        private void Start()
        {
            // Start, not Awake/OnEnable: every collider of the loaded scene is registered before the brackets search
            // for the hull surface.
            started = true;
            if (generated == null)
            {
                Build();
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying || !started)
            {
                return;
            }

            if (generated == null)
            {
                Build();
            }
            else
            {
                generated.SetActive(true);
            }
        }

        private void OnDisable()
        {
            if (generated != null)
            {
                generated.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            DestroyGenerated();
        }

        /// <summary>
        /// Re-reads the waypoints and (re)builds the rope, clips and brackets as hidden, unsaved children. Returns
        /// false (and builds nothing) for fewer than two waypoints.
        /// </summary>
        public bool Build()
        {
            geometryReady = false;
            if (!RefreshGeometry())
            {
                DestroyGenerated();
                return false;
            }

            DestroyGenerated();
            generated = new GameObject("EVA tether rope V02 (generated)");
            generated.hideFlags = HideFlags.DontSave;
            generated.transform.SetParent(transform, false);
            generated.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            generated.transform.localScale = Vector3.one;
            if (generated.transform.parent != null)
            {
                generated.transform.position = Vector3.zero;
                generated.transform.rotation = Quaternion.identity;
            }

            BuildRope();
            BuildAnchors();
            return true;
        }

        private void DestroyGenerated()
        {
            if (generated != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(generated);
                }
                else
                {
                    DestroyImmediate(generated);
                }

                generated = null;
            }

            foreach (Mesh mesh in ownedMeshes)
            {
                if (mesh != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(mesh);
                    }
                    else
                    {
                        DestroyImmediate(mesh);
                    }
                }
            }

            ownedMeshes.Clear();
        }

        /// <summary>Rope centre at a route distance (feet distance along the line), sag included, clamped to the rope.</summary>
        public Vector3 PointAt(float routeDistance)
        {
            if (!IsGeometryReady)
            {
                return transform.position;
            }

            float d = Mathf.Clamp(float.IsNaN(routeDistance) ? 0f : routeDistance, MinParam, MaxParam);
            return BasePoint(d) + Sag(d);
        }

        /// <summary>Unit direction of the rope at a route distance, towards increasing distance.</summary>
        public Vector3 TangentAt(float routeDistance)
        {
            if (!IsGeometryReady)
            {
                return Vector3.forward;
            }

            float d = Mathf.Clamp(float.IsNaN(routeDistance) ? 0f : routeDistance, MinParam, MaxParam);
            const float step = 0.02f;
            float a = Mathf.Max(MinParam, d - step);
            float b = Mathf.Min(MaxParam, d + step);
            Vector3 delta = PointAt(b) - PointAt(a);
            return delta.sqrMagnitude > 1e-10f ? delta.normalized : SegmentDirection(d);
        }

        /// <summary>The feet-line point at a route distance (the gameplay line), extended straight past both ends.</summary>
        public Vector3 FeetPointAt(float routeDistance)
        {
            if (!IsGeometryReady)
            {
                return transform.position;
            }

            float d = float.IsNaN(routeDistance) ? 0f : routeDistance;
            if (d <= 0f)
            {
                return feet[0] + (segmentDirections[0] * d);
            }

            if (d >= length)
            {
                return feet[feet.Count - 1] + (segmentDirections[segmentDirections.Count - 1] * (d - length));
            }

            int i = SegmentIndex(d);
            float span = cumulative[i + 1] - cumulative[i];
            float f = span > 1e-6f ? (d - cumulative[i]) / span : 0f;
            return Vector3.Lerp(feet[i], feet[i + 1], f);
        }

        private bool RefreshGeometry()
        {
            if (geometryReady)
            {
                return true;
            }

            feet.Clear();
            if (waypoints != null)
            {
                foreach (Transform waypoint in waypoints)
                {
                    if (waypoint != null && (feet.Count == 0 || (waypoint.position - feet[feet.Count - 1]).sqrMagnitude > 1e-8f))
                    {
                        feet.Add(waypoint.position);
                    }
                }
            }

            if (feet.Count < 2)
            {
                return false;
            }

            cumulative.Clear();
            segmentDirections.Clear();
            cumulative.Add(0f);
            for (int i = 1; i < feet.Count; i++)
            {
                Vector3 segment = feet[i] - feet[i - 1];
                cumulative.Add(cumulative[i - 1] + segment.magnitude);
                segmentDirections.Add(segment.normalized);
            }

            length = cumulative[cumulative.Count - 1];

            // Hull-side direction per segment (horizontal right of the outbound heading), mitred at the corners.
            var sides = new List<Vector3>(segmentDirections.Count);
            Vector3 lastSide = Vector3.zero;
            for (int i = 0; i < segmentDirections.Count; i++)
            {
                Vector3 horizontal = Vector3.ProjectOnPlane(segmentDirections[i], Vector3.up);
                Vector3 side = horizontal.sqrMagnitude > 1e-4f ? Vector3.Cross(Vector3.up, horizontal).normalized : lastSide;
                sides.Add(side);
                if (side.sqrMagnitude > 0f)
                {
                    lastSide = side;
                }
            }

            for (int i = sides.Count - 1; i >= 0; i--)
            {
                if (sides[i].sqrMagnitude <= 0f)
                {
                    sides[i] = i + 1 < sides.Count && sides[i + 1].sqrMagnitude > 0f ? sides[i + 1] : Vector3.right;
                }
            }

            offsets.Clear();
            for (int i = 0; i < feet.Count; i++)
            {
                Vector3 side;
                if (i == 0)
                {
                    side = sides[0];
                }
                else if (i == feet.Count - 1)
                {
                    side = sides[sides.Count - 1];
                }
                else
                {
                    // Mitre: the corner point where both parallel rope lines meet, so every straight stretch of rope
                    // runs exactly the lateral offset beside its segment (limited to twice it on sharp turns).
                    Vector3 mitre = sides[i - 1] + sides[i];
                    if (mitre.sqrMagnitude > 1e-4f)
                    {
                        Vector3 bisector = mitre.normalized;
                        float cosine = Vector3.Dot(bisector, sides[i]);
                        side = bisector * Mathf.Min(2f, 1f / Mathf.Max(0.5f, cosine));
                    }
                    else
                    {
                        side = sides[i];
                    }
                }

                offsets.Add((Vector3.up * heightAboveFeetMetres) + (side * lateralOffsetMetres));
            }

            // Anchors: rope ends, every corner, and evenly between corners at most the anchor spacing apart.
            anchorParams.Clear();
            anchorParams.Add(MinParamUnchecked());
            for (int i = 0; i < feet.Count - 1; i++)
            {
                float start = cumulative[i];
                float span = cumulative[i + 1] - start;
                int pieces = Mathf.Max(1, Mathf.CeilToInt(span / Mathf.Max(0.5f, maxAnchorSpacingMetres)));
                for (int k = 0; k < pieces; k++)
                {
                    AddAnchor(start + (span * k / pieces));
                }
            }

            AddAnchor(length);
            AddAnchor(length + Mathf.Max(0f, endExtensionMetres));
            geometryReady = true;
            anchorPoints.Clear();
            foreach (float anchor in anchorParams)
            {
                anchorPoints.Add(BasePoint(anchor));
            }

            return true;
        }

        private float MinParamUnchecked() => -Mathf.Max(0f, startExtensionMetres);

        private void AddAnchor(float param)
        {
            if (anchorParams.Count == 0 || param > anchorParams[anchorParams.Count - 1] + 0.05f)
            {
                anchorParams.Add(param);
            }
        }

        private int SegmentIndex(float d)
        {
            int last = feet.Count - 2;
            for (int i = 0; i < last; i++)
            {
                if (d < cumulative[i + 1])
                {
                    return i;
                }
            }

            return last;
        }

        private Vector3 SegmentDirection(float d)
        {
            if (segmentDirections.Count == 0)
            {
                return Vector3.forward;
            }

            if (d <= 0f)
            {
                return segmentDirections[0];
            }

            if (d >= length)
            {
                return segmentDirections[segmentDirections.Count - 1];
            }

            return segmentDirections[SegmentIndex(d)];
        }

        /// <summary>Straight rope line between corner offsets (no sag), extended past both ends.</summary>
        private Vector3 BasePoint(float d)
        {
            if (d <= 0f)
            {
                return feet[0] + offsets[0] + (segmentDirections[0] * d);
            }

            if (d >= length)
            {
                return feet[feet.Count - 1] + offsets[offsets.Count - 1]
                    + (segmentDirections[segmentDirections.Count - 1] * (d - length));
            }

            int i = SegmentIndex(d);
            float span = cumulative[i + 1] - cumulative[i];
            float f = span > 1e-6f ? (d - cumulative[i]) / span : 0f;
            return Vector3.Lerp(feet[i] + offsets[i], feet[i + 1] + offsets[i + 1], f);
        }

        private Vector3 Sag(float d)
        {
            if (anchorParams.Count < 2 || maxSagMetres <= 0f || sagPerSpanMetre <= 0f)
            {
                return Vector3.zero;
            }

            for (int j = 0; j < anchorParams.Count - 1; j++)
            {
                float a = anchorParams[j];
                float b = anchorParams[j + 1];
                if (d < a || d > b)
                {
                    continue;
                }

                Vector3 span = BasePoint(b) - BasePoint(a);
                float spanLength = span.magnitude;
                if (spanLength < 1e-4f || b - a < 1e-4f)
                {
                    return Vector3.zero;
                }

                float u = (d - a) / (b - a);
                float depth = Mathf.Min(maxSagMetres, sagPerSpanMetre * spanLength) * 4f * u * (1f - u);
                Vector3 down = Vector3.ProjectOnPlane(Vector3.down, span / spanLength);
                if (down.sqrMagnitude < 0.04f)
                {
                    // A near-vertical span hangs towards the hull side instead.
                    down = Vector3.ProjectOnPlane(offsets[0] - (Vector3.up * heightAboveFeetMetres), span / spanLength);
                }

                return down.sqrMagnitude > 1e-6f ? down.normalized * depth : Vector3.zero;
            }

            return Vector3.zero;
        }

        private void BuildRope()
        {
            var centres = new List<Vector3>();
            var tangents = new List<Vector3>();
            float start = MinParam;
            float end = MaxParam;
            var stations = new List<float>();
            for (int j = 0; j < anchorParams.Count - 1; j++)
            {
                float a = anchorParams[j];
                float b = anchorParams[j + 1];
                int pieces = Mathf.Max(1, Mathf.CeilToInt((b - a) / Mathf.Max(0.005f, sampleSpacingMetres)));
                for (int k = 0; k < pieces; k++)
                {
                    stations.Add(a + ((b - a) * k / pieces));
                }
            }

            stations.Add(end);
            for (int i = 0; i < stations.Count; i++)
            {
                float d = Mathf.Clamp(stations[i], start, end);
                centres.Add(PointAt(d));
            }

            for (int i = 0; i < centres.Count; i++)
            {
                Vector3 previous = centres[Mathf.Max(0, i - 1)];
                Vector3 next = centres[Mathf.Min(centres.Count - 1, i + 1)];
                Vector3 t = next - previous;
                tangents.Add(t.sqrMagnitude > 1e-10f ? t.normalized : SegmentDirection(stations[i]));
            }

            Mesh mesh = EvaTetherMeshes.Tube(centres, tangents, radiusMetres, radialSegments, metresPerTextureRepeat,
                "EVA tether rope V02");
            ownedMeshes.Add(mesh);
            AddRenderer("Rope", mesh, ropeMaterial, true);
        }

        private void BuildAnchors()
        {
            bracketBases.Clear();
            bracketOnHull.Clear();
            var brackets = new EvaTetherMeshes.MeshBuilder();
            var clips = new EvaTetherMeshes.MeshBuilder();
            for (int i = 0; i < anchorParams.Count; i++)
            {
                float d = anchorParams[i];
                Vector3 rope = PointAt(d);
                Vector3 tangent = TangentAt(d);
                Vector3 hullward = HullSide(d);
                Vector3 surface;
                Vector3 surfaceNormal;
                bool onHull = HasBakedBrackets
                    ? BakedHull(i, out surface, out surfaceNormal)
                    : FindHull(rope, hullward, out surface, out surfaceNormal);
                Vector3 toBracket = onHull ? (surface - rope).normalized : hullward;
                toBracket = Vector3.ProjectOnPlane(toBracket, tangent);
                if (toBracket.sqrMagnitude < 1e-4f)
                {
                    toBracket = Vector3.ProjectOnPlane(Vector3.down, tangent);
                }

                toBracket.Normalize();

                // Carabiner: loop plane across the rope; the rope runs through its near end, the post eye its far end.
                const float halfLength = 0.052f;
                const float halfWidth = 0.022f;
                const float wire = 0.0055f;
                Vector3 loopCentre = rope + (toBracket * (halfLength - wire - radiusMetres - 0.002f));
                Vector3 across = Vector3.Cross(tangent, toBracket);
                EvaTetherMeshes.AddLoop(clips, loopCentre, toBracket, across, halfLength, halfWidth, wire, 20, 8);

                // Bracket: eye ring through the carabiner's far end, a post to the hull and a bolted base plate.
                Vector3 farWire = loopCentre + (toBracket * (halfLength - wire));
                const float eyeRadius = 0.017f;
                Vector3 eyeCentre = farWire + (toBracket * (eyeRadius - 0.004f));
                EvaTetherMeshes.AddLoop(brackets, eyeCentre, tangent, toBracket, eyeRadius, eyeRadius, 0.005f, 16, 8);
                Vector3 postStart = eyeCentre + (toBracket * eyeRadius);
                Vector3 baseEnd;
                if (onHull && Vector3.Distance(postStart, surface) > 0.02f)
                {
                    baseEnd = surface;
                    brackets.AddCylinder(postStart, surface - (surfaceNormal.normalized * 0.004f), 0.0085f, 10);
                    brackets.AddPlate(surface, surfaceNormal, 0.034f, 0.007f, 14);
                }
                else
                {
                    // No surface in reach: a short stub with an end cap (never a long post into empty space).
                    baseEnd = postStart + (toBracket * 0.05f);
                    brackets.AddCylinder(postStart, baseEnd, 0.0085f, 10);
                    brackets.AddPlate(baseEnd, -toBracket, 0.018f, 0.006f, 12);
                }

                bracketBases.Add(baseEnd);
                bracketOnHull.Add(onHull);
            }

            Mesh clipMesh = clips.Build("EVA tether clips V02");
            Mesh bracketMesh = brackets.Build("EVA tether brackets V02");
            ownedMeshes.Add(clipMesh);
            ownedMeshes.Add(bracketMesh);
            AddRenderer("Clips", clipMesh, clipMaterial, true);
            AddRenderer("Brackets", bracketMesh, bracketMaterial, true);
        }

        /// <summary>Direction from the rope towards the hull side at a distance (the rope's lateral offset sense).</summary>
        private Vector3 HullSide(float d)
        {
            Vector3 lateral = BasePoint(d) - FeetPointAt(d) - (Vector3.up * heightAboveFeetMetres);
            if (lateral.sqrMagnitude < 1e-6f)
            {
                return Vector3.down;
            }

            // The rope is offset towards the hull by design, whichever side that is.
            return lateral.normalized;
        }

        /// <summary>True when the installer baked one bracket foot per current anchor.</summary>
        public bool HasBakedBrackets
        {
            get
            {
                RefreshGeometry();
                if (bakedAnchorParams == null || bakedBracketPoints == null || bakedBracketNormals == null
                    || bakedAnchorParams.Length != anchorParams.Count || bakedBracketPoints.Length != anchorParams.Count
                    || bakedBracketNormals.Length != anchorParams.Count)
                {
                    return false;
                }

                for (int i = 0; i < anchorParams.Count; i++)
                {
                    if (Mathf.Abs(bakedAnchorParams[i] - anchorParams[i]) > 0.001f)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// Editor/installer: finds, for every anchor, the nearest VISIBLE surface on the hull side (a collider with an
        /// enabled renderer on the same object; the installer adds temporary mesh colliders to the hull meshes first)
        /// and stores the bracket foot. Returns how many anchors found one; the others get a short stub.
        /// </summary>
        public int BakeBrackets()
        {
            geometryReady = false;
            if (!RefreshGeometry())
            {
                return 0;
            }

            Physics.SyncTransforms();
            int count = anchorParams.Count;
            bakedAnchorParams = new float[count];
            bakedBracketPoints = new Vector3[count];
            bakedBracketNormals = new Vector3[count];
            int found = 0;
            for (int i = 0; i < count; i++)
            {
                float d = anchorParams[i];
                bakedAnchorParams[i] = d;
                if (FindHull(PointAt(d), HullSide(d), out Vector3 point, out Vector3 normal, true))
                {
                    bakedBracketPoints[i] = point;
                    bakedBracketNormals[i] = normal;
                    found++;
                }
                else
                {
                    bakedBracketPoints[i] = new Vector3(float.NaN, float.NaN, float.NaN);
                    bakedBracketNormals[i] = Vector3.zero;
                }
            }

            return found;
        }

        private bool BakedHull(int index, out Vector3 point, out Vector3 normal)
        {
            point = bakedBracketPoints[index];
            normal = bakedBracketNormals[index];
            return !float.IsNaN(point.x) && normal.sqrMagnitude > 0.5f;
        }

        private static readonly Vector3[] SearchDirections = BuildSearchDirections();

        private static Vector3[] BuildSearchDirections()
        {
            var list = new List<Vector3>();
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        if (x != 0 || y != 0 || z != 0)
                        {
                            list.Add(new Vector3(x, y, z).normalized);
                        }
                    }
                }
            }

            return list.ToArray();
        }

        private bool FindHull(Vector3 rope, Vector3 hullward, out Vector3 point, out Vector3 normal, bool visibleOnly = false)
        {
            point = default;
            normal = default;
            if (!Application.isPlaying)
            {
                Physics.SyncTransforms();
            }

            float best = float.PositiveInfinity;
            var directions = new List<Vector3>(SearchDirections) { hullward };
            foreach (Vector3 direction in directions)
            {
                if (direction.sqrMagnitude < 1e-6f)
                {
                    continue;
                }

                float toward = Vector3.Dot(direction, hullward);
                if (toward < -0.2f)
                {
                    continue; // never across the climber's body
                }

                Vector3 start = rope + (direction * (radiusMetres + 0.03f));
                foreach (RaycastHit hit in Physics.RaycastAll(start, direction, maxStandoffMetres, hullMask, QueryTriggerInteraction.Ignore))
                {
                    if (Vector3.Dot(hit.normal, -direction) < 0.25f || (visibleOnly && !VisibleSurface(hit.collider)))
                    {
                        continue;
                    }

                    // Prefer the hull side over a nearer surface off to the side.
                    float score = hit.distance * (1.6f - (0.6f * toward));
                    if (score >= best)
                    {
                        continue;
                    }

                    best = score;
                    point = hit.point;
                    normal = hit.normal;
                }
            }

            return best < float.PositiveInfinity;
        }

        private static bool VisibleSurface(Collider collider)
        {
            var renderer = collider ? collider.GetComponent<Renderer>() : null;
            return renderer && renderer.enabled && renderer.gameObject.activeInHierarchy;
        }

        private void AddRenderer(string name, Mesh mesh, Material material, bool castShadows)
        {
            var child = new GameObject(name);
            child.hideFlags = HideFlags.DontSave;
            child.transform.SetParent(generated.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }
    }
}
