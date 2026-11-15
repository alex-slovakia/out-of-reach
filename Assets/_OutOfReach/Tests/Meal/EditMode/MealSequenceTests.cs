using System;
using System.Collections.Generic;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Presentation.Meal;
using UnityEditor;
using UnityEngine;

namespace OutOfReach.Tests.Meal.EditMode
{
    /// <summary>MEAL-PRES-01: the meal decorator around the real ration command. Domain-free fakes, observable rules only.</summary>
    public sealed class MealSequenceTests
    {
        private sealed class FakeRation : IInteractable
        {
            public bool Available = true;
            public bool Succeeds = true;
            public int Calls;
            public Action DuringCall;
            public string InteractionId => "survival-ration";
            public string Prompt => Available ? "Eat stored ration: 0.6133 kg" : "No ration is needed yet.";
            public bool CanInteract => Available;
            public CommandResult TryInteract()
            {
                Calls++;
                DuringCall?.Invoke();
                return Succeeds ? new CommandResult(true, CommandFailure.None, "Eat stored ration: 0.6133 kg")
                    : new CommandResult(false, CommandFailure.InsufficientResources, "Not enough food stored.");
            }
        }

        private FakeRation ration;
        private bool valid, paused;
        private string blocker;
        private MealSequence meal;
        private readonly List<string> log = new List<string>();

        [SetUp]
        public void SetUp()
        {
            ration = new FakeRation();
            valid = true; paused = false; blocker = null;
            log.Clear();
            meal = new MealSequence(ration, () => valid, () => paused, () => blocker, null, index => index >= 3);
            meal.Started += kind => log.Add("started " + kind);
            meal.Eaten += result => log.Add("eaten");
            meal.Finished += result => log.Add("finished");
            meal.Cancelled += (reason, eaten) => log.Add("cancelled " + eaten + ": " + reason);
        }

        private void AdvanceTo(double seconds)
        {
            while (meal.IsWorking && meal.ElapsedActiveSeconds < seconds - 1e-9)
                meal.Advance(Math.Min(.05d, seconds - meal.ElapsedActiveSeconds));
        }

        private void Finish() { for (int i = 0; i < 1000 && meal.IsWorking; i++) meal.Advance(.05d); }

        [Test]
        public void E_StartsTheMealWithoutCallingTheRealCommand()
        {
            CommandResult started = meal.TryInteract();
            Assert.That(started.Succeeded, Is.True, started.Explanation);
            Assert.That(meal.IsWorking && meal.Stage == MealStage.Take, Is.True);
            Assert.That(ration.Calls, Is.Zero, "E only starts the presentation");
            Assert.That(meal.Preparation, Is.EqualTo(MealPreparation.Warmed), "the first packaged meal is the warmed thermostabilised pouch");
        }

        [Test]
        public void TheRealRationRunsExactlyOnceAtTheFirstBite()
        {
            meal.TryInteract();
            AdvanceTo(meal.Timeline.CommitAt - .01d);
            Assert.That(ration.Calls, Is.Zero, "take, preparation and opening never grant food");
            Assert.That(meal.Stage, Is.EqualTo(MealStage.Open));
            meal.Advance(.02d);
            Assert.That(ration.Calls, Is.EqualTo(1));
            Assert.That(meal.Stage, Is.EqualTo(MealStage.Eat));
            Assert.That(meal.HasEaten, Is.True);
            Finish();
            Assert.That(ration.Calls, Is.EqualTo(1), "eating and stowing present the same ration");
            Assert.That(meal.IsWorking, Is.False);
            Assert.That(meal.MealsEaten, Is.EqualTo(1));
            Assert.That(log, Is.EqualTo(new[] { "started Warmed", "eaten", "finished" }));
        }

        [Test]
        public void OversizedAndRepeatedFramesNeverRepeatTheCommand()
        {
            meal.TryInteract();
            meal.Advance(1000d);
            meal.Advance(1000d);
            meal.Advance(0d);
            Assert.That(ration.Calls, Is.EqualTo(1));
            Finish();
            for (int i = 0; i < 20; i++) meal.Advance(5d);
            Assert.That(ration.Calls, Is.EqualTo(1), "a finished meal ignores replayed frames");
        }

        [Test]
        public void RepeatedEIsRefusedAndChangesNothing()
        {
            meal.TryInteract();
            meal.Advance(1d);
            double elapsed = meal.ElapsedActiveSeconds;
            CommandResult again = meal.TryInteract();
            Assert.That(again.Succeeded, Is.False);
            Assert.That(again.Explanation, Is.EqualTo(MealSequence.AlreadyEatingMessage));
            Assert.That(meal.ElapsedActiveSeconds, Is.EqualTo(elapsed));
            Assert.That(ration.Calls, Is.Zero);
        }

        [Test]
        public void ReentrantHandlersCannotIssueASecondCommand()
        {
            // Duplicate stage/animation notifications re-enter the sequence from inside the command and its events.
            int stageEvents = 0;
            meal.StageChanged += stage => { stageEvents++; meal.Advance(0d); meal.Advance(3d); };
            meal.Eaten += result => { meal.Advance(0d); meal.Advance(50d); meal.Advance(50d); };
            ration.DuringCall = () => { meal.Advance(10d); meal.Advance(10d); };
            meal.TryInteract();
            AdvanceTo(meal.Timeline.CommitAt + .01d);
            Finish();
            Assert.That(ration.Calls, Is.EqualTo(1));
            Assert.That(meal.MealsEaten, Is.EqualTo(1));
            Assert.That(stageEvents, Is.GreaterThan(1));
            Assert.That(meal.IsWorking, Is.False);
        }

        [Test]
        public void PauseFreezesTheMeal()
        {
            meal.TryInteract();
            meal.Advance(.5d);
            paused = true;
            for (int i = 0; i < 100; i++) meal.Advance(1d);
            Assert.That(meal.ElapsedActiveSeconds, Is.EqualTo(.5d).Within(1e-9));
            Assert.That(ration.Calls, Is.Zero);
            paused = false;
            Finish();
            Assert.That(ration.Calls, Is.EqualTo(1));
        }

        [Test]
        public void InvalidPolicyBeforeTheFirstBiteCancelsWithNothingSpent()
        {
            meal.TryInteract();
            AdvanceTo(meal.Timeline.Take + .2d);
            valid = false;
            meal.Advance(.02d);
            Assert.That(meal.IsWorking, Is.False);
            Assert.That(ration.Calls, Is.Zero);
            Assert.That(log[log.Count - 1], Does.StartWith("cancelled False"));
            valid = true;
            Finish();
            Assert.That(ration.Calls, Is.Zero, "a cancelled meal cannot resume and eat");
        }

        [Test]
        public void StaleSessionCancelsEvenWhilePaused()
        {
            meal.TryInteract();
            paused = true;
            valid = false;
            meal.Advance(0d);
            Assert.That(meal.IsWorking, Is.False);
            Assert.That(ration.Calls, Is.Zero);
        }

        [Test]
        public void InterruptionAfterTheFirstBiteKeepsTheOneRationAndAddsNothing()
        {
            meal.TryInteract();
            AdvanceTo(meal.Timeline.CommitAt + .3d);
            Assert.That(ration.Calls, Is.EqualTo(1));
            meal.Cancel("Alarm");
            Assert.That(log[log.Count - 1], Is.EqualTo("cancelled True: Alarm"));
            Finish();
            Assert.That(ration.Calls, Is.EqualTo(1));
            Assert.That(meal.MealsEaten, Is.EqualTo(1));
        }

        [Test]
        public void CoreUnavailableAtTheFirstBiteSpendsNothing()
        {
            meal.TryInteract();
            AdvanceTo(meal.Timeline.CommitAt - .05d);
            ration.Available = false;
            meal.Advance(.1d);
            Assert.That(ration.Calls, Is.Zero, "the fresh check refuses before the command");
            Assert.That(meal.IsWorking, Is.False);
            Assert.That(log[log.Count - 1], Is.EqualTo("cancelled False: No ration is needed yet."));
        }

        [Test]
        public void CoreRefusalAtTheFirstBiteEndsTheMealWithoutAnAward()
        {
            ration.Succeeds = false;
            meal.TryInteract();
            Finish();
            Assert.That(ration.Calls, Is.EqualTo(1));
            Assert.That(meal.MealsEaten, Is.Zero);
            Assert.That(log, Has.No.Member("eaten"));
            Assert.That(log[log.Count - 1], Is.EqualTo("cancelled False: Not enough food stored."));
        }

        [Test]
        public void UnavailableRationOrBlockerNeverStarts()
        {
            ration.Available = false;
            CommandResult refused = meal.TryInteract();
            Assert.That(refused.Succeeded, Is.False);
            Assert.That(refused.Explanation, Is.EqualTo("No ration is needed yet."));
            ration.Available = true;
            blocker = "Too far";
            Assert.That(meal.CanInteract, Is.False);
            Assert.That(meal.Prompt, Is.EqualTo("Too far"));
            Assert.That(meal.TryInteract().Explanation, Is.EqualTo("Too far"));
            Assert.That(meal.IsWorking, Is.False);
            Assert.That(ration.Calls, Is.Zero);
        }

        [Test]
        public void StartedHandlerCancellationIsReportedHonestly()
        {
            meal.Started += kind => meal.Cancel("view lost");
            CommandResult result = meal.TryInteract();
            Assert.That(result.Succeeded, Is.False);
            Finish();
            Assert.That(ration.Calls, Is.Zero);
        }

        [Test]
        public void PreparationRotatesAndTheRepeatBeatComesLater()
        {
            var kinds = new List<MealPreparation>();
            var checks = new List<bool>();
            for (int i = 0; i < 5; i++)
            {
                meal.TryInteract();
                kinds.Add(meal.Preparation);
                checks.Add(meal.IsLabelCheck);
                Finish();
            }
            Assert.That(kinds, Is.EqualTo(new[] { MealPreparation.Warmed, MealPreparation.Rehydrated, MealPreparation.ReadyToEat,
                MealPreparation.Warmed, MealPreparation.Rehydrated }));
            Assert.That(checks, Is.EqualTo(new[] { false, false, false, true, true }));
            Assert.That(ration.Calls, Is.EqualTo(5), "one command per meal");
        }

        [Test]
        public void TimelineValuesAreFiniteAndOrdered()
        {
            foreach (MealPreparation kind in Enum.GetValues(typeof(MealPreparation)))
            {
                MealTimeline t = MealTimeline.For(kind);
                Assert.That(t.CommitAt, Is.GreaterThan(t.Take));
                Assert.That(t.Total, Is.GreaterThan(t.CommitAt));
                Assert.That(t.StageAt(0d), Is.EqualTo(MealStage.Take));
                Assert.That(t.StageAt(t.CommitAt), Is.EqualTo(MealStage.Eat));
                Assert.That(t.StageAt(t.Total), Is.EqualTo(MealStage.Stow));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => new MealTimeline(0d, 1d, 1d, 1d, 1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => meal.Advance(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => meal.Advance(-1d));
        }

        [Test]
        public void ChoreographyTakesOpensEatsAndStows()
        {
            var stations = new MealStations
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
            foreach (MealPreparation kind in Enum.GetValues(typeof(MealPreparation)))
            {
                var path = new MealChoreography(stations, kind, false);
                MealPouchFrame first = path.Sample(MealStage.Take, 0d);
                Assert.That(first.Visible && !first.Opened && first.Attach == 0f, Is.True, kind + " starts in the drawer");
                Assert.That(Vector3.Distance(first.Position, stations.DrawerPosition), Is.LessThan(.06f));
                Assert.That(path.Sample(MealStage.Open, .3d).Opened, Is.False);
                Assert.That(path.Sample(MealStage.Open, .9d).Opened, Is.True, "the cut happens while opening");
                MealPouchFrame bite = path.Sample(MealStage.Eat, .35d);
                Assert.That(bite.Opened, Is.True);
                Assert.That(Vector3.Distance(bite.Position, stations.EyePosition), Is.LessThan(.25f), "eaten at the face");
                MealPouchFrame gone = path.Sample(MealStage.Stow, 1d);
                Assert.That(gone.Visible, Is.False);
                Assert.That(gone.HandWeight, Is.EqualTo(0f).Within(1e-4f), "the arm is given back");
                if (kind == MealPreparation.Rehydrated)
                {
                    Assert.That(path.Sample(MealStage.Prepare, 0d).Thickness, Is.LessThan(.8f), "dry before water");
                    Assert.That(path.Sample(MealStage.Prepare, 1d).Thickness, Is.EqualTo(1f).Within(1e-4f), "full after water");
                    Assert.That(Vector3.Distance(path.Station.Value.position, stations.Nozzle.Value), Is.LessThan(.12f), "at the needle");
                }
                if (kind == MealPreparation.Warmed)
                    Assert.That(Vector3.Distance(path.Station.Value.position, stations.Warmer.Value), Is.LessThan(.05f), "at the warmer");
            }
            Vector3 clamped = MealChoreography.ClampReach(new Vector3(0f, 0f, 2f), Vector3.zero, .6f);
            Assert.That(clamped.magnitude, Is.EqualTo(.6f).Within(1e-5f));
        }

        [Test]
        public void DeliveredClaspDataIsTheMeasuredMealPose()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_OutOfReach/Visuals/Props/GalleyConsumablesV01/WORK_Clasp_Meal_R.json");
            Assert.That(asset, Is.Not.Null);
            MealClaspData data = MealClaspData.Parse(asset.text);
            Assert.That(data, Is.Not.Null);
            Assert.That(data.pose, Is.EqualTo("WORK_Clasp_Meal_R"));
            Assert.That(data.bones.Length, Is.EqualTo(7));
            Assert.That(data.bones[0].name, Is.EqualTo("finger1-1.R"), "parent before child");
            Assert.That(new Vector3(data.pouchOrigin[0], data.pouchOrigin[1], data.pouchOrigin[2]).magnitude, Is.InRange(.10f, .20f));
            var pouch = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_OutOfReach/Visuals/Props/GalleyConsumablesV01/GC_MealPouch.fbx");
            Assert.That(pouch, Is.Not.Null);
            var names = new List<string>();
            foreach (Renderer r in pouch.GetComponentsInChildren<Renderer>(true)) names.Add(r.name);
            Assert.That(names, Does.Contain("GC_MealPouch_Sealed").And.Contain("GC_MealPouch_Opened"));
            Bounds sealedBounds = default;
            foreach (MeshFilter f in pouch.GetComponentsInChildren<MeshFilter>(true))
                if (f.name == "GC_MealPouch_Sealed") sealedBounds = f.sharedMesh.bounds;
            // GALLEY-PROPS-01 manifest: 0.141 x 0.178 x 0.029 m, y up the pouch (bake axis conversion on).
            Assert.That(sealedBounds.size.x, Is.EqualTo(.141f).Within(.003f));
            Assert.That(sealedBounds.size.y, Is.EqualTo(.178f).Within(.003f));
            Assert.That(sealedBounds.size.z, Is.EqualTo(.029f).Within(.003f));
        }
    }
}
