using System;
using UnityEngine;

/// <summary>
/// Controls rhythm synchronization using AudioSettings.dspTime for precise beat tracking.
/// Relies on native audio looping to ensure perfect sync with beat timing.
/// </summary>
 
public enum BeatHitQuality 
{ 
    Perfect, 
    Good, 
    Okay, 
    Bad 
} // Beat hits and their grading

[RequireComponent(typeof(AudioSource))]
public class RhythmManager : MonoBehaviour
{
    public static RhythmManager Instance { get; private set; }

    public TrackData currentTrack;          // Track metadata (BPM, clip, loop flag)
    private AudioSource audioSource;        // Audio playback source

    private double startDSPTime;            // When the track started (DSP time)
    private double nextBeatTime;            // Timestamp of the next expected beat
    private double beatInterval;            // Seconds between beats based on BPM
    private int beatCount = 0;              // Total beats since start
    private float tempoMultiplier = 1f;     // Multiplier for the tempo

    public static event Action OnBeat;  // Optional global beat event
    private bool isPlaying = false;

    // ─────────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        audioSource = GetComponent<AudioSource>();
    }
    
    private void Start()
    {
        if (currentTrack != null)
        {
            PlayTrack(currentTrack);
        }
    }

    private void Update()
    {
        if (Utilities.IsGameFrozen) return; // skip ticking while frozen
        
        if (!isPlaying || currentTrack == null || !audioSource.isPlaying)
            return;

        double dspTime = AudioSettings.dspTime;
        double songPos = dspTime - startDSPTime;

        while (songPos >= nextBeatTime - startDSPTime)
        {
            Beat(); // Trigger beat event
            nextBeatTime += beatInterval;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Begins playback of a rhythm track using precise DSP scheduling.
    /// </summary>
    public void PlayTrack(TrackData track)
    {
        if (track == null || track.musicClip == null)
        {
            Debug.LogWarning("RhythmManager: Invalid track.");
            return;
        }

        currentTrack = track;
        beatInterval = 60.0 / track.bpm;
        beatCount = 0;

        double dspStartTime = AudioSettings.dspTime + 0.1f;
        startDSPTime = dspStartTime;
        nextBeatTime = dspStartTime + beatInterval;

        audioSource.clip = track.musicClip;
        audioSource.loop = track.loop;
        audioSource.PlayScheduled(dspStartTime);

        isPlaying = true;
    }

    /// <summary>
    /// Called on every beat.
    /// Broadcasts global OnBeat event.
    /// </summary>
    private void Beat()
    {
        beatCount++;
        OnBeat?.Invoke();
    }

    /// <summary>
    /// Pauses audio and beat system.
    /// </summary>
    public void Pause()
    {
        isPlaying = false;
        audioSource.Pause();
    }

    /// <summary>
    /// Resumes audio and beat tracking.
    /// </summary>
    public void Resume()
    {
        isPlaying = true;
        audioSource.UnPause();

        // Realign next beat after pause
        double dspTime = AudioSettings.dspTime;
        double songPos = dspTime - startDSPTime;
        double beatsPassed = Mathf.FloorToInt((float)(songPos / beatInterval));
        nextBeatTime = startDSPTime + (beatsPassed + 1) * beatInterval;
    }

    /// <summary>
    /// Stops playback and resets state.
    /// </summary>
    public void Stop()
    {
        isPlaying = false;
        audioSource.Stop();
        nextBeatTime = 0;
        beatCount = 0;
    }

    /// <summary>
    /// Returns the percentage (0–1) through the current beat.
    /// Useful for animating beat UI bars.
    /// </summary>
    public float GetBeatProgress()
    {
        double dspTime = AudioSettings.dspTime;
        double songPos = dspTime - startDSPTime;
        double timeSinceLastBeat = songPos % beatInterval;
        return (float)(timeSinceLastBeat / beatInterval);
    }

    /// <summary>
    /// Returns the number of beats since the track started.
    /// </summary>
    public int GetBeatCount() => beatCount;
    
    /// <summary>
    /// Evaluates player's timing accuracy relative to the nearest beat.
    /// Returns a BeatHitQuality.
    /// </summary>
    public BeatHitQuality GetHitQuality()
    {
        if (!isPlaying || currentTrack == null) 
            return BeatHitQuality.Bad;

        double dspTime = AudioSettings.dspTime;
        double songPos = dspTime - startDSPTime;

        // Time since last beat
        double timeSinceLastBeat = songPos % beatInterval;

        // Distance to closest beat (in seconds)
        double distanceToBeat = Math.Min(timeSinceLastBeat, beatInterval - timeSinceLastBeat);

        // Thresholds (tune these!)
        if (distanceToBeat <= 0.06f)   return BeatHitQuality.Perfect; // ±60ms
        if (distanceToBeat <= 0.12f)   return BeatHitQuality.Good;    // ±120ms
        if (distanceToBeat <= 0.20f)   return BeatHitQuality.Okay;    // ±200ms
        return BeatHitQuality.Bad;
    }

    /// <summary>
    /// Adjusts rhythm tempo (0.5 = half speed, 2.0 = double speed).
    /// Updates beat interval and audio pitch so gameplay + music stay in sync.
    /// </summary>
    public void SetTempoMultiplier(float multiplier)
    {
        tempoMultiplier = Mathf.Max(0.1f, multiplier); // avoid 0/negative
        if (currentTrack == null || audioSource == null) return;

        // Update beat interval
        beatInterval = (60.0 / currentTrack.bpm) / tempoMultiplier;

        // Adjust audio playback speed
        audioSource.pitch = tempoMultiplier;

        // Realign beat scheduling to avoid drift
        double dspTime = AudioSettings.dspTime;
        double songPos = dspTime - startDSPTime;
        double beatsPassed = Mathf.FloorToInt((float)(songPos / beatInterval));
        nextBeatTime = startDSPTime + (beatsPassed + 1) * beatInterval;
    }

    /// <summary>
    /// Reset tempo back to normal (1.0).
    /// </summary>
    public void ResetTempo()
    {
        SetTempoMultiplier(1f);
    }
}
