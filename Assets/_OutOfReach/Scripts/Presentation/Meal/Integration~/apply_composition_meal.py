"""MEAL-PRES-01 composition wiring for the integration assembler: edits I01MissionComposition.cs (+ the Composition asmdef).

Run from the repo root (in place):
  python -I apply_composition_meal.py Assets/_OutOfReach/Scripts/Composition/Runtime/I01MissionComposition.cs Assets/_OutOfReach/Scripts/Composition/Runtime/I01MissionComposition.cs Assets/_OutOfReach/Scripts/Composition/Runtime/OutOfReach.Composition.asmdef Assets/_OutOfReach/Scripts/Composition/Runtime/OutOfReach.Composition.asmdef
Same result as composition-meal.patch; use it when the patch context has drifted.
Each anchor must occur exactly once; otherwise the tool stops (no fuzzy edits).
"""
import sys

src, out = sys.argv[1], sys.argv[2]
text = open(src, encoding="utf-8").read()


def once(old, new):
    global text
    count = text.count(old)
    if count != 1:
        sys.exit("anchor found %d times: %r" % (count, old[:90]))
    text = text.replace(old, new)


once("using OutOfReach.Presentation.Hands;\n",
     "using OutOfReach.Presentation.Hands;\nusing OutOfReach.Presentation.Meal;\n")
once("        [SerializeField] private I06HabitatBindings habitat;\n",
     "        [SerializeField] private I06HabitatBindings habitat;\n"
     "        // MEAL-PRES-01: the physical galley meal around the existing survival-ration target (presentation only).\n"
     "        [SerializeField] private MealPresenter meal;\n")
once("        public void ConfigureHabitat(I06HabitatBindings bindings) => habitat = bindings;\n",
     "        public void ConfigureHabitat(I06HabitatBindings bindings) => habitat = bindings;\n"
     "        public void ConfigureMeal(MealPresenter presenter)\n"
     "        {\n"
     "            if (Application.isPlaying && attached) throw new InvalidOperationException(\"Configure the meal before binding.\");\n"
     "            meal = presenter;\n"
     "        }\n"
     "        public MealPresenter Meal => meal;\n"
     "\n"
     "        // Same valid context while paused; never Player.ControlAllowed (the meal owns its movement lock).\n"
     "        private bool CanPresentMeal() => !EarthViewerOwnsInput && isActiveAndEnabled && attached && boundSession != null\n"
     "            && runtime && runtime.isActiveAndEnabled && ReferenceEquals(boundSession, runtime.Session)\n"
     "            && cinematics && !cinematics.IsPlaying && !IsTransportDeparturePlaying\n"
     "            && !(boundSession.SurvivalRecovery?.IsResting ?? false)\n"
     "            && !(antennaRepair && antennaRepair.IsWorking);\n"
     "\n"
     "        private void OnMealStarted(MealPreparation preparation)\n"
     "        {\n"
     "            // Let go of any rail first, as for the Earth viewer: the hand that eats must be free.\n"
     "            if (microgravityBridge) microgravityBridge.Release();\n"
     "            if (embodiedBody) embodiedBody.RefreshPresentation();\n"
     "            OnMealStatus(meal ? meal.StatusLine : string.Empty);\n"
     "        }\n"
     "\n"
     "        private void OnMealStatus(string line)\n"
     "        {\n"
     "            if (!flightBoard || string.IsNullOrWhiteSpace(line)) return;\n"
     "            flightBoard.ShowInteractionPrompt(line, true);\n"
     "            rejectionUntil = 0f;\n"
     "        }\n"
     "\n"
     "        private void OnMealFinished(CommandResult result)\n"
     "        {\n"
     "            if (!flightBoard) return;\n"
     "            flightBoard.ShowInteractionPrompt(string.IsNullOrWhiteSpace(result.Explanation) ? \"Meal finished.\" : result.Explanation, true);\n"
     "            rejectionUntil = Time.unscaledTime + 3f;\n"
     "        }\n"
     "\n"
     "        private void OnMealCancelled(string reason, bool eaten)\n"
     "        {\n"
     "            if (!flightBoard || string.IsNullOrWhiteSpace(reason)) return;\n"
     "            flightBoard.ShowInteractionPrompt(reason, false);\n"
     "            rejectionUntil = Time.unscaledTime + 4f;\n"
     "        }\n")
# Movement lock: the meal owns the player's control while it runs (like the antenna work).
once("            && !(antennaRepair && antennaRepair.BlocksMovement)\n            && domainControlGate != null && domainControlGate()\n",
     "            && !(antennaRepair && antennaRepair.BlocksMovement)\n            && !(meal && meal.BlocksMovement)\n"
     "            && domainControlGate != null && domainControlGate()\n")
# No opening-menu sway and no Earth viewer during a meal (the player has no control then).
once("            && !(antennaRepair && antennaRepair.IsWorking)\n            && runtime && runtime.isActiveAndEnabled && ReferenceEquals(boundSession, runtime.Session)\n"
     "            && runtime.Player && runtime.Player.isActiveAndEnabled && !runtime.Player.ControlAllowed\n",
     "            && !(antennaRepair && antennaRepair.IsWorking)\n            && !(meal && meal.IsEating)\n"
     "            && runtime && runtime.isActiveAndEnabled && ReferenceEquals(boundSession, runtime.Session)\n"
     "            && runtime.Player && runtime.Player.isActiveAndEnabled && !runtime.Player.ControlAllowed\n")
once("            && !IsTransportDeparturePlaying && !(antennaRepair && antennaRepair.BlocksMovement);\n        private void PrepareEarthViewer()\n",
     "            && !IsTransportDeparturePlaying && !(antennaRepair && antennaRepair.BlocksMovement)\n"
     "            && !(meal && meal.IsEating);\n        private void PrepareEarthViewer()\n")
# Bind after the habitat (which binds the raw ration action) and after the body (whose WORK arm holds the pouch).
once("            if (antennaRepair) antennaRepair.WorkCancelled += OnAntennaWorkCancelled;\n",
     "            if (antennaRepair) antennaRepair.WorkCancelled += OnAntennaWorkCancelled;\n"
     "            if (meal && habitat && runtime.TryGetInteractable(InteractionIds.ConsumeRation, out IInteractable rationForMeal)\n"
     "                && meal.ConfigureSession(rationForMeal, runtime.Clock, runtime.GameState, runtime.EvaStatus, boundEvents,\n"
     "                    presentationCamera ? presentationCamera.transform : runtime.Player.ViewPivot, embodiedBody, CanPresentMeal))\n"
     "            {\n"
     "                meal.Started += OnMealStarted;\n"
     "                meal.StatusChanged += OnMealStatus;\n"
     "                meal.Finished += OnMealFinished;\n"
     "                meal.Cancelled += OnMealCancelled;\n"
     "            }\n")
once("            if (habitat) habitat.ClearBinding();\n",
     "            if (meal)\n"
     "            {\n"
     "                meal.Started -= OnMealStarted;\n"
     "                meal.StatusChanged -= OnMealStatus;\n"
     "                meal.Finished -= OnMealFinished;\n"
     "                meal.Cancelled -= OnMealCancelled;\n"
     "                meal.ClearBinding();\n"
     "            }\n"
     "            if (habitat) habitat.ClearBinding();\n")
open(out, "w", encoding="utf-8", newline="\n").write(text)

if len(sys.argv) > 4:
    asm = open(sys.argv[3], encoding="utf-8").read()
    anchor = '    "OutOfReach.Presentation.Habitat",\n'
    if asm.count(anchor) != 1:
        sys.exit("asmdef anchor missing")
    asm = asm.replace(anchor, anchor + '    "OutOfReach.Presentation.Meal",\n')
    open(sys.argv[4], "w", encoding="utf-8", newline="\n").write(asm)
print("patched")
