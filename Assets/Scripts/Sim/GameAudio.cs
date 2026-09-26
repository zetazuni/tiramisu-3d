using System.Collections.Generic;
using UnityEngine;

namespace Tiramisu
{
    /// <summary>
    /// All the sound of the game, made in code (so nothing is copied from anywhere): soft clicks, a chime for good news, a low note for
    /// "no", a warm pad of music, birds by day, crickets at night and wind in winter. M mutes everything (music only with N).
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        public enum Sfx { Click, Chime, No, Buy, Sell, Alert, Place, Splash, Ding }

        public static GameAudio Instance { get; private set; }
        public static bool Muted { get; private set; }
        public static bool MusicOn { get; private set; } = true;

        const int Rate = 22050;
        readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        AudioSource sfx, music, birds, crickets, wind;
        AudioClip birdChirp;
        float nextChirp;

        void Awake()
        {
            Instance = this;
            if (PlayerPrefs.GetInt("tiramisu.muted", 0) == 1) Muted = true;
            if (PlayerPrefs.GetInt("tiramisu.music", 1) == 0) MusicOn = false;
            var go = gameObject;
            sfx = Source(go, false, 0.55f);
            music = Source(go, true, 0.16f);
            crickets = Source(go, true, 0f);
            wind = Source(go, true, 0f);
            birds = Source(go, false, 0.35f);
            Build();
        }

        static AudioSource Source(GameObject go, bool loop, float vol)
        {
            var s = go.AddComponent<AudioSource>();
            s.spatialBlend = 0f; s.loop = loop; s.volume = vol; s.playOnAwake = false;
            return s;
        }

        public static void Play(Sfx s)
        {
            if (Instance == null || Muted) return;
            if (Instance.clips.TryGetValue(s, out var c)) Instance.sfx.PlayOneShot(c);
        }

        public static void ToggleMute() { Muted = !Muted; PlayerPrefs.SetInt("tiramisu.muted", Muted ? 1 : 0); }
        public static void ToggleMusic() { MusicOn = !MusicOn; PlayerPrefs.SetInt("tiramisu.music", MusicOn ? 1 : 0); }

        // ------------------------------------------------------------ making the sounds

        static AudioClip Make(string name, float seconds, System.Func<float, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        static float Env(float t, float attack, float length) => Mathf.Clamp01(t / attack) * Mathf.Exp(-t * (4f / length));
        static float Sine(float hz, float t) => Mathf.Sin(2f * Mathf.PI * hz * t);

        void Build()
        {
            clips[Sfx.Click] = Make("click", 0.06f, t => Sine(1250f, t) * Env(t, 0.002f, 0.05f) * 0.5f);
            clips[Sfx.Ding] = Make("ding", 0.25f, t => (Sine(880f, t) + 0.4f * Sine(1760f, t)) * Env(t, 0.004f, 0.22f) * 0.4f);
            clips[Sfx.Chime] = Make("chime", 0.9f, t => (Sine(659.3f, t) * Env(t, 0.004f, 0.6f) + (t > 0.14f ? Sine(830.6f, t - 0.14f) * Env(t - 0.14f, 0.004f, 0.6f) : 0f) + (t > 0.28f ? Sine(987.8f, t - 0.28f) * Env(t - 0.28f, 0.004f, 0.6f) : 0f)) * 0.3f);
            clips[Sfx.No] = Make("no", 0.28f, t => (Sine(196f, t) + 0.5f * Sine(207f, t)) * Env(t, 0.005f, 0.24f) * 0.4f);
            clips[Sfx.Buy] = Make("buy", 0.5f, t => (Sine(1046f, t) * Env(t, 0.003f, 0.12f) + (t > 0.09f ? Sine(1568f, t - 0.09f) * Env(t - 0.09f, 0.003f, 0.3f) : 0f)) * 0.32f);
            clips[Sfx.Sell] = Make("sell", 0.4f, t => (Sine(1318f, t) * Env(t, 0.003f, 0.1f) + (t > 0.08f ? Sine(880f, t - 0.08f) * Env(t - 0.08f, 0.003f, 0.25f) : 0f)) * 0.3f);
            clips[Sfx.Alert] = Make("alert", 0.5f, t => (Sine(523f, t) * Env(t, 0.004f, 0.15f) + (t > 0.2f ? Sine(392f, t - 0.2f) * Env(t - 0.2f, 0.004f, 0.2f) : 0f)) * 0.3f);
            clips[Sfx.Place] = Make("place", 0.16f, t => Sine(150f + 60f * Mathf.Exp(-t * 30f), t) * Env(t, 0.002f, 0.12f) * 0.7f);
            var rnd = new System.Random(3);
            clips[Sfx.Splash] = Make("splash", 0.6f, t => ((float)rnd.NextDouble() * 2f - 1f) * Env(t, 0.02f, 0.5f) * 0.25f);

            // the music: a slow chord pad, C major seventh going to A minor, that loops
            music.clip = Make("pad", 16f, t =>
            {
                float[] a = { 130.8f, 164.8f, 196f, 246.9f }, b = { 110f, 130.8f, 164.8f, 196f };
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Repeat(t, 16f) - 6f) / 4f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Repeat(t, 16f) - 14f) / 2f)));
                float v = 0f;
                for (int i = 0; i < 4; i++) v += Mathf.Lerp(Sine(a[i], t), Sine(b[i], t), k) * (0.2f + 0.05f * Sine(0.17f + 0.03f * i, t));
                v += 0.05f * Sine(523.3f * (1f + 0.25f * k), t) * (0.5f + 0.5f * Sine(0.25f, t));
                return v * 0.4f;
            });
            music.clip.name = "pad";
            crickets.clip = Make("crickets", 3f, t => Mathf.Repeat(t * 6f, 1f) < 0.25f ? Sine(4300f, t) * 0.4f * Mathf.Sin(Mathf.PI * Mathf.Repeat(t * 6f, 1f) / 0.25f) : 0f);
            var r2 = new System.Random(8); float lp = 0f;
            wind.clip = Make("wind", 6f, t => { lp = lp * 0.985f + ((float)r2.NextDouble() * 2f - 1f) * 0.015f; return lp * 9f * (0.6f + 0.4f * Sine(0.2f, t)); });
            birdChirp = Make("bird", 0.35f, t => Sine(2600f + 1400f * Mathf.Sin(t * 30f) + 800f * t, t) * Env(t, 0.01f, 0.25f) * 0.35f);
            music.Play(); crickets.Play(); wind.Play();
        }

        // ------------------------------------------------------------ every frame

        void Update()
        {
            var dn = DayNightCycle.Instance;
            float night = dn ? dn.Night01 : 0f;
            var sea = SeasonCycle.Instance;
            float winter = sea && sea.season == Season.Winter ? 1f : 0f;
            float k = 1f - Mathf.Exp(-2f * Time.unscaledDeltaTime);
            float mute = Muted ? 0f : 1f;
            music.volume = Mathf.Lerp(music.volume, MusicOn ? 0.16f * mute : 0f, k);
            crickets.volume = Mathf.Lerp(crickets.volume, (sea && sea.season == Season.Winter ? 0.02f : 0.08f) * night * mute, k);
            wind.volume = Mathf.Lerp(wind.volume, (0.02f + 0.06f * winter) * mute, k);
            if (Time.time > nextChirp)
            {
                nextChirp = Time.time + Random.Range(2.5f, 9f);
                if (night < 0.4f && winter < 0.5f && !Muted) { birds.pitch = Random.Range(0.85f, 1.3f); birds.PlayOneShot(birdChirp, 0.5f); if (Random.value < 0.5f) StartCoroutine(Twice()); }
            }
            if (Input.GetKeyDown(KeyCode.N)) ToggleMusic();
            if (Input.GetKeyDown(KeyCode.F2)) ToggleMute();
        }

        System.Collections.IEnumerator Twice() { yield return new WaitForSeconds(0.28f); if (!Muted) birds.PlayOneShot(birdChirp, 0.4f); }
    }
}
