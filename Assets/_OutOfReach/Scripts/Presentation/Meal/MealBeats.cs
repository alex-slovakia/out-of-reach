using System.Collections.Generic;

namespace OutOfReach.Presentation.Meal
{
    /// <summary>The audible and visible beats of one meal (R35), in the order they happen.</summary>
    public enum MealBeat
    {
        /// <summary>The right hand closes on the pouch in the drawer.</summary>
        Grasp,
        /// <summary>The pouch is squeezed or kneaded while it is prepared (ready to eat, rehydrated).</summary>
        Knead,
        /// <summary>The left thumb and index finger close on the top corner.</summary>
        Pinch,
        /// <summary>The top strip is torn off.</summary>
        Tear,
        /// <summary>The pouch is squeezed at the lips (three per meal).</summary>
        Sip,
        /// <summary>The swallow after a sip.</summary>
        Swallow,
        /// <summary>The emptied pouch goes into the wet-trash bag.</summary>
        Stow,
    }

    /// <summary>One beat at a progress of a meal stage. Presentation only: it never issues a command.</summary>
    public readonly struct MealBeatMark
    {
        public MealBeatMark(MealBeat beat, int index, MealStage stage, float progress)
        {
            Beat = beat; Index = index; Stage = stage; Progress = progress;
        }

        public MealBeat Beat { get; }
        /// <summary>0-based number of this beat among beats of its kind (the sip and the swallow count to three).</summary>
        public int Index { get; }
        public MealStage Stage { get; }
        /// <summary>0..1 within <see cref="Stage"/>.</summary>
        public float Progress { get; }

        /// <summary>Unpaused real seconds from the start of a meal with this pacing.</summary>
        public double SecondsIn(MealTimeline timeline) => timeline.StageStart(Stage) + Progress * timeline.StageLength(Stage);

        public override string ToString() => Beat + (Beat == MealBeat.Sip || Beat == MealBeat.Swallow ? " " + (Index + 1) : string.Empty);
    }

    /// <summary>
    /// The beat schedule of a meal, taken from the same constants as <see cref="MealChoreography"/> so the sound falls on
    /// the motion: grasp, (knead), pinch, tear, then three times sip and swallow, then stow. The tear ends before the
    /// first bite (<see cref="MealTimeline.CommitAt"/>) and every sip comes after it: nothing is drunk before Core has
    /// debited the ration.
    /// </summary>
    public static class MealBeats
    {
        public const float GraspAt = .36f;
        public const float ReadyToEatSqueezeAt = .42f;
        public const float RehydratedKneadAt = .80f;
        public const float StowAt = .70f;

        public static MealBeatMark[] For(MealPreparation kind, bool repeatedMenu)
        {
            var marks = new List<MealBeatMark> { new MealBeatMark(MealBeat.Grasp, 0, MealStage.Take, GraspAt) };
            if (kind == MealPreparation.ReadyToEat) marks.Add(new MealBeatMark(MealBeat.Knead, 0, MealStage.Prepare, ReadyToEatSqueezeAt));
            else if (kind == MealPreparation.Rehydrated) marks.Add(new MealBeatMark(MealBeat.Knead, 0, MealStage.Prepare, RehydratedKneadAt));
            marks.Add(new MealBeatMark(MealBeat.Pinch, 0, MealStage.Open, MealChoreography.OpenStageProgress(MealChoreography.PinchAt, repeatedMenu)));
            marks.Add(new MealBeatMark(MealBeat.Tear, 0, MealStage.Open, MealChoreography.OpenStageProgress(MealChoreography.TearStart, repeatedMenu)));
            for (int i = 0; i < MealChoreography.SipCount; i++)
            {
                marks.Add(new MealBeatMark(MealBeat.Sip, i, MealStage.Eat, MealChoreography.SipProgress(i, MealChoreography.SipSqueezeFrom)));
                marks.Add(new MealBeatMark(MealBeat.Swallow, i, MealStage.Eat, MealChoreography.SipProgress(i, MealChoreography.SipSwallowAt)));
            }
            marks.Add(new MealBeatMark(MealBeat.Stow, 0, MealStage.Stow, StowAt));
            return marks.ToArray();
        }
    }
}
