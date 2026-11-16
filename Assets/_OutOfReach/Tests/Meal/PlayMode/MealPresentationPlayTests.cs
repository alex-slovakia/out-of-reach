using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Interaction;
using OutOfReach.Presentation.Meal;
using OutOfReach.Tests.PlayMode;
using UnityEngine;
using UnityEngine.TestTools;

namespace OutOfReach.Tests.Meal.PlayMode
{
    /// <summary>
    /// MEAL-PRES-01 through the real MissionRuntime and the real survival-ration target of the grey-box harness: E on the
    /// target plays the meal and Core debits exactly one ration at the first bite. EXPLICIT SHORTCUTS, and only these:
    /// the route to contact (RepairLoopHarness.ReachContact, covered by its own tests), one simulated wait until a ration
    /// is due, a scripted presentation clock (MealPresenter.Advance) instead of wall-clock frames, and a primitive pouch
    /// with the two variant names (the imported FBX is checked in EditMode). No food or need is ever written directly.
    /// </summary>
    public sealed class MealPresentationPlayTests
    {
        private RepairLoopHarness harness;
        private MealPresenter meal;
        private GameObject pouch;
        private readonly List<string> events = new List<string>();

        private MissionSession Session => harness.Session;
        private SurvivalExtension Survival => Session.Survival;
        private double Food => Session.Resources.Snapshot.FoodKg;

        [SetUp]
        public void SetUp()
        {
            Time.captureDeltaTime = .02f;
            harness = RepairLoopHarness.Build(survival: true);
            harness.Runtime.DriveFromUpdate = false;
            events.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (meal) UnityEngine.Object.DestroyImmediate(meal.gameObject);
            if (pouch) UnityEngine.Object.DestroyImmediate(pouch);
            harness.Destroy();
            Time.captureDeltaTime = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }

        /// <summary>Contact, a due ration, the player 0.8 m in front of the ration target, and the meal bound around it.</summary>
        private IEnumerator Prepare()
        {
            yield return Frames(2);
            harness.ReachContact();
            Assert.That(Session.State.Phase, Is.EqualTo(GamePhase.Survival));
            CommandResult waited = Session.AdvanceSimulation(Survival.Spec.PortionSeconds + 600d);
            Assert.That(waited.Succeeded, Is.True, "a ration comes due: " + waited.Explanation);
            Assert.That(Survival.CheckConsumeRation().Succeeded, Is.True, Survival.CheckConsumeRation().Explanation);

            pouch = new GameObject("PrimitivePouch");
            CreateVariant("GC_MealPouch_Sealed", pouch.transform);
            CreateVariant("GC_MealPouch_Opened", pouch.transform);
            pouch.SetActive(false);

            InteractableTarget target = harness.Ration;
            var water = new GameObject("WaterInteractionSurface").transform;
            water.SetParent(harness.Root.transform, false);
            water.SetPositionAndRotation(target.transform.position + new Vector3(0f, -.6f, .8f), target.transform.rotation);
            var host = new GameObject("Meal presentation (test)");
            meal = host.AddComponent<MealPresenter>();
            meal.ConfigureViews(target, target.transform, water, pouch, null);
            meal.DriveFromUpdate = false;
            Assert.That(target.Interactable, Is.Not.Null.And.Not.InstanceOf<MealSequence>());
            Assert.That(meal.ConfigureSession(target.Interactable, harness.Runtime.Clock, harness.Runtime.GameState,
                harness.Runtime.EvaStatus, harness.Runtime.Events, harness.Player.ViewPivot, null, () => true), Is.True);
            Assert.That(target.Interactable, Is.SameAs(meal.Sequence), "the real target now starts a meal");
            meal.Started += kind => events.Add("started " + kind);
            meal.Eaten += result => events.Add("eaten");
            meal.Finished += result => events.Add("finished");
            meal.Cancelled += (reason, eaten) => events.Add("cancelled " + eaten + ": " + reason);

            Vector3 at = target.transform.position;
            harness.PlacePlayerFacing(at.x + .8f, at.z, at);
            yield return Frames(3);
        }

        private static void CreateVariant(string name, Transform parent)
        {
            GameObject variant = GameObject.CreatePrimitive(PrimitiveType.Cube);
            variant.name = name;
            UnityEngine.Object.DestroyImmediate(variant.GetComponent<Collider>());
            variant.transform.SetParent(parent, false);
            variant.transform.localScale = new Vector3(.141f, .178f, .029f);
        }

        /// <summary>Presses E through the real input, resolver and target.</summary>
        private IEnumerator PressE(bool expectStart)
        {
            harness.Resolver.Refresh();
            Assert.That(harness.Resolver.CurrentTarget, Is.SameAs(harness.Ration), "aimed at the ration drawer");
            harness.Input.PressInteract();
            yield return Frames(3);
            CommandResult? last = harness.Player.LastInteraction;
            Assert.That(last.HasValue, Is.True);
            Assert.That(last.Value.Succeeded, Is.EqualTo(expectStart), last.Value.Explanation);
        }

        /// <summary>Advances the scripted presentation clock in 0.05 s steps, one shown frame per step.</summary>
        private IEnumerator StepTo(double seconds)
        {
            while (meal.IsEating && meal.Sequence.ElapsedActiveSeconds < seconds - 1e-9)
            {
                meal.Advance(Math.Min(.05d, seconds - meal.Sequence.ElapsedActiveSeconds));
                yield return null;
            }
            yield return null;
        }

        private IEnumerator AdvanceTo(double seconds)
        {
            int frames = 0;
            while (meal.IsEating && meal.Sequence.ElapsedActiveSeconds < seconds - 1e-9)
            {
                meal.Advance(Math.Min(.05d, seconds - meal.Sequence.ElapsedActiveSeconds));
                if (++frames % 5 == 0) yield return null;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator EOnTheRationTargetPlaysTheMealAndCoreDebitsExactlyOneRation()
        {
            yield return Prepare();
            double food = Food, hunger = Survival.Hunger01, ration = Survival.RationFoodKg;
            yield return PressE(true);
            Assert.That(meal.IsEating && meal.BlocksMovement, Is.True);
            Assert.That(meal.Stage, Is.EqualTo(MealStage.Take));
            Assert.That(events, Is.EqualTo(new[] { "started Warmed" }));
            Assert.That(Food, Is.EqualTo(food).Within(1e-12), "E spends nothing");
            yield return AdvanceTo(.2d);
            Assert.That(meal.PouchVisible, Is.True, "the pouch appears at the drawer");
            Assert.That(Vector3.Distance(meal.Pouch.position, harness.Ration.transform.position), Is.LessThan(.12f));

            yield return AdvanceTo(meal.Sequence.Timeline.CommitAt - .05d);
            Assert.That(meal.Stage, Is.EqualTo(MealStage.Open));
            Assert.That(meal.PouchOpened, Is.True, "opened before the first bite");
            Assert.That(Food, Is.EqualTo(food).Within(1e-12), "taking, warming and opening grant nothing");

            yield return AdvanceTo(meal.Sequence.Timeline.CommitAt + .05d);
            Assert.That(meal.Stage, Is.EqualTo(MealStage.Eat));
            Assert.That(Food, Is.EqualTo(food - ration).Within(1e-9), "one stored ration is debited");
            Assert.That(Survival.Hunger01, Is.LessThan(hunger), "fed status increases");
            Assert.That(meal.Sequence.CommandsIssued, Is.EqualTo(1));

            yield return AdvanceTo(meal.Sequence.Timeline.Total + 1d);
            Assert.That(meal.IsEating || meal.BlocksMovement || meal.PouchVisible, Is.False, "the meal ends cleanly");
            for (int i = 0; i < 20; i++) meal.Advance(10d);
            yield return Frames(2);
            Assert.That(Food, Is.EqualTo(food - ration).Within(1e-9), "replayed frames add nothing");
            Assert.That(meal.Sequence.CommandsIssued, Is.EqualTo(1));
            Assert.That(events, Is.EqualTo(new[] { "started Warmed", "eaten", "finished" }));
            Assert.That(harness.Ration.Interactable, Is.SameAs(meal.Sequence), "ready for the next meal");
        }

        [UnityTest]
        public IEnumerator RepeatedEDuringTheMealIsRefusedAndNeverRepeatsTheRation()
        {
            yield return Prepare();
            double food = Food, ration = Survival.RationFoodKg;
            yield return PressE(true);
            yield return AdvanceTo(1d);
            // The player has no control during the meal in the game; here the target is pressed directly as a duplicate.
            CommandResult again = harness.Ration.Interactable.TryInteract();
            Assert.That(again.Succeeded, Is.False);
            Assert.That(again.Explanation, Is.EqualTo(MealSequence.AlreadyEatingMessage));
            meal.Advance(1000d);
            meal.Advance(1000d);
            yield return AdvanceTo(meal.Sequence.Timeline.Total + 1d);
            Assert.That(Food, Is.EqualTo(food - ration).Within(1e-9));
            Assert.That(meal.Sequence.CommandsIssued, Is.EqualTo(1));
            Assert.That(Survival.CheckConsumeRation().Succeeded, Is.False, "the need is met: no second ration is due");
        }

        [UnityTest]
        public IEnumerator PauseFreezesTheMealAndResumeFinishesIt()
        {
            yield return Prepare();
            double food = Food, ration = Survival.RationFoodKg;
            yield return PressE(true);
            yield return AdvanceTo(2d);
            harness.Runtime.SetPaused(true);
            for (int i = 0; i < 50; i++) meal.Advance(.1d);
            yield return Frames(3);
            Assert.That(meal.Sequence.ElapsedActiveSeconds, Is.EqualTo(2d).Within(1e-6), "paused: frozen");
            Assert.That(meal.IsEating && meal.PouchVisible, Is.True, "the pose stays behind the pause menu");
            Assert.That(Food, Is.EqualTo(food).Within(1e-12));
            harness.Runtime.SetPaused(false);
            yield return AdvanceTo(meal.Sequence.Timeline.Total + 1d);
            Assert.That(Food, Is.EqualTo(food - ration).Within(1e-9));
            Assert.That(meal.IsEating, Is.False);
        }

        [UnityTest]
        public IEnumerator RestartBeforeTheFirstBiteSpendsNothingAndRestoresTheTarget()
        {
            yield return Prepare();
            MissionSession before = Session;
            double food = before.Resources.Snapshot.FoodKg;
            yield return PressE(true);
            yield return AdvanceTo(3d);
            Assert.That(meal.Stage, Is.EqualTo(MealStage.Prepare));
            harness.Runtime.RestartMission();
            meal.Advance(.05d); // the old session's target binding is gone: the meal ends at once
            yield return Frames(2);
            Assert.That(meal.IsEating || meal.PouchVisible || meal.BlocksMovement, Is.False);
            Assert.That(before.Resources.Snapshot.FoodKg, Is.EqualTo(food).Within(1e-12), "the interrupted meal ate nothing");
            Assert.That(meal.Sequence.CommandsIssued, Is.Zero);
            Assert.That(events[events.Count - 1], Does.StartWith("cancelled False"));
            meal.ClearBinding(); // what the composition does when the session ends
            Assert.That(meal.IsBound, Is.False);
        }

        [UnityTest]
        public IEnumerator HazardBeforeTheFirstBiteInterruptsWithNothingSpent()
        {
            yield return Prepare();
            double food = Food;
            Session.Events.Publish(new SystemConditionChanged(ShipSystemId.Power, 1d));
            yield return PressE(true);
            yield return AdvanceTo(2.5d);
            Session.Events.Publish(new SystemConditionChanged(ShipSystemId.Power, .35d));
            yield return Frames(2);
            Assert.That(meal.IsEating || meal.PouchVisible, Is.False);
            Assert.That(Food, Is.EqualTo(food).Within(1e-12));
            Assert.That(events[events.Count - 1], Does.StartWith("cancelled False: " + MealPresenter.HazardMessage));
            yield return PressE(true);
            yield return AdvanceTo(meal.Sequence.Timeline.Total + 1d);
            Assert.That(Food, Is.EqualTo(food - Survival.RationFoodKg).Within(1e-9), "a later meal still eats exactly one ration");
        }

        [UnityTest]
        public IEnumerator MissionEndAfterTheFirstBiteKeepsOneRationAndHidesThePouch()
        {
            yield return Prepare();
            double food = Food, ration = Survival.RationFoodKg;
            yield return PressE(true);
            yield return AdvanceTo(meal.Sequence.Timeline.CommitAt + .4d);
            Assert.That(Food, Is.EqualTo(food - ration).Within(1e-9));
            Session.Events.Publish(new MissionEnded(MissionOutcome.Lost, "test end", Session.Resources.Snapshot));
            yield return Frames(2);
            Assert.That(meal.IsEating || meal.PouchVisible || meal.BlocksMovement, Is.False);
            Assert.That(events[events.Count - 1], Does.StartWith("cancelled True"));
            Assert.That(Food, Is.EqualTo(food - ration).Within(1e-9), "no refund and no second ration");
        }

        [UnityTest]
        public IEnumerator TheMealBeatsPlayInTheNewOrderAndTheTearComesBeforeTheFirstBite()
        {
            yield return Prepare();
            var beats = new List<string>();
            meal.BeatPlayed += (mark, sounded) => beats.Add(mark.ToString());
            double food = Food, ration = Survival.RationFoodKg;
            yield return PressE(true);
            MealTimeline line = meal.Sequence.Timeline;

            // R35: grasp, pinch and tear, all before the first bite; the strip is off and nothing is spent.
            yield return StepTo(line.CommitAt - .02d);
            Assert.That(beats, Is.EqualTo(new[] { "Grasp", "Pinch", "Tear" }), "the pouch is grasped, pinched and torn open before the first bite");
            Assert.That(meal.Stage, Is.EqualTo(MealStage.Open));
            Assert.That(meal.TearProgress, Is.EqualTo(1f).Within(1e-4f), "the strip is off");
            Assert.That(meal.PouchOpened, Is.True);
            Assert.That(meal.StripVisible, Is.False, "no visible left hand in this harness: the torn strip never floats");
            Assert.That(Food, Is.EqualTo(food).Within(1e-12), "tearing the pouch open spends nothing");
            Assert.That(meal.Sequence.CommandsIssued, Is.Zero);

            // The first bite is the one real command; the first sip is heard only after it.
            yield return StepTo(line.CommitAt + .3d);
            Assert.That(meal.Sequence.CommandsIssued, Is.EqualTo(1));
            Assert.That(Food, Is.EqualTo(food - ration).Within(1e-9));
            Assert.That(beats.Count, Is.EqualTo(3), "nothing is drunk before the pouch is at the mouth");

            // Three squeezed sips with their swallows; the pouch flattens; then it is stowed.
            yield return StepTo(line.CommitAt + MealChoreography.SipProgress(1, .5f) * line.Eat);
            Assert.That(meal.Squeeze, Is.GreaterThan(.9f), "the second sip squeezes the pouch");
            Assert.That(meal.Pouch.localScale.z, Is.LessThan(.9f), "the pouch flattens as it is eaten");
            yield return StepTo(line.Total + 1d);
            Assert.That(beats, Is.EqualTo(new[] { "Grasp", "Pinch", "Tear", "Sip 1", "Swallow 1", "Sip 2", "Swallow 2", "Sip 3", "Swallow 3", "Stow" }));
            Assert.That(meal.Sequence.CommandsIssued, Is.EqualTo(1), "the beats never repeat the ration");
            Assert.That(Food, Is.EqualTo(food - ration).Within(1e-9));
            Assert.That(meal.IsEating || meal.PouchVisible || meal.StripVisible, Is.False, "the meal ends cleanly");
            Assert.That(meal.BeatsPlayed, Is.EqualTo(10));
        }

        [UnityTest]
        public IEnumerator TheHeadNodsWhileSippingAndReducedMotionKeepsTheViewStill()
        {
            yield return Prepare();
            // The meal nods only a camera's own transform (never the player's look pivot): give it one below the pivot.
            IInteractable real = meal.Sequence.Inner;
            var eye = new GameObject("Meal test camera");
            eye.transform.SetParent(harness.Player.ViewPivot, false);
            eye.AddComponent<Camera>().enabled = false;
            Assert.That(meal.ConfigureSession(real, harness.Runtime.Clock, harness.Runtime.GameState,
                harness.Runtime.EvaStatus, harness.Runtime.Events, eye.transform, null, () => true), Is.True);
            Quaternion rest = eye.transform.localRotation;
            Quaternion pivot = harness.Player.ViewPivot.localRotation;
            bool reduced = false;
            meal.ReducedMotionQuery = () => reduced;
            double food = Food, ration = Survival.RationFoodKg;
            yield return PressE(true);
            MealTimeline line = meal.Sequence.Timeline;

            yield return StepTo(line.CommitAt - .1d);
            Assert.That(Quaternion.Angle(rest, eye.transform.localRotation), Is.LessThan(1e-3f), "the view is still while the pouch is taken and opened");

            // At the first squeeze the head has come down to the pouch.
            yield return StepTo(line.CommitAt + MealChoreography.SipProgress(0, MealChoreography.SipSqueezeFrom) * line.Eat);
            Assert.That(meal.NodDegrees, Is.EqualTo(MealChoreography.NodDown).Within(.3f));
            Assert.That(Quaternion.Angle(rest, eye.transform.localRotation), Is.EqualTo(MealChoreography.NodDown).Within(.3f), "a small nod");
            Assert.That(Quaternion.Angle(pivot, harness.Player.ViewPivot.localRotation), Is.LessThan(1e-3f), "the player's own look pivot is never touched");

            // Reduced motion: the view goes back to rest at once and stays there, while the meal goes on.
            reduced = true;
            yield return Frames(2);
            Assert.That(meal.NodDegrees, Is.Zero);
            Assert.That(Quaternion.Angle(rest, eye.transform.localRotation), Is.LessThan(1e-3f), "reduced motion: no nod");
            yield return StepTo(line.CommitAt + MealChoreography.SipProgress(1, .5f) * line.Eat);
            Assert.That(meal.Squeeze, Is.GreaterThan(.9f), "the sip itself still plays");
            Assert.That(Quaternion.Angle(rest, eye.transform.localRotation), Is.LessThan(1e-3f), "reduced motion: the view stays still");

            // With motion allowed again the nod returns, and the rest pose is given back exactly when the meal ends.
            reduced = false;
            yield return StepTo(line.CommitAt + MealChoreography.SipProgress(2, MealChoreography.SipSqueezeFrom) * line.Eat);
            Assert.That(Quaternion.Angle(rest, eye.transform.localRotation), Is.GreaterThan(1f));
            yield return StepTo(line.Total + 1d);
            Assert.That(meal.IsEating, Is.False);
            Assert.That(eye.transform.localRotation == rest, Is.True, "the view is given back");
            Assert.That(Quaternion.Angle(rest, eye.transform.localRotation), Is.LessThan(1e-4f));
            Assert.That(Food, Is.EqualTo(food - ration).Within(1e-9), "the nod changes nothing about the one ration");
        }

        [UnityTest]
        public IEnumerator TooFarFromTheDrawerRefusesWithoutStarting()
        {
            yield return Prepare();
            Vector3 at = harness.Ration.transform.position;
            harness.PlacePlayerFacing(at.x + 1.7f, at.z, at);
            yield return Frames(3);
            harness.Resolver.Refresh();
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo(MealPresenter.MoveCloserMessage));
            yield return PressE(false);
            Assert.That(meal.IsEating, Is.False);
            Assert.That(meal.Sequence.CommandsIssued, Is.Zero);
        }
    }
}
