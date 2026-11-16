using OutOfReach.Presentation.Audio;
using UnityEngine;

namespace OutOfReach.Presentation.Meal
{
    /// <summary>
    /// The meal's contact sounds (R35): foil in the hand, the tear, the squeeze and the swallow. Original synthesised
    /// clips (<c>Audio/Original/MealFoley</c>, generator and provenance beside them), loaded by name so the saved scene
    /// needs no new reference, and played through the mission's own <see cref="MissionAudioPresenter"/>: its SFX level,
    /// its pause and its mute apply. Without that presenter (tests, bare scenes) nothing sounds.
    /// </summary>
    public sealed class MealFoley
    {
        public const string ResourceFolder = "MealFoley";
        public const string GrabClip = ResourceFolder + "/meal-pouch-grab";
        public const string TearClip = ResourceFolder + "/meal-pouch-tear";
        public const string SqueezeClip = ResourceFolder + "/meal-pouch-squeeze";
        public const string SwallowClip = ResourceFolder + "/meal-swallow";

        private AudioClip grab, tear, squeeze, swallow;
        private bool loaded;
        private MissionAudioPresenter output;

        /// <summary>The clip of a beat, or null when the beat is silent.</summary>
        public static string ClipOf(MealBeat beat)
        {
            switch (beat)
            {
                case MealBeat.Grasp:
                case MealBeat.Stow: return GrabClip;
                case MealBeat.Knead:
                case MealBeat.Sip: return SqueezeClip;
                case MealBeat.Tear: return TearClip;
                case MealBeat.Swallow: return SwallowClip;
                default: return null;
            }
        }

        /// <summary>Level of a beat (0..1 of the SFX level): the tear leads, the swallow stays under it.</summary>
        public static float GainOf(MealBeatMark mark)
        {
            switch (mark.Beat)
            {
                case MealBeat.Grasp: return .75f;
                case MealBeat.Knead: return .70f;
                case MealBeat.Tear: return 1f;
                case MealBeat.Sip: return .85f - .05f * mark.Index;
                case MealBeat.Swallow: return .50f;
                case MealBeat.Stow: return .55f;
                default: return 0f;
            }
        }

        /// <summary>Plays the sound of a beat. False when the beat is silent or nothing could play.</summary>
        public bool Play(MealBeatMark mark)
        {
            AudioClip clip = Clip(mark.Beat);
            if (!clip) return false;
            if (!output) output = Object.FindAnyObjectByType<MissionAudioPresenter>();
            return output && output.PlaySfx(clip, GainOf(mark));
        }

        private AudioClip Clip(MealBeat beat)
        {
            if (!loaded)
            {
                grab = Resources.Load<AudioClip>(GrabClip);
                tear = Resources.Load<AudioClip>(TearClip);
                squeeze = Resources.Load<AudioClip>(SqueezeClip);
                swallow = Resources.Load<AudioClip>(SwallowClip);
                loaded = true;
            }
            switch (beat)
            {
                case MealBeat.Grasp:
                case MealBeat.Stow: return grab;
                case MealBeat.Knead:
                case MealBeat.Sip: return squeeze;
                case MealBeat.Tear: return tear;
                case MealBeat.Swallow: return swallow;
                default: return null;
            }
        }
    }
}
