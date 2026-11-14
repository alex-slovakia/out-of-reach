using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace OutOfReach.Presentation.Editor
{
    /// <summary>Checks committed audio bytes and Unity import. Does not prove playback, triggering or listening quality.</summary>
    public static class AudioProductionChecks
    {
        private const string AudioRoot = "Assets/_OutOfReach/Audio/";

        [Serializable] private sealed class Manifest { public Entry[] files; }
        [Serializable] private sealed class Entry
        {
            public string path;
            public string sha256;
            public int frames;
            public int sample_rate_hz;
            public int channels;
        }

        [MenuItem("OUT OF REACH/Audio/Configure and validate production clips")]
        public static void ConfigureAndValidate()
        {
            if (Application.unityVersion != "6000.3.28f1") throw new InvalidOperationException("Use Unity 6000.3.28f1.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioRoot.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = false; // Source already mono; preserve its original waveform.
                importer.loadInBackground = false;
                importer.SaveAndReimport();
            }
            Validate();
        }

        [MenuItem("OUT OF REACH/Audio/Validate production clips")]
        public static void Validate()
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(AudioRoot + "Production/audio-manifest.json"));
            if (manifest?.files == null || manifest.files.Length != 31) throw new InvalidDataException("Expected 31 production clips.");
            float greatestPeak = 0;
            foreach (Entry entry in manifest.files)
            {
                string path = AudioRoot + entry.path;
                if (entry.path.Contains("..") || Path.IsPathRooted(entry.path)) throw new InvalidDataException("Invalid manifest path.");
                using (var sha = SHA256.Create())
                {
                    string actual = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
                    if (actual != entry.sha256) throw new InvalidDataException("Audio hash mismatch: " + path);
                }
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (!clip || clip.channels != 1 || clip.frequency != 24000 || clip.samples != entry.frames)
                    throw new InvalidDataException("Audio import/format mismatch: " + path);
                if (entry.channels != clip.channels || entry.sample_rate_hz != clip.frequency)
                    throw new InvalidDataException("Manifest format mismatch: " + path);
                clip.LoadAudioData();
                var samples = new float[clip.samples * clip.channels];
                if (!clip.GetData(samples, 0)) throw new InvalidDataException("Cannot decode Unity clip: " + path);
                foreach (float sample in samples)
                {
                    if (float.IsNaN(sample) || float.IsInfinity(sample) || Mathf.Abs(sample) > 0.842f)
                        throw new InvalidDataException("Nonfinite or excessive sample peak: " + path);
                    greatestPeak = Mathf.Max(greatestPeak, Mathf.Abs(sample));
                }
                if (!File.Exists(Path.ChangeExtension(path, ".txt"))) throw new InvalidDataException("Missing English subtitle/caption: " + path);
            }
            Debug.Log("AUDIO_PRODUCTION_PASS clips=31 nativeRate=24000 channels=1 sha256=matched decodedSamples=finite maxSamplePeak=" + greatestPeak.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
            Debug.Log("AUDIO_PRODUCTION_LIMITS: imported clips only; no event wiring, playback mix, subjective listening or end-to-end gameplay verified.");
        }
    }
}
