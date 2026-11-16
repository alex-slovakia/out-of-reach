// R35 MEAL native QA, part for the reworked presenter only (copy it next to MealBeatsNativeQa.cs for an "after" run):
// logs every beat the presenter reports and whether its sound played.
using OutOfReach.Presentation.Meal;
using UnityEditor;

[InitializeOnLoad]
public static class MealBeatsNativeQaAfter
{
    static MealBeatsNativeQaAfter()
    {
        MealBeatsNativeQa.HookBeats = (meal, log) => meal.BeatPlayed += (mark, sounded) => log(mark + (sounded ? " (sound)" : " (silent)"));
    }
}
