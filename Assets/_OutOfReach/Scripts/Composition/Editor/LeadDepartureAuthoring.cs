using System;
using System.IO;
using System.Linq;
using OutOfReach.Presentation;
using OutOfReach.Presentation.Cinematics;
using OutOfReach.Presentation.MarineOcean.FX;
using OutOfReach.Presentation.Motion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OutOfReach.Composition.Editor
{
    /// <summary>Integrator-run scene authoring for the shared Bootstrap departure.</summary>
    public static class LeadDepartureAuthoring
    {
        private const string OrbitSetName = "Departure - AURORA-7 in Earth orbit";
        private const string TransferAnchorsName = "Departure - AURORA-7 interior transfer";
        private const string HullGroupName = "Delivered AURORA-7 exterior renderers";
        private const string ReceiverName = "AURORA-7 docking contact plane";
        private const string ApproachName = "AURORA-7 approach camera pose";
        private const string DockName = "AURORA-7 collar camera pose";
        private const string TransferStartName = "Aft airlock transfer eye - start";
        private const string TransferEndName = "Aft airlock transfer eye - end";
        private const string SeatBodyFrameName = "Departure seated WORK body";
        private const string Materials = "Assets/_OutOfReach/Visuals/Models/Aurora7/Materials/";
        private const string TransitShader = "Assets/_OutOfReach/Scripts/Presentation/Cinematics/Preflight/DepartureTransitSky.shader";
        private const string Stars = "Assets/_OutOfReach/Visuals/Space/DistantStars.mat";
        private const float TransferEyeHeight = 1.60f;
        private static readonly Vector3 SeatedEye = new Vector3(0, 1.397f, 1.40f);
        private static readonly Vector3 SeatedBodyOrigin = new Vector3(0, .12f, 1.46f);
        private static readonly Quaternion SeatedBodyFacing = Quaternion.Euler(0f, 180f, 0f);

        // Unity -executeMethod OutOfReach.Composition.Editor.LeadDepartureAuthoring.Apply
        public static void Apply()
        {
            RequireIntegrationProject();
            Scene scene = OpenSavedBootstrap();
            GameObject[] roots = scene.GetRootGameObjects();
            I06MarineBindings marine = ExactlyOne<I06MarineBindings>(roots);
            ExactlyOne<TransportDeparturePresenter>(roots);
            ExactlyOne<MarineDepartureFxPresenter>(roots);
            EmbodiedBodyPresenter body = ExactlyOne<EmbodiedBodyPresenter>(roots);
            if (!marine.MarineRoot || !marine.Vehicle || !marine.SeatCamera || marine.SeatCamera.parent != marine.Vehicle)
                throw new InvalidOperationException("Bootstrap lacks the actual launch transport and seated eye anchor.");

            ShipVisualAnchors ship = ExactlyOne<ShipVisualAnchors>(roots);
            Transform actualModel = NamedRoot(roots, "AURORA-7 delivered Blender models");
            Transform site = ChildNamed(marine.MarineRoot, "Launch site - stationary");
            Transform pad = DescendantNamed(site, "Land pad top plane");
            Transform port = ChildNamed(marine.Vehicle, "DockingPort");
            Transform booster = ChildNamed(marine.Vehicle, "BoosterStack");
            Material stars = Required<Material>(Stars);
            Shader transitSky = Required<Shader>(TransitShader);
            RequireMetreScale(ship.transform);
            RequireMetreScale(actualModel);
            RequireMetreScale(port);
            RequireMetreScale(booster);
            if (!ship.AirlockOuter || !ship.AirlockInner ||
                Vector3.Dot(port.forward, marine.Vehicle.up) < .999f ||
                Mathf.Abs(marine.Vehicle.InverseTransformPoint(port.position).y - 4.95f) > .02f ||
                Vector3.Dot(pad.up, Vector3.up) < .999f)
                throw new InvalidOperationException("Saved transport docking, airlock or pad anchors disagree with the reviewed geometry.");

            Transform orbit = NamedRoot(roots, OrbitSetName, false);
            Transform transfer = NamedRoot(roots, TransferAnchorsName, false);
            if ((orbit == null) != (transfer == null))
                throw new InvalidOperationException("Bootstrap contains only part of the authored departure groups.");

            bool newGroups = !orbit;
            Transform seatBodyFrame = marine.Vehicle.Find(SeatBodyFrameName);
            bool newSeatBodyFrame = !seatBodyFrame;
            Vector3 previousSeatedEye = marine.SeatCamera.localPosition;
            try
            {
                if (newGroups)
                {
                    orbit = new GameObject(OrbitSetName).transform;
                    transfer = new GameObject(TransferAnchorsName).transform;
                    BuildOrbit(orbit, actualModel, ship.AirlockOuter);
                    BuildTransfer(transfer, ship.AirlockOuter, ship.AirlockInner);
                }
                if (newSeatBodyFrame)
                    seatBodyFrame = Marker(marine.Vehicle, SeatBodyFrameName, SeatedBodyOrigin, SeatedBodyFacing);
                RequireMetreScale(seatBodyFrame);

                Transform receiver = ChildNamed(orbit, ReceiverName);
                Transform approach = ChildNamed(orbit, ApproachName);
                Transform dock = ChildNamed(orbit, DockName);
                Transform start = ChildNamed(transfer, TransferStartName);
                Transform end = ChildNamed(transfer, TransferEndName);
                ValidateOrbit(orbit, receiver, approach, dock, marine.Vehicle, port);
                ValidateTransfer(start, end);

                if (!newGroups && !newSeatBodyFrame &&
                    AlreadyBound(marine, site, pad, port, booster, receiver, approach, dock, start, end,
                        orbit.gameObject, stars, transitSky, body, seatBodyFrame))
                {
                    Debug.Log("LEAD_DEPARTURE_AUTHORING VERIFIED existing Bootstrap anchors.");
                    return;
                }

                marine.SeatCamera.localPosition = SeatedEye;
                seatBodyFrame.SetLocalPositionAndRotation(SeatedBodyOrigin, SeatedBodyFacing);
                orbit.gameObject.SetActive(false);
                marine.ConfigureDepartureSite(site, pad);
                marine.ConfigureDepartureSeat(body, seatBodyFrame);
                marine.ConfigureOrbitalViews(port, booster, receiver, approach, dock, start, end, orbit.gameObject, stars, transitSky);
                EditorUtility.SetDirty(marine);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new IOException("Could not save the authored departure to Bootstrap.");
                Debug.Log($"LEAD_DEPARTURE_AUTHORING SAVED receiver={receiver.position:F3} transfer={start.position:F3} to {end.position:F3}");
            }
            catch
            {
                marine.SeatCamera.localPosition = previousSeatedEye;
                if (newSeatBodyFrame && seatBodyFrame) Object.DestroyImmediate(seatBodyFrame.gameObject);
                if (newGroups)
                {
                    if (transfer) Object.DestroyImmediate(transfer.gameObject);
                    if (orbit) Object.DestroyImmediate(orbit.gameObject);
                }
                throw;
            }
        }

        private static void BuildOrbit(Transform orbit, Transform actualRoot, Transform outerAirlock)
        {
            // Scene metres for a clean background and lighting lease, separate from gameplay and the launch site.
            orbit.SetPositionAndRotation(actualRoot.position + Vector3.forward * 1200f, actualRoot.rotation);
            Transform hull = Marker(orbit, HullGroupName, Vector3.zero, Quaternion.identity);
            MeshRenderer[] actualRenderers = actualRoot.GetComponentsInChildren<MeshRenderer>(true);
            CopyRenderer(actualRenderers.Single(renderer => renderer.name == "A7_Exterior"), actualRoot, hull);
            foreach (MeshRenderer renderer in ChildNamed(actualRoot, "ExteriorParts").GetComponentsInChildren<MeshRenderer>(true))
                CopyRenderer(renderer, actualRoot, hull);
            CopyRenderer(actualRenderers.Single(renderer => renderer.name == "DoorLeaf_Outer_L"), actualRoot, hull);
            CopyRenderer(actualRenderers.Single(renderer => renderer.name == "DoorLeaf_Outer_R"), actualRoot, hull);

            // The adapter mounts on AURORA's existing aft hatch and ends beyond its EVA porch.
            Vector3 hatchPosition = actualRoot.InverseTransformPoint(outerAirlock.position);
            Vector3 collarBase = hatchPosition + new Vector3(0, 1.28f, .16f);
            const float collarLength = 3.84f;
            Vector3 contact = collarBase + Vector3.forward * collarLength;
            Transform collar = Marker(orbit, "Aft docking adapter and alignment lamps", collarBase, Quaternion.identity);
            BuildCollar(collar, collarLength);
            Transform receiver = Marker(orbit, ReceiverName, contact, Quaternion.identity);
            Shot(orbit, ApproachName, receiver.localPosition, new Vector3(37f, 9.3f, 22f), new Vector3(0, .4f, 1.5f));
            Shot(orbit, DockName, receiver.localPosition, new Vector3(9.5f, 2.8f, 4.2f), new Vector3(0, .05f, 1.9f));
        }

        private static void BuildCollar(Transform collar, float length)
        {
            Material alloy = Required<Material>(Materials + "A7_Aluminum.mat");
            Material graphite = Required<Material>(Materials + "A7_PaintGraphite.mat");
            Material pale = Required<Material>(Materials + "A7_PaintWhite.mat");
            Material amber = Required<Material>(Materials + "A7_PaintAmber.mat");
            Material gasket = Required<Material>(Materials + "A7_Rubber.mat");
            Material cyan = Required<Material>(Materials + "A7_LedCyan.mat");

            Ring(collar, "Bulkhead flange", 1.18f, 1.61f, .11f, .26f, alloy);
            Ring(collar, "Transfer barrel", 1.12f, 1.30f, length * .48f, length * .96f, graphite);
            Ring(collar, "Adapter support ring", 1.20f, 1.39f, length * .66f, .22f, alloy);
            Ring(collar, "Mating flange", .90f, 1.34f, length - .20f, .26f, pale);
            // The source capsule face is at y=4.95 m with a .82 m outer radius. Its guide lugs
            // project .15 m past that face within a .736 m radius, into this gasket opening.
            Ring(collar, "Contact seal", .75f, .97f, length - .034f, .068f, gasket);

            for (int i = 0; i < 4; i++)
            {
                Quaternion orientation = Quaternion.AngleAxis(45f + i * 90f, Vector3.forward);
                Vector3 radial = orientation * Vector3.up;
                Vector3 tangent = orientation * Vector3.right;
                Panel(collar, "Axial reinforcing rail", radial * 1.31f + Vector3.forward * 1.79f,
                    orientation, new Vector3(.18f, .15f, 3.24f), alloy);
                Vector3 latch = radial * 1.20f + Vector3.forward * (length - .08f);
                Panel(collar, "Amber latch housing", latch, orientation, new Vector3(.35f, .24f, .13f), amber);
                Panel(collar, "Cyan alignment lamp", latch + tangent * .26f, orientation,
                    new Vector3(.14f, .08f, .11f), cyan);
            }
        }

        private static void Ring(Transform collar, string name, float inner, float outer, float axial, float depth, Material material)
        {
            const int facets = 16;
            const float sector = 360f / facets;
            float width = 2f * outer * Mathf.Tan(Mathf.Deg2Rad * sector * .5f);
            float radius = (inner + outer) * .5f;
            Transform band = Marker(collar, name, Vector3.forward * axial, Quaternion.identity);
            for (int i = 0; i < facets; i++)
            {
                Quaternion rotation = Quaternion.AngleAxis(i * sector, Vector3.forward);
                // A submillimetre seam prevents coplanar faces where adjacent pieces meet.
                Vector3 center = rotation * Vector3.up * radius - Vector3.forward * ((i % 2) * .0007f);
                Panel(band, "Collar facet", center, rotation, new Vector3(width, outer - inner, depth), material);
            }
        }

        private static void Panel(Transform parent, string name, Vector3 position, Quaternion rotation,
            Vector3 size, Material material)
        {
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = name;
            panel.transform.SetParent(parent, false);
            panel.transform.SetLocalPositionAndRotation(position, rotation);
            panel.transform.localScale = size;
            Object.DestroyImmediate(panel.GetComponent<Collider>());
            MeshRenderer renderer = panel.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        private static void CopyRenderer(MeshRenderer source, Transform sourceRoot, Transform destination)
        {
            if (!source || !source.enabled)
                throw new InvalidOperationException("The reviewed AURORA exterior renderer is missing or disabled.");
            MeshFilter filter = source.GetComponent<MeshFilter>();
            if (!filter || !filter.sharedMesh)
                throw new InvalidOperationException("The reviewed AURORA exterior renderer has no mesh: " + source.name);
            Transform copy = Marker(destination, source.name, sourceRoot.InverseTransformPoint(source.transform.position),
                Quaternion.Inverse(sourceRoot.rotation) * source.transform.rotation);
            copy.localScale = source.transform.lossyScale;
            copy.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            MeshRenderer renderer = copy.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = source.sharedMaterials;
            renderer.shadowCastingMode = source.shadowCastingMode;
            renderer.receiveShadows = source.receiveShadows;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static void BuildTransfer(Transform transfer, Transform outer, Transform inner)
        {
            Vector3 inward = (inner.position - outer.position).normalized;
            Vector3 up = outer.up;
            if (Vector3.Dot(inward, -outer.forward) < .999f || Vector3.Distance(inner.position, outer.position) < 3.5f)
                throw new InvalidOperationException("The reviewed aft airlock passage anchors are no longer aligned.");
            Quaternion rotation = Quaternion.LookRotation(inward, up);
            Marker(transfer, TransferStartName, outer.position + inward * .58f + up * TransferEyeHeight, rotation);
            Marker(transfer, TransferEndName, inner.position - inward * .67f + up * TransferEyeHeight, rotation);
        }

        private static void ValidateTransfer(Transform start, Transform end)
        {
            RequireMetreScale(start);
            RequireMetreScale(end);
            if (Vector3.Distance(start.position, end.position) < 2.5f ||
                Vector3.Dot(start.forward, (end.position - start.position).normalized) < .995f)
                throw new InvalidOperationException("Departure transfer markers must span and face along the real aft airlock.");

            Physics.SyncTransforms();
            for (int i = 0; i <= 18; i++)
            {
                Vector3 eye = Vector3.Lerp(start.position, end.position, i / 18f);
                Vector3 feet = eye - start.up * TransferEyeHeight;
                Collider[] hits = Physics.OverlapCapsule(feet + start.up * .32f, eye + start.up * .04f,
                    .24f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (hits.Length > 0)
                    throw new InvalidOperationException($"Transfer clearance blocked at {eye:F3} by {hits[0].name}.");
                if (!Physics.Raycast(feet + start.up * .22f, -start.up, out RaycastHit floor, .40f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || floor.collider.name != "COL_Airlock_Floor")
                    throw new InvalidOperationException($"The live AURORA airlock deck is missing below {eye:F3}.");
            }
        }

        private static void ValidateOrbit(Transform orbit, Transform receiver, Transform approach, Transform dock,
            Transform vehicle, Transform capsulePort)
        {
            RequireMetreScale(receiver);
            RequireMetreScale(approach);
            RequireMetreScale(dock);
            if (orbit.GetComponentsInChildren<Camera>(true).Length != 0 ||
                orbit.GetComponentsInChildren<AudioListener>(true).Length != 0 ||
                orbit.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("The orbital set must have visual geometry and plain pose anchors only.");

            MeshRenderer[] hull = ChildNamed(orbit, HullGroupName).GetComponentsInChildren<MeshRenderer>(true);
            if (hull.Length < 3 || hull.Max(renderer => MaxAlong(renderer.bounds, receiver.position, receiver.forward)) > -.60f)
                throw new InvalidOperationException("AURORA geometry extends into the capsule approach ahead of its receiving ring.");
            var capsule = ChildNamed(vehicle, "CapsuleShell").GetComponent<MeshRenderer>();
            if (!capsule || Mathf.Abs(MaxAlong(capsule.bounds, capsulePort.position, capsulePort.forward) - .15f) > .04f)
                throw new InvalidOperationException("The transport nose and the contact datum no longer match the reviewed dimensions.");

            Vector3 firstPort = receiver.position + receiver.forward * 19f + receiver.right * 3f;
            if (!InFrame(approach, receiver.position, 47f) || !InFrame(approach, firstPort, 47f) ||
                !InFrame(dock, receiver.position, 41f) || !InFrame(dock, receiver.position + receiver.forward * 1.35f, 41f))
                throw new InvalidOperationException("An authored camera crops the mating interface before its intended shot.");
        }

        private static float MaxAlong(Bounds bounds, Vector3 origin, Vector3 axis)
        {
            Vector3 absoluteAxis = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));
            return Vector3.Dot(bounds.center - origin, axis) + Vector3.Dot(bounds.extents, absoluteAxis);
        }

        private static bool InFrame(Transform view, Vector3 point, float verticalFov)
        {
            Vector3 projected = view.InverseTransformPoint(point);
            float halfHeight = projected.z * Mathf.Tan(verticalFov * .5f * Mathf.Deg2Rad);
            return projected.z > .05f && Mathf.Abs(projected.y) < halfHeight * .90f &&
                Mathf.Abs(projected.x) < halfHeight * (16f / 9f) * .90f;
        }

        private static bool AlreadyBound(I06MarineBindings marine, Transform site, Transform pad, Transform port,
            Transform booster, Transform receiver, Transform approach, Transform dock, Transform start, Transform end,
            GameObject orbit, Material stars, Shader transitSky, EmbodiedBodyPresenter body, Transform seatBodyFrame)
        {
            var fields = new SerializedObject(marine);
            bool Matches(string name, Object value) => fields.FindProperty(name).objectReferenceValue == value;
            return marine.SeatCamera.localPosition == SeatedEye && !orbit.activeSelf &&
                seatBodyFrame.localPosition == SeatedBodyOrigin && seatBodyFrame.localRotation == SeatedBodyFacing &&
                Matches("seatedBody", body) && Matches("seatedBodyFrame", seatBodyFrame) &&
                Matches("stationarySite", site) && Matches("dustPlane", pad) && Matches("capsulePort", port) &&
                Matches("booster", booster) && Matches("auroraReceiver", receiver) && Matches("approachView", approach) &&
                Matches("dockView", dock) && Matches("boardingStart", start) && Matches("boardingEnd", end) &&
                Matches("orbitalSet", orbit) && Matches("orbitalSky", stars) && Matches("transitSkyShader", transitSky);
        }

        private static Scene OpenSavedBootstrap()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save the open integration scenes before applying the departure.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != I01BootstrapBuilder.ScenePath || SceneManager.sceneCount != 1)
                scene = EditorSceneManager.OpenScene(I01BootstrapBuilder.ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Could not open saved Bootstrap.");
            return scene;
        }

        private static void RequireIntegrationProject()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (Application.unityVersion != "6000.3.28f1" || EditorApplication.isPlayingOrWillChangePlaymode ||
                project.IndexOf("/.local/", StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException("Run this authoring in the .local integration project on Unity 6000.3.28f1 outside Play mode.");
        }

        private static void RequireMetreScale(Transform value)
        {
            for (Transform node = value; node; node = node.parent)
                if ((node.localScale - Vector3.one).sqrMagnitude > .00000001f)
                    throw new InvalidOperationException("Departure anchors require metre scale: " + node.name);
        }

        private static T ExactlyOne<T>(GameObject[] roots) where T : Component =>
            roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();

        private static Transform NamedRoot(GameObject[] roots, string name, bool required = true)
        {
            GameObject match = roots.SingleOrDefault(root => root.name == name);
            if (!match && required) throw new InvalidOperationException("Missing saved Bootstrap root: " + name);
            return match ? match.transform : null;
        }

        private static Transform ChildNamed(Transform root, string name)
        {
            Transform value = root.Find(name);
            if (!value) throw new InvalidOperationException("Missing saved Bootstrap child: " + root.name + "/" + name);
            return value;
        }

        private static Transform DescendantNamed(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).Single(node => node != root && node.name == name);

        private static Transform Marker(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var marker = new GameObject(name).transform;
            marker.SetParent(parent, false);
            marker.SetLocalPositionAndRotation(position, rotation);
            return marker;
        }

        private static void Shot(Transform parent, string name, Vector3 origin, Vector3 offset, Vector3 target) =>
            Marker(parent, name, origin + offset, Quaternion.LookRotation(target - offset, Vector3.up));

        private static T Required<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("Missing departure source asset: " + path);
    }
}
