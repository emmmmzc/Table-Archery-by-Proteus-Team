using System;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [Serializable]
    public class Sound
    {
        public string name;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    public static AudioManager Instance { get; private set; }

    [Header("Sounds")]
    [SerializeField] private Sound[] sounds;

    private readonly Dictionary<string, Sound> soundLookup = new Dictionary<string, Sound>(StringComparer.Ordinal);
    private AudioSource sfxSource;
    private AudioSource musicSource;
    private string currentMusicName;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        sfxSource = GetComponent<AudioSource>();
        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;

        BuildLookup();
    }

    private void BuildLookup()
    {
        soundLookup.Clear();
        if (sounds == null) return;

        foreach (Sound sound in sounds)
        {
            if (sound == null) continue;
            if (string.IsNullOrWhiteSpace(sound.name) || sound.clip == null) continue;
            if (!soundLookup.ContainsKey(sound.name))
                soundLookup.Add(sound.name, sound);
        }
    }

    public void Play(string soundName)
    {
        if (string.IsNullOrEmpty(soundName)) return;
        if (!soundLookup.TryGetValue(soundName, out Sound sound)) return;

        sfxSource.PlayOneShot(sound.clip, sound.volume);
    }

    public bool TryGetSound(string soundName, out AudioClip clip, out float volume)
    {
        clip = null;
        volume = 1f;

        if (string.IsNullOrEmpty(soundName)) return false;
        if (!soundLookup.TryGetValue(soundName, out Sound sound)) return false;
        if (sound.clip == null) return false;

        clip = sound.clip;
        volume = sound.volume;
        return true;
    }

    public void PlayMusic(string soundName)
    {
        if (string.IsNullOrEmpty(soundName)) return;
        if (!soundLookup.TryGetValue(soundName, out Sound sound)) return;

        if (currentMusicName == sound.name && musicSource.isPlaying) return;

        currentMusicName = sound.name;
        musicSource.clip = sound.clip;
        musicSource.volume = sound.volume;
        musicSource.loop = true;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource.isPlaying)
            musicSource.Stop();
    }
}
