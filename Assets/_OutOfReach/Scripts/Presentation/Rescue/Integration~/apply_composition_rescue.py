"""RESCUE-ENDING-01 (R42): apply the composition hooks by anchors, idempotently.

Usage: python -I apply_composition_rescue.py <repo root>
Touches only Assets/_OutOfReach/Scripts/Composition/Runtime/I01MissionComposition.cs and the Composition asmdef.
Every anchor must match exactly once, otherwise nothing is written.
"""
import io
import json
import os
import sys

root = sys.argv[1] if len(sys.argv) > 1 else "."
comp = os.path.join(root, "Assets/_OutOfReach/Scripts/Composition/Runtime/I01MissionComposition.cs")
asmdef = os.path.join(root, "Assets/_OutOfReach/Scripts/Composition/Runtime/OutOfReach.Composition.asmdef")

src = io.open(comp, encoding="utf-8-sig").read()
newline = "\r\n" if "\r\n" in src else "\n"
s = src.replace("\r\n", "\n")

if "RescueEndingPresenter rescueEnding" in s:
    print("composition already patched")
else:
    edits = [
        ("using OutOfReach.Presentation.Meal;\n",
         "using OutOfReach.Presentation.Meal;\nusing OutOfReach.Presentation.Rescue;\n"),
        ("        [SerializeField] private MissionCinematicPresenter cinematics;\n",
         "        [SerializeField] private MissionCinematicPresenter cinematics;\n"
         "        [SerializeField] private RescueEndingPresenter rescueEnding;\n"),
        ("            cinematics.CaptionChanged += OnCinematicCaption;\n",
         "            cinematics.CaptionChanged += OnCinematicCaption;\n"
         "            if (rescueEnding) rescueEnding.PresentationChanged += OnRescuePresentation;\n"),
        ("            if (cinematics) cinematics.CaptionChanged -= OnCinematicCaption;\n",
         "            if (cinematics) cinematics.CaptionChanged -= OnCinematicCaption;\n"
         "            if (rescueEnding) rescueEnding.PresentationChanged -= OnRescuePresentation;\n"),
        ("            cinematics.Configure(boundEvents, runtime.GameState, runtime.Clock);\n",
         "            // R42: the rescue ending answers RecoveryArrival; the existing presenter sees every other request.\n"
         "            if (rescueEnding) rescueEnding.Configure(boundEvents, runtime.GameState, runtime.Clock);\n"
         "            cinematics.Configure(rescueEnding ? rescueEnding.FilterFor(boundEvents) : boundEvents, runtime.GameState, runtime.Clock);\n"),
        ("            if (cinematics) cinematics.enabled = false;\n",
         "            if (rescueEnding) rescueEnding.Release();\n"
         "            if (cinematics) cinematics.enabled = false;\n"),
        ("            if (cinematics) cinematics.ReducedEffects = reduced;\n",
         "            if (cinematics) cinematics.ReducedEffects = reduced;\n"
         "            if (rescueEnding) rescueEnding.ReducedEffects = reduced;\n"),
        ("flightBoard.SetDeparturePresentation(IsTransportDeparturePlaying || (cinematics && cinematics.IsPlaying) || EarthViewerOwnsInput);\n",
         "flightBoard.SetDeparturePresentation(IsTransportDeparturePlaying || (cinematics && cinematics.IsPlaying)\n"
         "                    || (rescueEnding && rescueEnding.IsPlaying) || EarthViewerOwnsInput);\n"),
        ("            if (!flightBoard) return;\n            if (audioPresenter && audioPresenter.CurrentSubtitle.IsVisible)\n",
         "            if (!flightBoard) return;\n"
         "            if (rescueEnding && rescueEnding.IsPlaying && !string.IsNullOrEmpty(rescueEnding.CurrentCaption))\n"
         "                flightBoard.ShowSubtitle(rescueEnding.CurrentCaption, rescueEnding.CurrentSpeaker);\n"
         "            else if (audioPresenter && audioPresenter.CurrentSubtitle.IsVisible)\n"),
        ("        private void OnCinematicCaption(string caption)\n",
         "        private void OnRescuePresentation()\n"
         "        {\n"
         "            RefreshCinematicHud();\n"
         "            RefreshSubtitle();\n"
         "        }\n\n"
         "        private void OnCinematicCaption(string caption)\n"),
    ]
    for old, new in edits:
        count = s.count(old)
        if count != 1:
            sys.exit("anchor matched %d times, nothing written: %r" % (count, old[:90]))
    for old, new in edits:
        s = s.replace(old, new)
    io.open(comp, "w", encoding="utf-8", newline="").write(s.replace("\n", newline))
    print("composition patched (%d hunks)" % len(edits))

a = json.load(io.open(asmdef, encoding="utf-8-sig"))
if "OutOfReach.Presentation.Rescue" not in a["references"]:
    raw = io.open(asmdef, encoding="utf-8-sig").read()
    anchor = '    "OutOfReach.Presentation.Meal",\n'
    nl = "\r\n" if "\r\n" in raw else "\n"
    text = raw.replace("\r\n", "\n")
    if text.count(anchor) != 1:
        sys.exit("asmdef anchor not found")
    text = text.replace(anchor, anchor + '    "OutOfReach.Presentation.Rescue",\n')
    io.open(asmdef, "w", encoding="utf-8", newline="").write(text.replace("\n", nl))
    print("asmdef reference added")
else:
    print("asmdef already references OutOfReach.Presentation.Rescue")
