using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutOfReach.Player;
using OutOfReach.Presentation.UI;
using UnityEngine.InputSystem;

namespace OutOfReach.Tests.PauseControls
{
    /// <summary>R32 pause pictograms: the rows follow the real PlayerInputActions bindings and the movement mode.</summary>
    public sealed class PauseControlsModelTests
    {
        private PlayerInputActions input;

        [SetUp] public void SetUp() => input = new PlayerInputActions();
        [TearDown] public void TearDown() => input.Dispose();

        private static string[] Signatures(PauseControlRow row) =>
            row.Alternatives.Select(chord => string.Join("+", chord.Select(g => g.Signature))).ToArray();

        private static PauseControlRow Row(IReadOnlyList<PauseControlRow> rows, string startsWith) =>
            rows.FirstOrDefault(r => r.Label.StartsWith(startsWith));

        [Test]
        public void RealBindingsAreReadFromTheLiveMapNotTheFallback()
        {
            var bindings = PauseBindings.FromMap(input.Map);

            Assert.That(bindings.IsFallback, Is.False);
            Assert.That(bindings.Move.Count, Is.EqualTo(2), "WASD and the arrow keys");
            Assert.That(bindings.Move[0].Signature, Is.EqualTo("cluster(key:W,key:A,key:S,key:D)"));
            Assert.That(bindings.Sprint.Select(g => g.Signature), Is.EqualTo(new[] { "key:SHIFT" }), "left and right shift are one cap");
            Assert.That(bindings.Interact.Select(g => g.Signature), Is.EqualTo(new[] { "key:E", "mouse:L" }), "gamepad is not pictured");
            Assert.That(bindings.Pause.Select(g => g.Signature), Is.EqualTo(new[] { "key:ESC" }));
            Assert.That(bindings.MouseLooks, Is.True);
        }

        [Test]
        public void AnOverriddenBindingShowsWhatTheGameActuallyListensTo()
        {
            input.Map.FindAction("Pause").ApplyBindingOverride(0, "<Keyboard>/p");

            var bindings = PauseBindings.FromMap(input.Map);

            Assert.That(bindings.Pause.Select(g => g.Signature), Is.EqualTo(new[] { "key:P" }));
            Assert.That(Signatures(Row(PauseControlsModel.Build(PauseControlsMode.Walking, false, bindings), "Resume")),
                Is.EqualTo(new[] { "key:P" }));
        }

        [Test]
        public void ANullMapUsesTheMarkedFallback()
        {
            Assert.That(PauseBindings.FromMap(null).IsFallback, Is.True);
        }

        [Test]
        public void WalkingShowsMoveLookSprintInteractSystemsAndResume()
        {
            var rows = PauseControlsModel.Build(PauseControlsMode.Walking, false, PauseBindings.FromMap(input.Map));

            Assert.That(rows.Select(r => r.Label), Is.EqualTo(new[]
                { "Move", "Look", "Sprint · hold", "Interact", "Systems · hold", "Resume" }));
            Assert.That(Signatures(Row(rows, "Move")), Is.EqualTo(new[]
                { "cluster(key:W,key:A,key:S,key:D)", "cluster(arrow:up,arrow:left,arrow:down,arrow:right)" }));
            Assert.That(Signatures(Row(rows, "Interact")), Is.EqualTo(new[] { "key:E", "mouse:L" }));
            Assert.That(Signatures(Row(rows, "Resume")), Is.EqualTo(new[] { "key:ESC" }));
            Assert.That(Signatures(Row(rows, "Systems")), Is.EqualTo(new[] { "key:TAB" }));
        }

        [Test]
        public void TheTetherRouteMovesOnlyAlongItAndHasNoSprint()
        {
            var rows = PauseControlsModel.Build(PauseControlsMode.TetherRoute, true, PauseBindings.FromMap(input.Map));

            Assert.That(Row(rows, "Sprint"), Is.Null);
            Assert.That(Row(rows, "Grip"), Is.Null, "rail grips are a floating control");
            Assert.That(Signatures(Row(rows, "Move along the tether")), Is.EqualTo(new[]
                { "cluster(key:W,-,key:S,-)", "cluster(arrow:up,-,arrow:down,-)" }));
        }

        [Test]
        public void FloatingPromisesPushesButNoWasdThrustAndNoSprint()
        {
            var rows = PauseControlsModel.Build(PauseControlsMode.Floating, false, PauseBindings.FromMap(input.Map));

            Assert.That(Row(rows, "Move"), Is.Null, "no WASD thrust is offered");
            Assert.That(Row(rows, "Sprint"), Is.Null);
            Assert.That(Row(rows, "Grip"), Is.Null, "a bare-hand grip is not authored");
            Assert.That(Signatures(Row(rows, "Push off")), Is.EqualTo(new[] { "mouse:L", "mouse:R" }));
            Assert.That(Signatures(Row(rows, "Push with both")), Is.EqualTo(new[] { "mouse:LR" }));
            Assert.That(Signatures(Row(rows, "Lean")), Is.EqualTo(new[] { "key:A", "key:D" }));
            var push = Row(rows, "Hold W + Space");
            Assert.That(Signatures(push), Is.EqualTo(new[] { "key:W+key:SPACE" }));
            Assert.That(push.Note, Does.Contain("Legs on the deck"));
            Assert.That(Signatures(Row(rows, "Use equipment")), Is.EqualTo(new[] { "key:E" }), "mouse click is a push here");
        }

        [Test]
        public void OnlyASuitedFloatingPlayerSeesTheRailGrip()
        {
            var bindings = PauseBindings.FromMap(input.Map);

            var suited = PauseControlsModel.Build(PauseControlsMode.Floating, true, bindings);

            Assert.That(Signatures(Row(suited, "Grip a reachable rail")), Is.EqualTo(new[] { "mouse:L", "mouse:R" }));
            Assert.That(Row(PauseControlsModel.Build(PauseControlsMode.Floating, false, bindings), "Grip"), Is.Null);
        }

        [Test]
        public void ViewRebuildsOnlyWhenTheControlsChange()
        {
            var view = new PauseControlsView(null);
            var bindings = PauseBindings.FromMap(input.Map);

            view.SetControls(PauseControlsMode.Walking, false, bindings);
            var first = view.Rows;
            view.SetControls(PauseControlsMode.Walking, false, bindings);
            Assert.That(view.Rows, Is.SameAs(first));
            view.SetControls(PauseControlsMode.Floating, false, bindings);
            Assert.That(view.Rows, Is.Not.SameAs(first));
        }
    }
}
