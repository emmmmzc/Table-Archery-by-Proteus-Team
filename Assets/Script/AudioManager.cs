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
    private AudioSource audioSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

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

        audioSource.PlayOneShot(sound.clip, sound.volume);
    }
}
