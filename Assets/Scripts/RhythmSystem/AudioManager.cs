using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Central audio hub for the game. Owns all AudioSources and handles:
///   - Rhythm tracks: DSP-scheduled playback driven by RhythmManager.
///   - BGM: a single looping background music track.
///   - SFX: one-shot clips with per-clip cooldown to prevent stacking.
///   - Ambience: looping layered ambient sounds that auto-start on Awake.
///   - Volume: persisted SFX and Music mixer group volumes via PlayerPrefs.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Mixer")]
    [Tooltip("The master AudioMixer asset. Required for volume control.")]
    public AudioMixer masterMixer;

    [Header("Rhythm Track")]
    [Tooltip("AudioSource used for DSP-scheduled rhythm track playback. Created automatically if left empty.")]
    public AudioSource rhythmSource;
    [Tooltip("AudioSource used for the SFX clips. Created automatically if left empty.")]
    public AudioSource sfxSource;

    [Header("SFX")]
    [Tooltip("All sound effect clips. Reference by clip name when calling PlaySFX().")]
    public List<AudioClip> sfxClips;
    [Tooltip("Minimum seconds before the same SFX clip can play again.")]
    public float sfxCooldown = 0.05f;

    // PlayerPrefs keys
    private const string PrefsSFXVolume   = "SFXVolume";
    private const string PrefsMusicVolume = "MusicVolume";

    // Mixer exposed parameter names — must match what you named them in the Mixer window
    private const string MixerSFXParam   = "SFXVolume";
    private const string MixerMusicParam = "MusicVolume";

    private Dictionary<string, AudioClip> sfxLookup   = new Dictionary<string, AudioClip>();
    private Dictionary<string, float> sfxLastPlayed   = new Dictionary<string, float>();

    // ─────────────────────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // DontDestroyOnLoad(gameObject);

        InitialiseRhythmSource();
        InitialiseSFXSource();
        RestoreVolumes();
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────────
    #region Initialisation

    private void InitialiseRhythmSource()
    {
        if (rhythmSource == null)
        {
            rhythmSource = gameObject.AddComponent<AudioSource>();
            rhythmSource.playOnAwake = false;
        }
    }

    private void InitialiseSFXSource()
    {
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }

        foreach (var clip in sfxClips)
            if (clip != null) sfxLookup[clip.name] = clip;
    }

    /// <summary>
    /// Applies saved volume levels from PlayerPrefs on startup.
    /// Falls back to 0.75 if no saved value exists.
    /// </summary>
    private void RestoreVolumes()
    {
        SetSFXVolume(PlayerPrefs.GetFloat(PrefsSFXVolume, 0.75f));
        SetMusicVolume(PlayerPrefs.GetFloat(PrefsMusicVolume, 0.75f));
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────────
    #region Volume Control

    /// <summary>
    /// Sets the SFX mixer group volume. Intended to be wired directly to a UI Slider
    /// via its OnValueChanged event. Value is saved to PlayerPrefs.
    /// </summary>
    /// <param name="sliderValue">Linear slider value in the range 0–1.</param>
    public void SetSFXVolume(float sliderValue)
    {
        ApplyMixerVolume(MixerSFXParam, sliderValue);
        PlayerPrefs.SetFloat(PrefsSFXVolume, sliderValue);
    }

    /// <summary>
    /// Sets the Music mixer group volume. Intended to be wired directly to a UI Slider
    /// via its OnValueChanged event. Value is saved to PlayerPrefs.
    /// </summary>
    /// <param name="sliderValue">Linear slider value in the range 0–1.</param>
    public void SetMusicVolume(float sliderValue)
    {
        ApplyMixerVolume(MixerMusicParam, sliderValue);
        PlayerPrefs.SetFloat(PrefsMusicVolume, sliderValue);
    }

    /// <summary>
    /// Returns the saved SFX volume (0–1) for initialising a slider on panel open.
    /// </summary>
    public float GetSFXVolume() => PlayerPrefs.GetFloat(PrefsSFXVolume, 0.75f);

    /// <summary>
    /// Returns the saved Music volume (0–1) for initialising a slider on panel open.
    /// </summary>
    public float GetMusicVolume() => PlayerPrefs.GetFloat(PrefsMusicVolume, 0.75f);

    /// <summary>
    /// Converts a linear 0–1 value to decibels and applies it to the named mixer parameter.
    /// Clamps the minimum to avoid log(0).
    /// </summary>
    private void ApplyMixerVolume(string parameterName, float sliderValue)
    {
        if (masterMixer == null)
        {
            Debug.LogWarning("[AudioManager] masterMixer is not assigned.");
            return;
        }

        float dB = Mathf.Log10(Mathf.Max(sliderValue, 0.0001f)) * 20f;
        masterMixer.SetFloat(parameterName, dB);
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────────
    #region Rhythm Track

    /// <summary>
    /// Schedules a rhythm track for playback at a precise DSP time.
    /// Called by RhythmManager when starting a track.
    /// </summary>
    /// <param name="clip">The audio clip to play.</param>
    /// <param name="loop">Whether the clip should loop.</param>
    /// <param name="dspStartTime">Exact DSP time at which playback begins.</param>
    /// <param name="volume">Volume of the clip to set.</param>
    public void PlayRhythmTrack(AudioClip clip, bool loop, double dspStartTime, float volume)
    {
        if (clip == null)
        {
            Debug.LogWarning("[AudioManager] PlayRhythmTrack: clip is null.");
            return;
        }

        rhythmSource.clip   = clip;
        rhythmSource.loop   = loop;
        rhythmSource.pitch  = 1f;
        rhythmSource.volume = volume;
        rhythmSource.PlayScheduled(dspStartTime);
    }

    /// <summary>
    /// Pauses the rhythm track.
    /// </summary>
    public void PauseRhythmTrack() => rhythmSource.Pause();

    /// <summary>
    /// Resumes the rhythm track after a pause.
    /// </summary>
    public void ResumeRhythmTrack() => rhythmSource.UnPause();

    /// <summary>
    /// Stops and clears the rhythm track.
    /// </summary>
    public void StopRhythmTrack()
    {
        rhythmSource.Stop();
        rhythmSource.clip = null;
    }

    /// <summary>
    /// Adjusts the pitch of the rhythm track to match a tempo multiplier.
    /// A pitch of 2.0 plays audio at double speed; 0.5 at half speed.
    /// </summary>
    public void SetRhythmPitch(float pitch)
    {
        rhythmSource.pitch = Mathf.Max(0.1f, pitch);
    }

    /// <summary>
    /// Returns true if the rhythm AudioSource is currently playing.
    /// </summary>
    public bool IsRhythmPlaying => rhythmSource != null && rhythmSource.isPlaying;

    #endregion
    // ─────────────────────────────────────────────────────────────────────────────
    #region SFX

    /// <summary>
    /// Plays a one-shot SFX by name. Silently skipped if the clip is still on cooldown
    /// or if the key is not found in the SFX list.
    /// </summary>
    public void PlaySFX(string key, float volume = 1f)
    {
        if (!sfxLookup.TryGetValue(key, out var clip))
        {
            Debug.LogWarning($"[AudioManager] SFX '{key}' not found. Check sfxClips list.");
            return;
        }

        float now = Time.time;
        if (sfxLastPlayed.TryGetValue(key, out float lastTime) && now - lastTime < sfxCooldown)
            return; // still cooling down

        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        sfxLastPlayed[key] = now;
    }

    /// <summary>
    /// Stops all SFX immediately and resets all cooldown timers.
    /// </summary>
    public void StopAllSFX()
    {
        if (sfxSource != null && sfxSource.isPlaying)
            sfxSource.Stop();

        sfxLastPlayed.Clear();
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────────
}