using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OutOfReach.Presentation.Motion
{
    /// <summary>Samples the existing seated WORK skeleton and its four-point restraint from one departure playhead.</summary>
    internal sealed class DepartureSeatRestraint : IDisposable
    {
        private readonly Transform seatFrame, seatParent, bodyFrame;
        private readonly GameObject props;
        private readonly Mesh claspMesh;
        private readonly Material webbing, housing, tongue, release;
        private readonly RestraintSide left, right;
        private readonly SeatFoot leftFoot, rightFoot;

        internal bool IsUsable => props && bodyFrame && seatFrame && seatFrame.parent == seatParent &&
            seatFrame.gameObject.activeInHierarchy && (seatFrame.lossyScale - Vector3.one).sqrMagnitude < .000001f;
        internal bool IsLatched { get; private set; }

        internal DepartureSeatRestraint(Transform bodyFrame, Transform seatFrame,
            Dictionary<string, Transform> bones, Shader litShader)
        {
            this.bodyFrame = bodyFrame;
            this.seatFrame = seatFrame;
            seatParent = seatFrame.parent;
            var leftArm = new SeatArm(bones, "L", -1f);
            var rightArm = new SeatArm(bones, "R", 1f);
            leftFoot = new SeatFoot(bones, "L");
            rightFoot = new SeatFoot(bones, "R");
            props = new GameObject("WORK departure restraint") { hideFlags = HideFlags.DontSave };
            props.SetActive(false);
            props.transform.SetParent(bodyFrame, false);
            try
            {
                webbing = Finish(litShader, "Amber woven restraint", new Color(.43f, .162f, .052f), .12f, 0f);
                housing = Finish(litShader, "Graphite buckle housing", new Color(.105f, .12f, .13f), .28f, .35f);
                tongue = Finish(litShader, "Steel buckle tongue", new Color(.43f, .46f, .48f), .48f, .69f);
                release = Finish(litShader, "Amber buckle release", new Color(.87f, .31f, .065f), .24f, .03f);
                claspMesh = MakeClaspMesh();
                left = new RestraintSide(-1, "L", leftArm, props.transform, claspMesh, webbing, housing, tongue, release);
                right = new RestraintSide(1, "R", rightArm, props.transform, claspMesh, webbing, housing, tongue, release);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        // Sample a frame independently. There is no integration of Unity delta time or motion input.
        internal void Activate()
        {
            FollowSeat();
            left.Arm.Capture(bodyFrame);
            right.Arm.Capture(bodyFrame);
            // The existing transport's deck plates finish at 12 mm. The held seat clip rests
            // its own boot soles at origin; lowering both boots keeps them on the real deck
            // when the body frame is raised to meet the cushion. No change to pelvis or chair.
            float soleDropMetres = Mathf.Clamp(seatFrame.localPosition.y - .012f, 0f, .17f);
            leftFoot.Plant(bodyFrame, soleDropMetres);
            rightFoot.Plant(bodyFrame, soleDropMetres);
            Present(0f);
            props.SetActive(true);
        }

        internal void Present(float seconds)
        {
            FollowSeat();
            var receiver = SeatAction.Sample(seconds, true);
            var insert = SeatAction.Sample(seconds, false);
            left.Present(bodyFrame, receiver);
            right.Present(bodyFrame, insert);
            IsLatched = insert.Draw == 1f;
        }

        private void FollowSeat() => bodyFrame.SetPositionAndRotation(seatFrame.position, seatFrame.rotation);

        public void Dispose()
        {
            if (props) props.SetActive(false);
            left?.Dispose();
            right?.Dispose();
            Object.Destroy(props);
            Object.Destroy(claspMesh);
            Object.Destroy(webbing);
            Object.Destroy(housing);
            Object.Destroy(tongue);
            Object.Destroy(release);
        }

        internal readonly struct SeatAction
        {
            internal readonly float Reach, Draw, Grip;

            private SeatAction(float reach, float draw, float grip)
            { Reach = reach; Draw = draw; Grip = grip; }

            internal static SeatAction Sample(float seconds, bool receiver)
            {
                float reach = receiver ? Beat(seconds, 1.18f, 1.98f) * (1f - Beat(seconds, 3.46f, 4.05f))
                    : Beat(seconds, 1.42f, 2.18f) * (1f - Beat(seconds, 3.14f, 3.82f));
                float draw = receiver ? Beat(seconds, 1.98f, 2.43f) : Beat(seconds, 2.18f, 2.83f);
                float grip = receiver ? Beat(seconds, 1.83f, 2.06f) * (1f - Beat(seconds, 3.27f, 3.58f))
                    : Beat(seconds, 2.02f, 2.24f) * (1f - Beat(seconds, 3.05f, 3.42f));
                return new SeatAction(reach, draw, grip);
            }

            private static float Beat(float seconds, float start, float end)
            {
                float u = Mathf.Clamp01((seconds - start) / (end - start));
                return u * u * (3f - 2f * u);
            }
        }

        private sealed class RestraintSide : IDisposable
        {
            private readonly float side;
            private readonly Ribbon shoulder, lap;
            private readonly Transform clasp;
            internal readonly SeatArm Arm;

            internal RestraintSide(float side, string suffix, SeatArm arm, Transform props,
                Mesh box, Material webbing, Material housing, Material tongue, Material release)
            {
                this.side = side;
                Arm = arm;
                shoulder = new Ribbon("Shoulder webbing " + suffix, props, webbing, 6, .049f, Vector3.right);
                lap = new Ribbon("Lap webbing " + suffix, props, webbing, 4, .046f, Vector3.up);
                clasp = Child(side < 0 ? "Receiver half" : "Tongue half", props);
                Box("Buckle shell", clasp, box, housing, Vector3.zero, new Vector3(.057f, .024f, .060f));
                if (side < 0)
                    Box("Release button", clasp, box, release, new Vector3(.004f, .014f, 0f), new Vector3(.032f, .006f, .034f));
                else
                    Box("Latching tongue", clasp, box, tongue, new Vector3(-.027f, -.002f, .002f), new Vector3(.038f, .009f, .038f));
            }

            internal void Present(Transform body, SeatAction action)
            {
                // The open ends rest over their own armrest. Their two housings meet at the waist.
                Vector3 stowed = new Vector3(side * .274f, .670f, .290f);
                Vector3 closed = new Vector3(side * .0285f, .646f, .315f);
                Vector3 end = Vector3.Lerp(stowed, closed, action.Draw);
                end.y += .037f * 4f * action.Draw * (1f - action.Draw);
                clasp.SetLocalPositionAndRotation(end, Quaternion.Euler(
                    Mathf.Lerp(-23f, -9f, action.Draw), side * Mathf.Lerp(18f, 0f, action.Draw), 0f));

                // Anchor behind the shoulder on the actual chair. Guide the belt clear of the flight
                // suit and let its last bend move with the physical buckle instead of snapping on.
                shoulder.Points[0] = new Vector3(side * .110f, 1.075f, -.346f);
                shoulder.Points[1] = new Vector3(side * .154f, 1.123f, -.075f);
                shoulder.Points[2] = new Vector3(side * .146f, 1.104f, .107f);
                shoulder.Points[3] = new Vector3(side * Mathf.Lerp(.196f, .134f, action.Draw), .975f, .139f);
                shoulder.Points[4] = new Vector3(side * Mathf.Lerp(.246f, .106f, action.Draw), .810f, .166f);
                shoulder.Points[5] = end + new Vector3(side * .011f, .007f, -.029f);
                shoulder.Apply();

                lap.Points[0] = new Vector3(side * .310f, .508f, .146f);
                lap.Points[1] = new Vector3(side * .278f, .570f, .194f);
                lap.Points[2] = Vector3.Lerp(new Vector3(side * .266f, .613f, .222f),
                    new Vector3(side * .164f, .642f, .294f), action.Draw);
                lap.Points[3] = end + new Vector3(side * .025f, -.011f, .006f);
                lap.Apply();

                // Palm pad is measured from this rig's wrist and metacarpal, then solved to
                // the outside of its own moving hardware. The fingers close only over that hardware.
                Vector3 gripOffset = side < 0f ? new Vector3(-.042f, .030f, -.006f)
                    : new Vector3(.039f, .024f, .021f);
                Vector3 grip = end + clasp.localRotation * gripOffset;
                Arm.Present(body, grip, action.Reach, action.Grip);
            }

            public void Dispose() { shoulder.Dispose(); lap.Dispose(); }
        }

        private sealed class SeatFoot
        {
            private readonly Transform upper, lower, ankle;
            internal SeatFoot(Dictionary<string, Transform> bones, string suffix)
            {
                upper = bones["upperleg01." + suffix];
                lower = bones["lowerleg01." + suffix];
                ankle = bones["foot." + suffix];
            }

            internal void Plant(Transform body, float lowerMetres)
            {
                if (lowerMetres <= 0f) return;
                Vector3 hip = body.InverseTransformPoint(upper.position);
                Vector3 knee = body.InverseTransformPoint(lower.position);
                Vector3 foot = body.InverseTransformPoint(ankle.position);
                Vector3 pole = Vector3.ProjectOnPlane(knee - hip, foot - hip).normalized;
                Quaternion authoredOrientation = ankle.rotation;
                Bend(body, upper, lower, ankle, hip, foot - Vector3.up * lowerMetres, pole,
                    Vector3.Distance(hip, knee), Vector3.Distance(knee, foot));
                ankle.rotation = authoredOrientation;
            }
        }

        private sealed class SeatArm
        {
            private readonly Transform upper, lower, wrist;
            private readonly Transform[] digits = new Transform[15];
            private readonly Quaternion[] digitBaseline = new Quaternion[15];
            private readonly float side;
            private Quaternion upperBaseline, lowerBaseline, wristBaseline, authoredWristRotation;
            private Vector3 authoredWrist, authoredElbowPole, palmInWrist;
            private float upperLength, lowerLength;

            internal SeatArm(Dictionary<string, Transform> bones, string suffix, float side)
            {
                this.side = side;
                upper = bones["upperarm01." + suffix];
                lower = bones["lowerarm01." + suffix];
                wrist = bones["wrist." + suffix];
                for (int digit = 0; digit < 5; digit++)
                    for (int joint = 0; joint < 3; joint++)
                        digits[digit * 3 + joint] = bones[$"finger{digit + 1}-{joint + 1}.{suffix}"];
            }

            internal void Capture(Transform body)
            {
                upperBaseline = upper.localRotation;
                lowerBaseline = lower.localRotation;
                wristBaseline = wrist.localRotation;
                for (int i = 0; i < digits.Length; i++) digitBaseline[i] = digits[i].localRotation;
                authoredWrist = body.InverseTransformPoint(wrist.position);
                authoredWristRotation = Quaternion.Inverse(body.rotation) * wrist.rotation;
                Vector3 shoulder = body.InverseTransformPoint(upper.position);
                Vector3 elbow = body.InverseTransformPoint(lower.position);
                upperLength = Vector3.Distance(shoulder, elbow);
                lowerLength = Vector3.Distance(elbow, authoredWrist);
                authoredElbowPole = Vector3.ProjectOnPlane(elbow - shoulder, authoredWrist - shoulder).normalized;
                Vector3 palm = wrist.InverseTransformPoint(digits[6].position);
                palmInWrist = palm * .76f;
            }

            internal void Present(Transform body, Vector3 grip, float reach, float curl)
            {
                upper.localRotation = upperBaseline;
                lower.localRotation = lowerBaseline;
                wrist.localRotation = wristBaseline;
                for (int i = 0; i < digits.Length; i++) digits[i].localRotation = digitBaseline[i];
                if (reach <= 0f) return;

                Quaternion wristRotation = Quaternion.AngleAxis(-side * (side < 0f ? 15f : 23f) * reach, Vector3.up)
                    * authoredWristRotation;
                Vector3 targetWrist = Vector3.Lerp(authoredWrist, grip - wristRotation * palmInWrist, reach);

                Vector3 shoulder = body.InverseTransformPoint(upper.position);
                Vector3 pole = Vector3.Slerp(authoredElbowPole, new Vector3(side * .64f, -.59f, -.49f).normalized, .64f * reach);
                Bend(body, upper, lower, wrist, shoulder, targetWrist, pole, upperLength, lowerLength);
                wrist.rotation = body.rotation * wristRotation;
                if (curl <= 0f) return;

                Vector3 across = digits[3].position - digits[12].position;
                Vector3 palmNormal = Vector3.Cross(across, digits[6].position - wrist.position).normalized;
                if (Vector3.Dot(palmNormal, -side * body.right) < 0f) palmNormal = -palmNormal;
                for (int digit = 0; digit < 5; digit++)
                    for (int joint = 0; joint < 2; joint++)
                    {
                        Transform node = digits[digit * 3 + joint];
                        Vector3 direction = digits[digit * 3 + joint + 1].position - node.position;
                        Vector3 hinge = Vector3.Cross(direction, palmNormal).normalized;
                        if (hinge.sqrMagnitude <= .00001f) continue;
                        float amount = digit == 0 ? 12f : joint == 0 ? 21f : 29f;
                        node.rotation = Quaternion.AngleAxis(amount * curl, hinge) * node.rotation;
                    }
            }

        }

        private static void Bend(Transform body, Transform upper, Transform lower, Transform end,
            Vector3 origin, Vector3 target, Vector3 pole, float upperLength, float lowerLength)
        {
            Vector3 axis = (target - origin).normalized;
            float reach = Mathf.Clamp(Vector3.Distance(origin, target),
                Mathf.Abs(upperLength - lowerLength) + .001f, upperLength + lowerLength - .001f);
            float along = (upperLength * upperLength - lowerLength * lowerLength + reach * reach) / (2f * reach);
            float perpendicular = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 joint = origin + axis * along + Vector3.ProjectOnPlane(pole, axis).normalized * perpendicular;
            Aim(upper, lower, body.TransformPoint(joint));
            Aim(lower, end, body.TransformPoint(origin + axis * reach));
        }

        private static void Aim(Transform joint, Transform child, Vector3 target) =>
            joint.rotation = Quaternion.FromToRotation(child.position - joint.position, target - joint.position) * joint.rotation;

        private sealed class Ribbon : IDisposable
        {
            internal readonly Vector3[] Points;
            private readonly Vector3[] vertices;
            private readonly Mesh mesh;
            private readonly float halfWidth;
            private readonly Vector3 widthAxis;

            internal Ribbon(string name, Transform parent, Material material, int pointCount, float width, Vector3 across)
            {
                Points = new Vector3[pointCount];
                vertices = new Vector3[pointCount * 4];
                halfWidth = width * .5f;
                widthAxis = across;
                mesh = new Mesh { name = name };
                mesh.MarkDynamic();
                mesh.vertices = vertices;
                var triangles = new List<int>((pointCount - 1) * 24 + 12);
                for (int ring = 1; ring < pointCount; ring++)
                    for (int side = 0; side < 4; side++)
                    {
                        int a = (ring - 1) * 4 + side, b = (ring - 1) * 4 + ((side + 1) % 4);
                        triangles.AddRange(new[] { a, b + 4, b, a, a + 4, b + 4 });
                    }
                int last = vertices.Length - 4;
                triangles.AddRange(new[] { 0, 1, 2, 0, 2, 3, last, last + 2, last + 1, last, last + 3, last + 2 });
                mesh.SetTriangles(triangles, 0);
                MeshAt(Child(name, parent), mesh, material);
            }

            internal void Apply()
            {
                for (int i = 0; i < Points.Length; i++)
                {
                    Vector3 tangent = (Points[Mathf.Min(i + 1, Points.Length - 1)] -
                        Points[Mathf.Max(0, i - 1)]).normalized;
                    Vector3 width = Vector3.ProjectOnPlane(widthAxis, tangent).normalized;
                    if (width.sqrMagnitude < .01f)
                        width = Vector3.ProjectOnPlane(Vector3.forward, tangent).normalized;
                    Vector3 across = width * halfWidth;
                    Vector3 depth = Vector3.Cross(width, tangent).normalized * .0023f;
                    int j = i * 4;
                    vertices[j] = Points[i] - across - depth;
                    vertices[j + 1] = Points[i] + across - depth;
                    vertices[j + 2] = Points[i] + across + depth;
                    vertices[j + 3] = Points[i] - across + depth;
                }
                mesh.vertices = vertices;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            }

            public void Dispose() => Object.Destroy(mesh);
        }

        private static Transform Child(string name, Transform parent)
        {
            var part = new GameObject(name) { hideFlags = HideFlags.DontSave };
            part.transform.SetParent(parent, false);
            return part.transform;
        }

        private static void MeshAt(Transform part, Mesh mesh, Material material)
        {
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void Box(string name, Transform parent, Mesh mesh, Material material, Vector3 offset, Vector3 size)
        {
            Transform part = Child(name, parent);
            part.localPosition = offset;
            part.localScale = size;
            MeshAt(part, mesh, material);
        }

        private static Material Finish(Shader shader, string name, Color color, float smoothness, float metallic)
        {
            var material = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            return material;
        }

        private static Mesh MakeClaspMesh()
        {
            // Chamfered profile, 10% cut corners in plan, with a closed prism and native lighting.
            var vertices = new Vector3[16];
            var profile = new[]
            {
                new Vector2(-.5f, -.37f), new Vector2(-.37f, -.5f),
                new Vector2(.37f, -.5f), new Vector2(.5f, -.37f),
                new Vector2(.5f, .37f), new Vector2(.37f, .5f),
                new Vector2(-.37f, .5f), new Vector2(-.5f, .37f)
            };
            var triangles = new List<int>(84);
            for (int i = 0; i < 8; i++)
            {
                vertices[i] = new Vector3(profile[i].x, -.5f, profile[i].y);
                vertices[i + 8] = new Vector3(profile[i].x, .5f, profile[i].y);
                int j = (i + 1) % 8;
                triangles.AddRange(new[] { i, i + 8, j + 8, i, j + 8, j });
                if (i < 1 || i > 6) continue;
                triangles.AddRange(new[] { 0, i, i + 1, 8, i + 9, i + 8 });
            }
            var mesh = new Mesh { name = "Departure restraint hardware", vertices = vertices };
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
