using UnityEngine;

namespace GunnerVR
{
    /// <summary>
    /// Builds the handful of clips this prototype needs at runtime, so the project
    /// needs no audio files on disk. Swap these for real recordings when art lands:
    /// every caller just takes an <see cref="AudioClip"/>.
    /// </summary>
    public static class ProceduralAudio
    {
        const int k_SampleRate = 44100;

        /// <summary>A sharp noise crack with a fast decay. Reads as a shot.</summary>
        public static AudioClip CreateShot()
        {
            return CreateNoise("GunnerShot", 0.18f, decay: 28f, lowPass: 0.55f, startGain: 0.9f);
        }

        /// <summary>A short dry tick for pulling the trigger on an empty magazine.</summary>
        public static AudioClip CreateDryFire()
        {
            return CreateNoise("GunnerDryFire", 0.05f, decay: 90f, lowPass: 0.25f, startGain: 0.35f);
        }

        /// <summary>A duller, longer thunk for seating a fresh magazine.</summary>
        public static AudioClip CreateReload()
        {
            return CreateNoise("GunnerReload", 0.22f, decay: 16f, lowPass: 0.12f, startGain: 0.6f);
        }

        /// <summary>A bright blip for a scoring hit on paper.</summary>
        public static AudioClip CreateHit()
        {
            return CreateTone("GunnerHit", 0.12f, frequency: 880f, decay: 26f, gain: 0.45f);
        }

        /// <summary>A rising two-note chime for finishing the build.</summary>
        public static AudioClip CreateUnlock()
        {
            var length = 0.5f;
            var count = Mathf.CeilToInt(k_SampleRate * length);
            var data = new float[count];

            for (var i = 0; i < count; i++)
            {
                var t = i / (float)k_SampleRate;
                // Two overlapping notes, the second entering halfway through.
                var a = Mathf.Sin(2f * Mathf.PI * 587f * t) * Mathf.Exp(-5f * t);
                var b = t < 0.25f ? 0f : Mathf.Sin(2f * Mathf.PI * 880f * (t - 0.25f)) * Mathf.Exp(-6f * (t - 0.25f));
                data[i] = (a + b) * 0.35f;
            }

            return FromSamples("GunnerUnlock", data);
        }

        static AudioClip CreateNoise(string name, float length, float decay, float lowPass, float startGain)
        {
            var count = Mathf.CeilToInt(k_SampleRate * length);
            var data = new float[count];
            var random = new System.Random(name.GetHashCode());
            var previous = 0f;

            for (var i = 0; i < count; i++)
            {
                var t = i / (float)k_SampleRate;
                var white = (float)(random.NextDouble() * 2.0 - 1.0);

                // One-pole low pass: the smaller the coefficient, the heavier the thud.
                previous = Mathf.Lerp(previous, white, lowPass);
                data[i] = previous * Mathf.Exp(-decay * t) * startGain;
            }

            return FromSamples(name, data);
        }

        static AudioClip CreateTone(string name, float length, float frequency, float decay, float gain)
        {
            var count = Mathf.CeilToInt(k_SampleRate * length);
            var data = new float[count];

            for (var i = 0; i < count; i++)
            {
                var t = i / (float)k_SampleRate;
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * Mathf.Exp(-decay * t) * gain;
            }

            return FromSamples(name, data);
        }

        static AudioClip FromSamples(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, k_SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
