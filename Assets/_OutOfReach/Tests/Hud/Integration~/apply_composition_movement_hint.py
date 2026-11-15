"""MOVEMENT-HINT-01 composition wiring for the assembler (I01MissionComposition.cs, in place, exact anchors):
the flight board owns the once-per-session timed exit; the first hand push completes it; reduced motion reaches the board.
Run: python -I apply_composition_movement_hint.py Assets/_OutOfReach/Scripts/Composition/Runtime/I01MissionComposition.cs
"""
import sys

path = sys.argv[1]
t = open(path, encoding="utf-8").read()
pairs = [
    ("""            flightBoard.SetMicrogravityPrompt(!microgravityTutorialCompleted, railHintVisible);""",
     """            // MOVEMENT-HINT-01: the board shows the movement hint once per session and owns its timed exit.
            flightBoard.SetMicrogravityPrompt(true, railHintVisible);"""),
    ("""            microgravityTutorialCompleted = true;
            if (flightBoard) flightBoard.SetMicrogravityPrompt(false, railHintVisible);""",
     """            if (!microgravityTutorialCompleted && flightBoard) flightBoard.CompleteMicrogravityTutorial();
            microgravityTutorialCompleted = true;"""),
    ("""            flightBoard.SetDeparturePresentation(false);
            flightBoard.SetPhysicalTerminalPresentation(false);
            flightBoard.ConfigureTiming(runtime.Clock, runtime.PlanningDeadline);""",
     """            flightBoard.SetDeparturePresentation(false);
            flightBoard.SetPhysicalTerminalPresentation(false);
            flightBoard.SetReducedMotion(cinematics && cinematics.ReducedEffects);
            flightBoard.ConfigureTiming(runtime.Clock, runtime.PlanningDeadline);"""),
    ("""            if (cargoHands) cargoHands.SetReducedMotion(reduced);
        }""",
     """            if (cargoHands) cargoHands.SetReducedMotion(reduced);
            if (flightBoard) flightBoard.SetReducedMotion(reduced);
        }"""),
]
for a, b in pairs:
    assert t.count(a) == 1, a[:90]
    t = t.replace(a, b)
open(path, "w", encoding="utf-8", newline="\n").write(t)
print("composition hint wiring ok")
