"""SLEEP-SEQ composition change for I01MissionComposition.cs (anchor-based, idempotent check).

Usage: python -I apply_sleepseq_composition.py <path to I01MissionComposition.cs>
Applies four edits by exact anchors; fails loudly if an anchor is missing. Run once.
"""
import io
import sys

path = sys.argv[1]
src = io.open(path, encoding="utf-8").read()
if "SLEEP-SEQ" in src:
    sys.exit("already applied (SLEEP-SEQ marker found)")


def replace_once(text, old, new, what):
    if text.count(old) != 1:
        sys.exit("anchor not found exactly once: " + what + " (" + str(text.count(old)) + ")")
    return text.replace(old, new, 1)


# 1. Gameplay input waits while the berth sequence owns the body (planning controls and micro contacts).
src = replace_once(src,
    "            && !(boundSession.SurvivalRecovery?.IsResting ?? false)\n            && !(chapterUi && chapterUi.BlocksPlayerControl)",
    "            && !(boundSession.SurvivalRecovery?.IsResting ?? false)\n            && !(embodiedBody && embodiedBody.RestBlocksControl)\n            && !(chapterUi && chapterUi.BlocksPlayerControl)",
    "CanUsePlanningControls")
src = replace_once(src,
    "            && !(boundSession.SurvivalRecovery?.IsResting ?? false)\n            && string.IsNullOrEmpty(runtime.Cargo?.CarriedEquipmentId)",
    "            && !(boundSession.SurvivalRecovery?.IsResting ?? false)\n            && !(embodiedBody && embodiedBody.RestBlocksControl)\n            && string.IsNullOrEmpty(runtime.Cargo?.CarriedEquipmentId)",
    "CanUseMicroContacts")

# 2. Eyelids wait while the body floats in and straps up (with or without the chapter UI).
src = replace_once(src,
    "                    survivalUi.ConfigureRestPresentationOwner(() => chapterUi && chapterUi.RoutineOwnsRestPresentation);",
    "                    survivalUi.ConfigureRestPresentationOwner(() => chapterUi && chapterUi.RoutineOwnsRestPresentation\n"
    "                        || RestBodyEntering());",
    "rest presentation owner (chapter UI)")
src = replace_once(src,
    "            flightBoard.ConfigureCo2Scale(SurvivalRules.CarbonDioxideActionLevelPpm, SurvivalRules.CarbonDioxideLossLimitPpm);",
    "            // SLEEP-SEQ: the eyelids wait until the body is in the berth and strapped in, so the approach is seen.\n"
    "            if (!chapterUi && survivalUi) survivalUi.ConfigureRestPresentationOwner(RestBodyEntering);\n"
    "            flightBoard.ConfigureCo2Scale(SurvivalRules.CarbonDioxideActionLevelPpm, SurvivalRules.CarbonDioxideLossLimitPpm);",
    "rest presentation owner (no chapter UI)")

# 2b. The chapter routine montage owns its nightly rests: no walk-in into the berth for those.
src = replace_once(src,
    "                    embodiedBody.ConfigureRest(() => recovery.IsResting, sleepBodyFrame, () => recovery.LastRestStop);",
    "                    embodiedBody.ConfigureRest(() => recovery.IsResting, sleepBodyFrame, () => recovery.LastRestStop,\n"
    "                        () => chapterUi && chapterUi.RoutineOwnsRestPresentation);",
    "ConfigureRest (routine owner)")

# 3. The lease follows the presenter's berth path instead of cutting to SleepCamera, and holds through Releasing.
old_wanted = (
    "            if (!(boundSession.SurvivalRecovery?.IsResting ?? false)) return false;\n"
    "            return embodiedBody.RestPhase != RestPhase.Releasing;\n"
    "        }\n")
new_wanted = (
    "            // SLEEP-SEQ: the body's berth sequence (approach, straps, hold, wake, unbuckle, float out) owns the view\n"
    "            // until it ends; Core's IsResting starts it and its wake ends it, the presenter only plays it out.\n"
    "            return embodiedBody.RestPhase != RestPhase.None;\n"
    "        }\n"
    "\n"
    "        private bool RestBodyEntering() => embodiedBody && embodiedBody.RestPhase == RestPhase.Entering;\n")
src = replace_once(src, old_wanted, new_wanted, "RestCameraWanted")

start = src.index("        private void UpdateRestCamera()\n")
end = src.index("        private void ReleaseRestCamera()\n")
new_update = '''        private void UpdateRestCamera()
        {
            if (!RestCameraWanted()) { ReleaseRestCamera(); return; }
            // Keep every offset below the pivot (departure, cinematic, motion). The live gameplay eye is where the
            // camera would be without the lease; the presenter turns it into this frame's view on the berth path
            // (SLEEP-SEQ), so the view glides from the eye into the berth and back out, never cut.
            Transform eye = RestEye().transform;
            Quaternion relative = Quaternion.Inverse(planningFramingPivot.rotation) * eye.rotation;
            Vector3 offset = planningFramingPivot.InverseTransformPoint(eye.position);
            Vector3 localPosition = restCameraLeased ? restLeaseLocalPosition : planningFramingPivot.localPosition;
            Quaternion localRotation = restCameraLeased ? restLeaseLocalRotation : planningFramingPivot.localRotation;
            Transform parent = planningFramingPivot.parent;
            Vector3 pivotPosition = parent ? parent.TransformPoint(localPosition) : localPosition;
            Quaternion pivotRotation = parent ? parent.rotation * localRotation : localRotation;
            if (!embodiedBody.TryGetRestView(pivotPosition + pivotRotation * offset, pivotRotation * relative,
                    out Vector3 viewPosition, out Quaternion view))
            { ReleaseRestCamera(); return; }
            if (!restCameraLeased)
            {
                restLeaseLocalPosition = planningFramingPivot.localPosition;
                restLeaseLocalRotation = planningFramingPivot.localRotation;
                restCameraLeased = true;
            }
            Quaternion rotation = view * Quaternion.Inverse(relative);
            planningFramingPivot.SetPositionAndRotation(viewPosition - rotation * offset, rotation);
        }

'''
old_block = src[start:end]
if "RestPitchDegrees" not in old_block:
    sys.exit("UpdateRestCamera/RestPitchDegrees block not as expected")
src = src[:start] + new_update + src[end:]
io.open(path, "w", encoding="utf-8", newline="").write(src)
print("SLEEP-SEQ composition change applied to", path)
