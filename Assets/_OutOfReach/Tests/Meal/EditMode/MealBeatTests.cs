using System;
using System.Collections.Generic;
using NUnit.Framework;
using OutOfReach.Presentation.Meal;
using UnityEditor;
using UnityEngine;

namespace OutOfReach.Tests.Meal.EditMode
{
    /// <summary>
    /// R35, the readable first-person meal: the new beat order around the one real command, both hands on the tear, three
    /// squeezed sips at the mouth with a small head nod, the torn strip as one ribbon, and the delivered original foley.
    /// Pure choreography and schedule; the presenter is covered in PlayMode.
    /// </summary>
    public sealed class MealBeatTests
    {
        private static readonly MealPreparation[] Kinds = { MealPreparation.ReadyToEat, MealPreparation.Warmed, MealPreparation.Rehydrated };

        private static MealStations Galley() => new MealStations
        {
            EyePosition = new Vector3(-1.2f, 1.62f, 14.4f),
            EyeRotation = Quaternion.LookRotation(Vector3.left),
            DrawerPosition = new Vector3(-1.64f, 1.72f, 14.395f),
            DrawerRotation = Quaternion.LookRotation(Vector3.right),
            Nozzle = new Vector3(-2.08f, 1.13f, 15.195f),
            Warmer = new Vector3(-2.2f, .48f, 15.15f),
            Trash = new Vector3(-2.13f, .70f, 14.35f),
            IntoGalley = Vector3.left,
            Up = Vector3.up,
        };

        private static bool Near(float value, float boundary) => Mathf.Abs(value - boundary) < .004f;

        [Test]
        public void BeatsComeInTheNewOrderAroundTheOneRealCommand()
        {
            foreach (MealPreparation kind in Kinds)
                foreach (bool repeated in new[] { false, true })
                {
                    string what = kind + (repeated ? ", repeated menu" : "");
                    MealTimeline line = MealTimeline.For(kind, repeated);
                    MealBeatMark[] marks = MealBeats.For(kind, repeated);
                    var order = new List<string>();
                    foreach (MealBeatMark mark in marks) order.Add(mark.ToString());
                    var expected = new List<string> { "Grasp" };
                    if (kind != MealPreparation.Warmed) expected.Add("Knead");
                    expected.AddRange(new[] { "Pinch", "Tear", "Sip 1", "Swallow 1", "Sip 2", "Swallow 2", "Sip 3", "Swallow 3", "Stow" });
                    Assert.That(order, Is.EqualTo(expected), what + ": grasp, tear, three sips with their swallows, stow");

                    double previous = -1d, tear = double.NaN, firstSip = double.NaN;
                    foreach (MealBeatMark mark in marks)
                    {
                        double at = mark.SecondsIn(line);
                        Assert.That(at, Is.GreaterThan(previous + .05d), what + ": " + mark + " follows the beat before it");
                        Assert.That(line.StageAt(at), Is.EqualTo(mark.Stage), what + ": " + mark + " lies in its stage");
                        Assert.That(at, Is.LessThan(line.Total));
                        if (mark.Beat == MealBeat.Tear) tear = at;
                        if (mark.Beat == MealBeat.Sip && mark.Index == 0) firstSip = at;
                        previous = at;
                    }
                    // The pouch is torn open before the first bite, and nothing is drunk before Core debited the ration.
                    double tearEnd = line.StageStart(MealStage.Open) + MealChoreography.OpenStageProgress(MealChoreography.TearEnd, repeated) * line.Open;
                    Assert.That(tear, Is.LessThan(tearEnd));
                    Assert.That(tearEnd, Is.LessThan(line.CommitAt), what + ": the strip is off before the first bite");
                    Assert.That(firstSip, Is.GreaterThan(line.CommitAt), what + ": the first sip comes after the first bite");
                }

            // R35 changes no pacing: the reviewed stage lengths and the moment of the real command are the same.
            Assert.That(MealTimeline.For(MealPreparation.Warmed).CommitAt, Is.EqualTo(5.0d).Within(1e-9));
            Assert.That(MealTimeline.For(MealPreparation.Warmed).Total, Is.EqualTo(8.8d).Within(1e-9));
            Assert.That(MealTimeline.For(MealPreparation.Rehydrated).CommitAt, Is.EqualTo(5.2d).Within(1e-9));
            Assert.That(MealTimeline.For(MealPreparation.Rehydrated).Total, Is.EqualTo(9.0d).Within(1e-9));
            Assert.That(MealTimeline.For(MealPreparation.ReadyToEat).CommitAt, Is.EqualTo(3.2d).Within(1e-9));
            Assert.That(MealTimeline.For(MealPreparation.ReadyToEat).Total, Is.EqualTo(7.0d).Within(1e-9));
            Assert.That(MealTimeline.For(MealPreparation.ReadyToEat, true).Total, Is.EqualTo(7.7d).Within(1e-9));
        }

        [Test]
        public void BothHandsTearThePouchOpenBeforeTheFirstBite()
        {
            MealStations galley = Galley();
            foreach (MealPreparation kind in Kinds)
                foreach (bool repeated in new[] { false, true })
                {
                    string what = kind + (repeated ? ", repeated menu" : "");
                    var path = new MealChoreography(galley, kind, repeated);
                    MealPouchShape shape = path.Shape;

                    // The left arm rests during the preparation and comes up beside the top corner at its end.
                    Assert.That(path.Sample(MealStage.Prepare, .3d).OffHandWeight, Is.Zero, what);
                    MealPouchFrame waiting = path.Sample(MealStage.Prepare, 1d);
                    Assert.That(waiting.OffHandWeight, Is.GreaterThan(.8f), what + ": the left hand waits beside the pouch");
                    Assert.That(waiting.Opened || waiting.StripVisible || waiting.Tear > 0f, Is.False, what);

                    float previous = 0f;
                    bool peeled = false;
                    for (int i = 0; i <= 250; i++)
                    {
                        float k = i / 250f;
                        if (Near(k, MealChoreography.PinchAt) || Near(k, MealChoreography.TearStart) || Near(k, MealChoreography.TearEnd)) continue;
                        MealPouchFrame frame = path.Sample(MealStage.Open, MealChoreography.OpenStageProgress(k, repeated));
                        Assert.That(frame.Tear, Is.GreaterThanOrEqualTo(previous - 1e-5f), what + ": the tear never goes back");
                        previous = frame.Tear;
                        Assert.That(frame.OffHandWeight, Is.GreaterThan(.85f), what + ": the left hand stays on the pouch while it is opened");
                        Assert.That(frame.HandWeight, Is.EqualTo(1f), what + ": the right hand holds it");
                        if (k < MealChoreography.TearStart)
                        {
                            Assert.That(frame.Opened || frame.StripVisible, Is.False, what + ": sealed until the tear starts");
                            Assert.That(frame.Tear, Is.Zero);
                        }
                        else Assert.That(frame.Opened && frame.StripVisible, Is.True, what + ": the strip shows while it is torn");
                        if (k > MealChoreography.PinchAt)
                            Assert.That(frame.OffHandPinch, Is.EqualTo(1f).Within(1e-4f), what + ": thumb and index finger are closed on the strip");
                        if (k > MealChoreography.PinchAt && k < MealChoreography.TearEnd)
                        {
                            // The fingers hold the strip: the pinch is the ribbon's own held point.
                            Vector3 held = MealStripShape.Pinch(shape, frame.Tear, MealStripShape.PeelDirection);
                            Assert.That(Vector3.Distance(frame.OffHandLocal, held), Is.LessThan(.001f), what + ": the strip is in the fingers");
                            Assert.That(frame.OffHandAwayBlend, Is.Zero);
                        }
                        if (frame.Tear > .05f && frame.Tear < .95f) peeled = true;
                    }
                    Assert.That(peeled, Is.True, what + ": the strip peels over several frames, it does not pop");
                    Assert.That(previous, Is.EqualTo(1f).Within(1e-4f), what + ": the strip is off before the first bite");

                    // The pinch closes on the strip's own band at the viewer's left end.
                    Vector3 corner = path.Sample(MealStage.Open, MealChoreography.OpenStageProgress(MealChoreography.PinchAt + .01f, repeated)).OffHandLocal;
                    Assert.That(corner.x, Is.InRange(shape.HalfWidth - .03f, shape.HalfWidth), what);
                    Assert.That(corner.y, Is.InRange(shape.StripBottom, shape.StripTop), what);

                    // Then the left hand takes the strip out of the view and the arm is given back.
                    MealPouchFrame taken = path.Sample(MealStage.Eat, MealChoreography.OffHandGoneAt * .25f);
                    Assert.That(taken.OffHandAwayBlend, Is.GreaterThan(MealChoreography.AwayShareWhileOpening));
                    MealPouchFrame gone = path.Sample(MealStage.Eat, MealChoreography.OffHandGoneAt + .01f);
                    Assert.That(gone.OffHandWeight, Is.EqualTo(0f).Within(1e-4f), what + ": the left arm is given back");
                    Assert.That(gone.StripVisible, Is.False, what);
                    Vector3 away = Quaternion.Inverse(galley.EyeRotation) * (gone.OffHandAway - galley.EyePosition);
                    Assert.That(Mathf.Abs(away.y) / away.z, Is.GreaterThan(Mathf.Tan(34f * Mathf.Deg2Rad)), what + ": the strip leaves the view");
                }
        }

        [Test]
        public void ThePouchIsTippedToTheMouthForThreeSqueezedSipsWithASmallNod()
        {
            MealStations galley = Galley();
            Vector3 mouth = galley.EyePosition + galley.EyeRotation * MealChoreography.MouthKey;
            Assert.That(MealChoreography.MouthKey.y, Is.LessThan(0f), "the mouth is below the eye");
            foreach (MealPreparation kind in Kinds)
            {
                var path = new MealChoreography(galley, kind, false);
                float raised = Vector3.Distance(path.Sample(MealStage.Eat, MealChoreography.RaiseEnd).Position, mouth);
                float emptied = 0f;
                for (int sip = 0; sip < MealChoreography.SipCount; sip++)
                {
                    MealPouchFrame at = path.Sample(MealStage.Eat, MealChoreography.SipProgress(sip, .5f));
                    Assert.That(at.Squeeze, Is.GreaterThan(.95f), kind + " sip " + sip + ": squeezed at the lips");
                    Assert.That(Vector3.Distance(at.Position, mouth), Is.LessThan(raised - .05f), kind + ": a sip brings the pouch to the mouth");
                    Assert.That(Vector3.Distance(at.Position, galley.EyePosition), Is.LessThan(.20f));
                    // The torn opening (pouch +y) points at the mouth.
                    Assert.That(Vector3.Dot(at.Rotation * Vector3.up, (mouth - at.Position).normalized), Is.GreaterThan(.8f), kind + ": tipped to the mouth");
                    Assert.That(at.Empty, Is.GreaterThan(emptied));
                    emptied = at.Empty;
                    Assert.That(at.Thickness, Is.LessThan(1f));
                    MealPouchFrame between = path.Sample(MealStage.Eat, MealChoreography.SipProgress(sip, .999f));
                    Assert.That(between.Squeeze, Is.EqualTo(0f).Within(.01f));
                    Assert.That(Vector3.Distance(between.Position, mouth), Is.GreaterThan(Vector3.Distance(at.Position, mouth) + .02f), kind + ": it comes back between sips");
                    Assert.That(between.Empty, Is.EqualTo((sip + 1f) / MealChoreography.SipCount).Within(.01f));
                }

                // A small nod only: down to the pouch, back while squeezing, and level again when the eating ends.
                float down = 0f, back = 0f;
                for (int i = 0; i <= 500; i++)
                {
                    MealPouchFrame frame = path.Sample(MealStage.Eat, i / 500d);
                    down = Mathf.Max(down, frame.NodDegrees);
                    back = Mathf.Min(back, frame.NodDegrees);
                    Assert.That(frame.Opened && frame.Visible, Is.True);
                    Assert.That(frame.Thickness, Is.InRange(.5f, 1.0001f));
                }
                Assert.That(down, Is.InRange(1f, 3f), kind + ": the head comes down a little");
                Assert.That(back, Is.InRange(-2.5f, -.5f), kind + ": and tips back a little");
                Assert.That(path.Sample(MealStage.Eat, 0d).NodDegrees, Is.Zero);
                Assert.That(path.Sample(MealStage.Eat, MealChoreography.SipsEnd + .01f).NodDegrees, Is.Zero);
                foreach (MealStage stage in new[] { MealStage.Take, MealStage.Prepare, MealStage.Open, MealStage.Stow })
                    for (int i = 0; i <= 20; i++)
                        Assert.That(path.Sample(stage, i / 20d).NodDegrees, Is.Zero, kind + ": the view is still outside the sips (" + stage + ")");

                // The emptied pouch is flat when it is lowered and stowed.
                Assert.That(path.Sample(MealStage.Eat, 1d).Thickness, Is.EqualTo(MealChoreography.EmptyThickness).Within(1e-4f));
                MealPouchFrame stowed = path.Sample(MealStage.Stow, .3d);
                Assert.That(stowed.Empty, Is.EqualTo(1f));
                Assert.That(stowed.Thickness, Is.EqualTo(MealChoreography.EmptyThickness).Within(1e-4f));
            }
        }

        [Test]
        public void TheWholeMealMovesWithoutJumps()
        {
            MealStations galley = Galley();
            var stages = new[] { MealStage.Take, MealStage.Prepare, MealStage.Open, MealStage.Eat, MealStage.Stow };
            foreach (MealPreparation kind in Kinds)
                foreach (bool repeated in new[] { false, true })
                {
                    var path = new MealChoreography(galley, kind, repeated);
                    MealTimeline line = MealTimeline.For(kind, repeated);
                    bool first = true;
                    MealPouchFrame previous = default;
                    foreach (MealStage stage in stages)
                    {
                        // 120 samples a second of the stage's own length: faster than any frame the game shows.
                        int steps = Mathf.Max(2, Mathf.CeilToInt((float)line.StageLength(stage) * 120f));
                        for (int i = 0; i <= steps; i++)
                        {
                            MealPouchFrame frame = path.Sample(stage, i / (double)steps);
                            if (!frame.Visible) break; // released into the wet-trash bag
                            if (!first)
                            {
                                string where = kind + (repeated ? " repeated " : " ") + stage + " " + i + "/" + steps;
                                Assert.That(Vector3.Distance(previous.Position, frame.Position), Is.LessThan(.03f), where + ": the pouch does not jump");
                                Assert.That(Quaternion.Angle(previous.Rotation, frame.Rotation), Is.LessThan(12f), where + ": the pouch does not flip");
                                Assert.That(Mathf.Abs(previous.Thickness - frame.Thickness), Is.LessThan(.05f), where + ": the pouch does not pop thicker or thinner");
                                Assert.That(Mathf.Abs(previous.OffHandWeight - frame.OffHandWeight), Is.LessThan(.12f), where + ": the left arm blends in and out");
                                Assert.That(Mathf.Abs(previous.NodDegrees - frame.NodDegrees), Is.LessThan(.35f), where + ": the nod is smooth");
                            }
                            previous = frame;
                            first = false;
                        }
                    }
                }
        }

        [Test]
        public void TheTornStripIsOneRibbonThatEndsInTheFingers()
        {
            MealPouchShape shape = MealPouchShape.Default;
            Assert.That(shape.IsValid, Is.True);
            Assert.That(shape.Width, Is.EqualTo(.141f).Within(1e-4f), "the GALLEY-PROPS-01 pouch width");
            Assert.That(shape.StripHeight, Is.InRange(.03f, .05f), "the band between the opened cut and the sealed top");
            Assert.That(new MealPouchShape(.07f, .09f, .09f).IsValid, Is.False);
            Vector3 peel = MealStripShape.PeelDirection;
            Assert.That(peel.z, Is.GreaterThan(.5f), "the strip is pulled towards the eye");
            foreach (float tear in new[] { 0f, .08f, .2f, .35f, .6f, .85f, 1f })
            {
                // One ribbon: its length does not change while it is torn off.
                float length = 0f;
                Vector3 previous = MealStripShape.Centre(shape, tear, 0f, peel, out _);
                for (int i = 1; i <= 600; i++)
                {
                    Vector3 point = MealStripShape.Centre(shape, tear, i / 600f, peel, out Vector3 tangent);
                    Assert.That(tangent.magnitude, Is.EqualTo(1f).Within(1e-3f));
                    length += Vector3.Distance(previous, point);
                    previous = point;
                }
                Assert.That(length, Is.EqualTo(shape.Width).Within(shape.Width * .03f), "tear " + tear);

                // What still holds lies on the cut; what is torn has left the pouch plane towards the eye.
                Vector3 front = MealStripShape.TearFront(shape, tear);
                Assert.That(front.z, Is.Zero);
                Assert.That(front.x, Is.EqualTo(shape.HalfWidth - tear * shape.Width).Within(1e-5f), "the tear runs from the viewer's left to the right");
                Vector3 held = MealStripShape.Pinch(shape, tear, peel);
                if (tear <= 0f)
                {
                    Assert.That(held.z, Is.Zero, "sealed: the fingers are on the pouch");
                    Assert.That(held.y, Is.InRange(shape.SealBottom, shape.StripTop), "on its flat top seal");
                }
                if (tear * shape.Width > MealStripShape.PinchInset + MealStripShape.BendLength) Assert.That(held.z, Is.GreaterThan(.004f), "tear " + tear + ": peeled off the pouch");
                // The held point is on the ribbon: no further from the tear front than the torn length (and the seal's height).
                Assert.That(Vector3.Distance(held, front), Is.LessThanOrEqualTo(
                    Mathf.Max(tear * shape.Width, MealStripShape.PinchInset) + MealStripShape.PinchHeight(shape) + 1e-4f));
            }

            // The cross-section is the sealed pouch's top: two walls as far apart as the pouch at the cut, meeting in the seal.
            var profile = new Vector2[5];
            MealStripShape.Profile(shape, profile);
            Assert.That(profile[0].y, Is.EqualTo(shape.CutHalfThickness).Within(1e-6f));
            Assert.That(profile[4].y, Is.EqualTo(-shape.CutHalfThickness).Within(1e-6f));
            Assert.That(profile[0].x, Is.EqualTo(shape.StripBottom - shape.StripMiddle).Within(1e-6f));
            Assert.That(profile[2].x, Is.EqualTo(shape.StripTop - shape.StripMiddle).Within(1e-6f));
            Assert.That(profile[2].y, Is.Zero);
            Assert.That(Mathf.Abs(profile[1].y), Is.LessThan(.002f), "the seal is flat");
        }

        [Test]
        public void TheFoleyIsDeliveredOriginalAndFitsItsBeats()
        {
            var clips = new Dictionary<string, AudioClip>();
            foreach (string path in new[] { MealFoley.GrabClip, MealFoley.TearClip, MealFoley.SqueezeClip, MealFoley.SwallowClip })
            {
                AudioClip clip = Resources.Load<AudioClip>(path);
                Assert.That(clip, Is.Not.Null, "Resources/" + path);
                Assert.That(clip.channels, Is.EqualTo(1), path);
                Assert.That(clip.frequency, Is.EqualTo(48000), path);
                Assert.That(clip.length, Is.InRange(.2f, .5f), path);
                clips[path] = clip;
            }

            MealTimeline line = MealTimeline.For(MealPreparation.Warmed);
            // The tear sound covers the tear and has ended by the first bite.
            float tearing = (MealChoreography.TearEnd - MealChoreography.TearStart) * (float)line.Open;
            Assert.That(clips[MealFoley.TearClip].length, Is.InRange(tearing - .05f, tearing + .12f));
            Assert.That(clips[MealFoley.TearClip].length, Is.LessThan((1f - MealChoreography.TearStart) * (float)line.Open));
            // A squeeze ends before its swallow, and the swallow before the next squeeze.
            float sip = MealChoreography.SipLength * (float)line.Eat;
            Assert.That(clips[MealFoley.SqueezeClip].length, Is.LessThan((MealChoreography.SipSwallowAt - MealChoreography.SipSqueezeFrom) * sip + .02f));
            Assert.That(clips[MealFoley.SwallowClip].length, Is.LessThan((1f - MealChoreography.SipSwallowAt + MealChoreography.SipSqueezeFrom) * sip));

            // Every audible beat has its clip and a level; the pinch itself is silent.
            foreach (MealBeat beat in Enum.GetValues(typeof(MealBeat)))
            {
                if (beat == MealBeat.Pinch) { Assert.That(MealFoley.ClipOf(beat), Is.Null); continue; }
                Assert.That(clips.ContainsKey(MealFoley.ClipOf(beat)), Is.True, beat.ToString());
                for (int index = 0; index < MealChoreography.SipCount; index++)
                    Assert.That(MealFoley.GainOf(new MealBeatMark(beat, index, MealStage.Eat, 0f)), Is.InRange(.3f, 1f), beat.ToString());
            }
            Assert.That(MealFoley.ClipOf(MealBeat.Tear), Is.EqualTo(MealFoley.TearClip));
            Assert.That(MealFoley.ClipOf(MealBeat.Sip), Is.EqualTo(MealFoley.SqueezeClip));
            Assert.That(MealFoley.ClipOf(MealBeat.Swallow), Is.EqualTo(MealFoley.SwallowClip));

            // Original synthesis with its generator and provenance beside it; nothing downloaded.
            const string folder = "Assets/_OutOfReach/Audio/Original/MealFoley/";
            var provenance = AssetDatabase.LoadAssetAtPath<TextAsset>(folder + "audio-provenance.json");
            Assert.That(provenance, Is.Not.Null);
            Assert.That(provenance.text, Does.Contain("No recordings"));
            foreach (string name in new[] { "meal-pouch-grab.wav", "meal-pouch-tear.wav", "meal-pouch-squeeze.wav", "meal-swallow.wav" })
                Assert.That(provenance.text, Does.Contain(name));
            Assert.That(System.IO.File.Exists(folder + "generate_meal_foley.py"), Is.True);
        }
    }
}
