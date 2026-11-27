using System.Collections.Generic;
using UnityEngine;

namespace IronKnuckle.Core
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private AudioSource bgmSource;
        private readonly List<AudioSource> sfxPool = new List<AudioSource>();
        private readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();
        private int poolIndex;

        public float SfxVolume { get; private set; } = 0.85f;
        public float BgmVolume { get; private set; } = 0.55f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;

            for (int i = 0; i < 8; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                sfxPool.Add(src);
            }
        }

        public void SetVolumes(float sfx, float bgm)
        {
            SfxVolume = Mathf.Clamp01(sfx);
            BgmVolume = Mathf.Clamp01(bgm);
            if (bgmSource != null) bgmSource.volume = BgmVolume;
        }

        public void PlayBGM(string clipName)
        {
            var clip = LoadClip("BGM/" + clipName);
            if (clip == null) return;
            if (bgmSource.clip == clip && bgmSource.isPlaying) return;
            bgmSource.clip = clip;
            bgmSource.volume = BgmVolume;
            bgmSource.Play();
        }

        public void PlaySFX(string clipName, float volumeScale = 1f, float pitchJitter = 0.06f)
        {
            var clip = LoadClip("SFX/" + clipName);
            if (clip == null || sfxPool.Count == 0) return;

            var src = sfxPool[poolIndex];
            poolIndex = (poolIndex + 1) % sfxPool.Count;
            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.volume = SfxVolume * volumeScale;
            src.PlayOneShot(clip);
        }

        private AudioClip LoadClip(string relativePath)
        {
            if (clipCache.TryGetValue(relativePath, out var cached)) return cached;
            var clip = Resources.Load<AudioClip>("Audio/" + relativePath);
            if (clip != null) clipCache[relativePath] = clip;
            return clip;
        }
    }
}
