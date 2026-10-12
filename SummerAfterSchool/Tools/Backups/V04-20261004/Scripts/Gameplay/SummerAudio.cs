using System.Collections.Generic;
using UnityEngine;

namespace Childhood
{
    public enum AudioMood { Explore, Match, Collection, Result }
    public enum ImpactMaterial { Paper, Metal, Glass, Wood, Rubber }

    /// <summary>分层环境、独立声部、地点淡化与本机音量；轻碰保持轻，连击保持清楚。</summary>
    public sealed class SummerAudio : MonoBehaviour
    {
        private const int VoiceCount = 10;
        private string preference = "Summer.Audio.";
        private float preferenceSaveAt = -1;
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly HashSet<string> missingClips = new HashSet<string>();
        private readonly Dictionary<string, AudioClip[]> variants = new Dictionary<string, AudioClip[]>();
        private readonly Dictionary<string, int> previousVariant = new Dictionary<string, int>();
        private readonly Dictionary<string, float> nextAllowed = new Dictionary<string, float>();
        private readonly Queue<float> recentSounds = new Queue<float>();
        private readonly Queue<float> recentImpacts = new Queue<float>();
        private LoopPair music, environment;
        private AudioSource wind, charge;
        private Voice[] voices;
        private float chargeStrength, requestedCharge, lastChargePreview, muteGain = 1;
        private float musicMoodGain = 1, windContextGain = 1, duckGain = 1;
        private float actualMaster, actualMusic, actualEffects, actualAmbience;
        private bool ducked;
        private int pendingCollect, pendingCombo;
        private bool pendingRare;
        private float collectAt, comboAt, impactAt;
        private string pendingImpact;
        private float pendingImpactGain, pendingImpactPitch;
        private Vector3? pendingImpactPosition;

        public bool Muted { get; private set; }
        public float MasterVolume { get; private set; }
        public float MusicVolume { get; private set; }
        public float EffectsVolume { get; private set; }
        public float AmbienceVolume { get; private set; }
        public string LocationId { get; private set; } = "home";
        public AudioMood Mood { get; private set; } = AudioMood.Explore;

        private sealed class Voice
        {
            public AudioSource source;
            public float gain, started;
            public int priority;
        }

        private sealed class LoopPair
        {
            public readonly AudioSource[] sources = new AudioSource[2];
            public readonly float[] weights = new float[2];
            private readonly float[] from = new float[2];
            private readonly float[] to = new float[2];
            private float elapsed, duration;
            private int active;

            public void Set(AudioClip clip, float seconds)
            {
                if (sources[active].clip == clip && sources[active].isPlaying) return;
                int destination = sources[active].isPlaying ? 1 - active : active;
                for (int i = 0; i < 2; i++) { from[i] = weights[i]; to[i] = i == destination && clip != null ? 1 : 0; }
                sources[destination].Stop(); sources[destination].clip = clip;
                weights[destination] = from[destination] = 0;
                if (clip != null) { sources[destination].volume = 0; sources[destination].Play(); }
                active = destination; elapsed = 0; duration = Mathf.Max(.08f, seconds);
            }

            public void Tick(float dt, float gain)
            {
                elapsed = Mathf.Min(duration, elapsed + dt);
                float t = duration <= 0 ? 1 : elapsed / duration;
                float ease = t * t * (3 - 2 * t);
                for (int i = 0; i < 2; i++)
                {
                    weights[i] = Mathf.Sqrt(Mathf.Lerp(from[i] * from[i], to[i] * to[i], ease));
                    sources[i].volume = weights[i] * gain;
                    if (t >= 1 && to[i] == 0 && sources[i].isPlaying) sources[i].Stop();
                }
            }
        }

        private void Awake()
        {
            foreach (string argument in System.Environment.GetCommandLineArgs())
                if (argument == "--smoke-test") { preference = "Summer.Audio.Smoke."; break; }
            MasterVolume = ReadVolume("Master", .85f); MusicVolume = ReadVolume("Music", .52f);
            EffectsVolume = ReadVolume("Effects", .80f); AmbienceVolume = ReadVolume("Ambience", .32f);
            actualMaster = MasterVolume; actualMusic = MusicVolume; actualEffects = EffectsVolume; actualAmbience = AmbienceVolume;
            Muted = PlayerPrefs.GetInt(preference + "Muted", 0) != 0; muteGain = Muted ? 0 : 1;
            music = MakePair("音乐"); environment = MakePair("地点环境");
            wind = MakeSource("树叶与微风", true); wind.clip = Clip("ambience_wind"); if (wind.clip != null) wind.Play();
            charge = MakeSource("指尖蓄力", true); charge.clip = Clip("charge_loop");
            voices = new Voice[VoiceCount];
            for (int i = 0; i < voices.Length; i++) voices[i] = new Voice { source = MakeSource("音效声部_" + i, false) };
            SetContext("home", AudioMood.Explore);
        }

        private float ReadVolume(string key, float fallback) { return Mathf.Clamp01(PlayerPrefs.GetFloat(preference + key, fallback)); }
        private AudioSource MakeSource(string name, bool loop)
        {
            var child = new GameObject(name); child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = loop; source.volume = 0;
            source.spatialBlend = 0; source.dopplerLevel = 0; source.priority = loop ? 96 : 72;
            return source;
        }
        private LoopPair MakePair(string name)
        {
            var pair = new LoopPair(); pair.sources[0] = MakeSource(name + " A", true); pair.sources[1] = MakeSource(name + " B", true); return pair;
        }

        private AudioClip Clip(string key)
        {
            if (clips.TryGetValue(key, out var clip)) return clip;
            if (missingClips.Contains(key)) return null;
            clip = Resources.Load<AudioClip>("Audio/" + key);
            if (clip == null) missingClips.Add(key); else clips[key] = clip;
            return clip;
        }

        private AudioClip Variant(string key)
        {
            if (!variants.TryGetValue(key, out var choices))
            {
                var found = new List<AudioClip>();
                for (int i = 0; i < 4; i++) { var clip = Clip(key + "_" + i.ToString("00")); if (clip != null) found.Add(clip); }
                if (found.Count == 0) { var clip = Clip(key); if (clip != null) found.Add(clip); }
                choices = found.ToArray(); variants[key] = choices;
            }
            if (choices.Length == 0) return null;
            int index = Random.Range(0, choices.Length);
            if (choices.Length > 1 && previousVariant.TryGetValue(key, out var last) && index == last)
                index = (index + 1 + Random.Range(0, choices.Length - 1)) % choices.Length;
            previousVariant[key] = index;
            return choices[index];
        }

        public void SetContext(string locationId, AudioMood mood)
        {
            LocationId = string.IsNullOrEmpty(locationId) ? "home" : locationId;
            Mood = mood;
            string ambience = LocationId == "old_tree" ? "ambience_tree" : LocationId == "alley" ? "ambience_alley"
                : LocationId == "school_gate" ? "ambience_school" : "ambience_courtyard";
            environment.Set(Clip(ambience) ?? Clip("ambience"), 1.9f);
            string desired = mood == AudioMood.Collection ? "summer_collection" : mood == AudioMood.Match ? "summer_match" : "summer_loop";
            music.Set(Clip(desired) ?? Clip("summer_loop"), 2.2f);
            musicMoodGain = mood == AudioMood.Collection ? .78f : mood == AudioMood.Match ? .92f : mood == AudioMood.Result ? .88f : 1;
            windContextGain = LocationId == "old_tree" ? .8f : LocationId == "alley" ? .35f : .55f;
            PreviewCharge(0);
        }

        public void SetVolumes(float master, float music, float effects, float ambience)
        {
            MasterVolume = Mathf.Clamp01(master); MusicVolume = Mathf.Clamp01(music);
            EffectsVolume = Mathf.Clamp01(effects); AmbienceVolume = Mathf.Clamp01(ambience);
            PlayerPrefs.SetFloat(preference + "Master", MasterVolume); PlayerPrefs.SetFloat(preference + "Music", MusicVolume);
            PlayerPrefs.SetFloat(preference + "Effects", EffectsVolume); PlayerPrefs.SetFloat(preference + "Ambience", AmbienceVolume);
            preferenceSaveAt = Time.unscaledTime + .45f;
        }

        public void ToggleMute()
        {
            Muted = !Muted; PlayerPrefs.SetInt(preference + "Muted", Muted ? 1 : 0); preferenceSaveAt = Time.unscaledTime + .45f;
        }
        public void Duck(bool active) { ducked = active; if (!active) PreviewCharge(0); }

        public void PreviewCharge(float normalizedStrength)
        {
            requestedCharge = Mathf.Clamp01(normalizedStrength);
            if (requestedCharge < .025f) requestedCharge = 0;
            lastChargePreview = Time.unscaledTime;
        }

        public void PlayShot(ShotKind shot, float power)
        {
            PreviewCharge(0);
            Play(shot == ShotKind.Smash ? "smash" : shot == ShotKind.Skim ? "skim" : "slap",
                Mathf.Lerp(.55f, .94f, Mathf.Clamp01(power)), Mathf.Lerp(.94f, 1.04f, Mathf.Clamp01(power)));
        }

        public void PlayImpact(ImpactMaterial material, float speed, float volume = 1, Vector3? position = null)
        {
            if (speed < .35f) return;
            float energy = Mathf.Sqrt(Mathf.Clamp01(speed / 16));
            float gain = Mathf.Lerp(.055f, .43f, energy) * Mathf.Clamp(volume, 0, 1.4f);
            if (material == ImpactMaterial.Glass || material == ImpactMaterial.Metal) gain *= .80f;
            if (pendingImpact != null && gain <= pendingImpactGain) return;
            if (pendingImpact == null) impactAt = Time.unscaledTime + .008f;
            pendingImpact = "impact_" + material.ToString().ToLowerInvariant();
            pendingImpactGain = gain; pendingImpactPitch = Random.Range(.94f, 1.035f); pendingImpactPosition = position;
        }

        public void PlayCollect(int combo = 1, bool rare = false)
        {
            if (pendingCollect == 0) collectAt = Time.unscaledTime + .028f;
            pendingCollect = Mathf.Max(pendingCollect, Mathf.Max(1, combo)); pendingRare |= rare;
        }

        public void PlayCombo(int streak)
        {
            if (streak < 2) return;
            if (pendingCombo == 0) comboAt = Time.unscaledTime + .028f;
            pendingCombo = Mathf.Max(pendingCombo, Mathf.Clamp(streak, 2, 6));
        }

        /// <summary>旧玩法兼容入口；选择不同样本，微调音高，限制同类重复。</summary>
        public void Play(string key, float volume = 1, float pitch = 1)
        {
            bool shot = key == "slap" || key == "smash" || key == "skim";
            if (shot) PreviewCharge(0);
            int priority = key == "win" || key == "lose" ? 4 : shot || key == "flip" ? 3 : 2;
            float cooldown = key == "win" || key == "lose" ? .85f : key == "flip" ? .10f : .055f;
            Emit(key, Mathf.Clamp(volume, 0, 1.4f), pitch * Random.Range(.98f, 1.025f), priority, key, cooldown, null);
        }

        private void Emit(string key, float gain, float pitch, int priority, string group, float cooldown, Vector3? position)
        {
            if (gain <= 0 || Muted || EffectsVolume <= 0 || MasterVolume <= 0) return;
            float now = Time.unscaledTime;
            if (nextAllowed.TryGetValue(group, out var next) && now < next) return;
            while (recentSounds.Count > 0 && now - recentSounds.Peek() > 1) recentSounds.Dequeue();
            while (recentImpacts.Count > 0 && now - recentImpacts.Peek() > 1) recentImpacts.Dequeue();
            if (priority < 3 && (recentSounds.Count >= 22 || group == "impact" && recentImpacts.Count >= 10)) return;
            AudioClip clip = Variant(key); if (clip == null) return;
            Voice chosen = null; int active = 0;
            foreach (var voice in voices)
            {
                if (!voice.source.isPlaying) { if (chosen == null || chosen.source.isPlaying) chosen = voice; continue; }
                active++;
                if ((chosen == null || chosen.source.isPlaying) && voice.priority <= priority &&
                    (chosen == null || voice.priority < chosen.priority || voice.priority == chosen.priority && voice.started < chosen.started)) chosen = voice;
            }
            if (chosen == null) return;
            nextAllowed[group] = now + cooldown; recentSounds.Enqueue(now); if (group == "impact") recentImpacts.Enqueue(now);
            chosen.source.Stop(); chosen.source.clip = clip; chosen.source.pitch = Mathf.Clamp(pitch, .7f, 1.65f);
            chosen.source.spatialBlend = position.HasValue ? .16f : 0;
            chosen.source.minDistance = 5; chosen.source.maxDistance = 40; chosen.source.rolloffMode = AudioRolloffMode.Linear;
            if (position.HasValue) chosen.source.transform.position = position.Value;
            chosen.gain = gain * .74f / Mathf.Sqrt(1 + active * .22f); chosen.priority = priority; chosen.started = now;
            chosen.source.volume = chosen.gain * actualEffects * actualMaster * muteGain;
            chosen.source.Play();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime, now = Time.unscaledTime;
            if (pendingImpact != null && now >= impactAt)
            {
                Emit(pendingImpact, pendingImpactGain, pendingImpactPitch, 1, "impact", .046f, pendingImpactPosition); pendingImpact = null;
            }
            if (pendingCollect > 0 && now >= collectAt)
            {
                float pitch = Mathf.Pow(2, Mathf.Clamp(pendingCollect - 1, 0, 6) / 12f);
                Emit("collect", .65f, pitch, 3, "collect", .085f, null);
                if (pendingRare) Emit("rare_collect", .45f, 1, 4, "rare_collect", .35f, null);
                pendingCollect = 0; pendingRare = false;
            }
            if (pendingCombo > 0 && now >= comboAt)
            {
                Emit("combo_" + pendingCombo.ToString("00"), .47f, 1, 3, "combo", .23f, null); pendingCombo = 0;
            }
            muteGain = Mathf.MoveTowards(muteGain, Muted ? 0 : 1, dt * 8);
            float smooth = 1 - Mathf.Exp(-dt * 18);
            actualMaster = Mathf.Lerp(actualMaster, MasterVolume, smooth); actualMusic = Mathf.Lerp(actualMusic, MusicVolume, smooth);
            actualEffects = Mathf.Lerp(actualEffects, EffectsVolume, smooth); actualAmbience = Mathf.Lerp(actualAmbience, AmbienceVolume, smooth);
            duckGain = Mathf.MoveTowards(duckGain, ducked ? .58f : 1, dt * 2.2f);
            float master = actualMaster * muteGain;
            music.Tick(dt, master * actualMusic * .43f * musicMoodGain * duckGain);
            environment.Tick(dt, master * actualAmbience * .68f * (Mood == AudioMood.Collection ? .65f : 1));
            wind.volume = master * actualAmbience * .35f * windContextGain;
            if (now - lastChargePreview > .18f) requestedCharge = 0;
            chargeStrength = Mathf.MoveTowards(chargeStrength, requestedCharge, dt * (requestedCharge > chargeStrength ? 5 : 12));
            charge.volume = master * actualEffects * .11f * chargeStrength;
            charge.pitch = .94f + chargeStrength * .12f;
            if (charge.clip != null)
            {
                if (chargeStrength > .005f && !charge.isPlaying) charge.Play();
                else if (chargeStrength <= .005f && charge.isPlaying) charge.Stop();
            }
            foreach (var voice in voices) if (voice.source.isPlaying) voice.source.volume = master * actualEffects * voice.gain;
            if (preferenceSaveAt >= 0 && now >= preferenceSaveAt) SavePreferences();
        }

        private void SavePreferences() { if (preferenceSaveAt >= 0) { PlayerPrefs.Save(); preferenceSaveAt = -1; } }
        private void OnApplicationQuit() { SavePreferences(); }
        private void OnDisable()
        {
            requestedCharge = chargeStrength = 0; pendingCollect = pendingCombo = 0; pendingRare = false; pendingImpact = null;
            if (charge != null) charge.Stop(); SavePreferences();
        }
    }
}
