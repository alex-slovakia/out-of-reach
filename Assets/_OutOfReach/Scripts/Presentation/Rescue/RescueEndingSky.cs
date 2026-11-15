using UnityEngine;

namespace OutOfReach.Presentation.Rescue
{
    /// <summary>
    /// Backlog R20 on the R42 ending: shows the EXISTING NASA star field during the rescue ending.
    ///
    /// Bootstrap's saved space skybox is NasaDeepSky.mat (NASA SVS Deep Star Maps 2020, starmap_2020_4k.exr) at the
    /// bright-ship exposure 0.15, where the stars are effectively invisible (CELESTIAL_FRAME.md section 6). While the
    /// ending holds, a runtime copy of that same material is shown at the existing deep-view exposure 0.45
    /// (NasaDeepSky_DeepView.mat, I06NasaDeepSkyAuthoring.DeepViewExposure). Texture, mapping, tint and rotation are
    /// the saved ones: only the artistic exposure changes, so there is no new art and no new claim about how the sky
    /// is oriented. No asset, scene, light or ambient setting is modified.
    ///
    /// Fails open: with any other skybox, or none, nothing is touched and the ending looks as it did before.
    /// </summary>
    public sealed class RescueEndingSky
    {
        /// <summary>Asset name of the committed NASA SVS star map; the only sky this class will brighten.</summary>
        public const string NasaStarMapName = "starmap_2020_4k";
        /// <summary>The existing deep-view preset (NasaDeepSky_DeepView.mat).</summary>
        public const float DeepViewExposure = .45f;
        /// <summary>Playhead seconds over which the exposure eases up (skipped with reduced effects).</summary>
        public const float RampSeconds = 1.5f;

        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        private Material saved, deepView;
        private float savedExposure;

        /// <summary>True while this class's copy is the active skybox on its behalf.</summary>
        public bool IsRaised { get; private set; }
        /// <summary>Exposure currently shown by the copy (0 when not raised).</summary>
        public float Exposure => IsRaised && deepView ? deepView.GetFloat(ExposureId) : 0f;
        /// <summary>The skybox that was active before the copy went up (null when not raised).</summary>
        public Material SavedSky => IsRaised ? saved : null;

        public static bool IsNasaDeepSky(Material sky)
        {
            if (!sky || !sky.HasProperty(ExposureId) || !sky.HasProperty(MainTexId)) return false;
            Texture map = sky.GetTexture(MainTexId);
            return map && map.name == NasaStarMapName;
        }

        /// <summary>Eased 0..1 exposure blend for a playhead time; instant with reduced effects.</summary>
        public static float Blend(float playheadSeconds, bool reducedEffects)
        {
            if (reducedEffects || playheadSeconds >= RampSeconds) return 1f;
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(playheadSeconds / RampSeconds));
        }

        /// <summary>Puts the copy up at the saved exposure (blend 0). False, and no change, for any other sky.</summary>
        public bool Raise()
        {
            Material sky = RenderSettings.skybox;
            if (IsRaised && deepView && sky == deepView) return true;
            IsRaised = false;
            if (deepView && sky == deepView) sky = saved;   // our copy was put back by someone else: start from the real one
            if (!IsNasaDeepSky(sky)) return false;
            float exposure = sky.GetFloat(ExposureId);
            if (exposure >= DeepViewExposure - 1e-4f) return false;   // already at least as deep: leave it
            if (!deepView) deepView = new Material(sky) { hideFlags = HideFlags.HideAndDontSave };
            else { deepView.shader = sky.shader; deepView.CopyPropertiesFromMaterial(sky); }
            deepView.name = sky.name + " (rescue ending deep view)";
            deepView.SetFloat(ExposureId, exposure);
            saved = sky;
            savedExposure = exposure;
            RenderSettings.skybox = deepView;
            IsRaised = true;
            return true;
        }

        /// <summary>0 = the saved exposure, 1 = the deep view. Does nothing unless raised.</summary>
        public void SetBlend(float blend)
        {
            if (!IsRaised || !deepView) return;
            deepView.SetFloat(ExposureId, Mathf.Lerp(savedExposure, DeepViewExposure, Mathf.Clamp01(blend)));
        }

        /// <summary>Puts the saved skybox back. A skybox someone else assigned meanwhile is left alone.</summary>
        public void Lower()
        {
            if (deepView && RenderSettings.skybox == deepView) RenderSettings.skybox = saved;
            IsRaised = false;
        }

        /// <summary>Per-frame guard outside the sequence: lowers once the ending no longer holds, and takes the copy
        /// back down if another system re-applied it after it was lowered.</summary>
        public void Keep(bool endingHolds)
        {
            if (IsRaised) { if (!endingHolds || !deepView || RenderSettings.skybox != deepView) Lower(); }
            else if (deepView && RenderSettings.skybox == deepView) RenderSettings.skybox = saved;
        }

        /// <summary>Lowers and destroys the copy (owner teardown).</summary>
        public void Dispose()
        {
            Lower();
            if (deepView)
            {
                if (Application.isPlaying) Object.Destroy(deepView); else Object.DestroyImmediate(deepView);
            }
            deepView = null;
            saved = null;
        }
    }
}
