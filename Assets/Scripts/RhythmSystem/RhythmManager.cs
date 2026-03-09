using System;
using UnityEngine;

/// <summary>
/// Timing grades for beat-aligned actions.
/// </summary>
// public enum BeatHitQuality
// {
//     Perfect,
//     Good,
//     Okay,
//     Bad
// }

public enum BeatHitQuality
{
    OnBeat,
    OffBeat
}

/// <summary>
/// Manages rhythm synchronisation and beat tracking using AudioSettings.dspTime.
/// Owns no AudioSources — all audio playback is delegated to AudioManager.
/// </summary>
public class RhythmManager : MonoBehaviour
{
    public static RhythmManager Instance { get; private set; }

    [Header("Track Settings")]
    [SerializeField] private TrackData currentTrack;          // Track metadata (BPM, clip, loop flag)
    
    [Header("Score Settings")]
    [Tooltip("Represents the percentage the player has to match to the current beat.")]
    [SerializeField] private float onBeatPercentage = 0.25f;
    
    [Header("Latency Compensation")]
    [Tooltip("Shifts the effective hit timestamp back by this many seconds to account for " +
             "audio output buffer lag and input polling delay. Start around 0.05–0.08 and " +
             "tune with a calibration screen. Positive = compensate for late-feeling hits.")]
    [Range(0f, 0.3f)]
    public float latencyCompensation = 0.06f;
    
    private double startDSPTime;            // DSP time when the track was started
    private double beatInterval;            // Seconds per beat at the current tempo
    private int    beatCount = 0;           // Total beats fired since track start
    private float  tempoMultiplier = 1f;    // Tempo scale (1 = normal, 2 = double speed)

    /// <summary>Broadcast on every beat. Subscribe to drive gameplay reactions.</summary>
    public static event Action OnBeat;

    private bool isPlaying = false;

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
    }

    private void Start()
    {
        if (currentTrack != null)
            PlayTrack(currentTrack);
    }

    /// <summary>
    /// Ticks beat timing every frame. Fires Beat() for every beat that has elapsed
    /// since the last Update to handle frame-rate drops gracefully.
    /// </summary>
    private void Update()
    {
        if (Utilities.IsGameFrozen) return;
        if (!isPlaying || currentTrack == null) return;
        if (!AudioManager.Instance.IsRhythmPlaying)  return;

        double songPos = AudioSettings.dspTime - startDSPTime;
        int expectedBeats = (int)(songPos / beatInterval);

        while (beatCount < expectedBeats)
            Beat();
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────────
    #region Playback Control

    /// <summary>
    /// Starts a rhythm track. Beat timing is synchronised to the DSP-scheduled
    /// playback that AudioManager handles.
    /// </summary>
    public void PlayTrack(TrackData track)
    {
        if (track == null || track.musicClip == null)
        {
            Debug.LogWarning("[RhythmManager] PlayTrack: TrackData or clip is null.");
            return;
        }

        currentTrack = track;
        tempoMultiplier = 1f;
        beatInterval = 60.0 / track.bpm;
        beatCount = 0;

        // Schedule slightly ahead so audio and beat timer start in sync
        double dspStartTime = AudioSettings.dspTime + 0.1;
        startDSPTime = dspStartTime;

        AudioManager.Instance.PlayRhythmTrack(track.musicClip, track.loop, dspStartTime, track.volume);

        isPlaying = true;
    }

    /// <summary>
    /// Pauses beat tracking and audio together.
    /// </summary>
    public void Pause()
    {
        if (!isPlaying) return;
        isPlaying = false;
        AudioManager.Instance.PauseRhythmTrack();
    }

    /// <summary>
    /// Resumes beat tracking and audio, realigning the next beat to avoid drift.
    /// </summary>
    public void Resume()
    {
        if (isPlaying) return;
        isPlaying = true;
        AudioManager.Instance.ResumeRhythmTrack();
    }

    /// <summary>
    /// Stops playback and resets all rhythm state.
    /// </summary>
    public void Stop()
    {
        isPlaying  = false;
        beatCount = 0;
        AudioManager.Instance.StopRhythmTrack();
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────────
    #region Tempo

    /// <summary>
    /// Scales the tempo and audio pitch together so gameplay and music stay in sync.
    /// </summary>
    /// <param name="multiplier">0.5 = half speed, 1.0 = normal, 2.0 = double speed.</param>
    public void SetTempoMultiplier(float multiplier)
    {
        tempoMultiplier = Mathf.Max(0.1f, multiplier);
        if (currentTrack == null) return;

        beatInterval = (60.0 / currentTrack.bpm) / tempoMultiplier;
        AudioManager.Instance.SetRhythmPitch(tempoMultiplier);

        // Re-anchor startDSPTime so that (dspTime - startDSPTime) % beatInterval
        // still maps correctly to beat positions under the new interval.
        // Without this, GetHitQuality drifts as soon as the interval changes.
        double dspNow = AudioSettings.dspTime;
        startDSPTime  = dspNow - (beatCount * beatInterval);
    }

    /// <summary>Resets tempo to 1× normal speed.</summary>
    public void ResetTempo() => SetTempoMultiplier(1f);

    #endregion
    // ─────────────────────────────────────────────────────────────────────────────
    #region Beat Queries

    /// <summary>
    /// Returns normalised progress (0–1) through the current beat.
    /// Useful for driving beat-pulse UI animations.
    /// </summary>
    public float GetBeatProgress()
    {
        double songPos = AudioSettings.dspTime - startDSPTime;
        double timeSinceLastBeat = songPos % beatInterval;
        return (float)(timeSinceLastBeat / beatInterval);
    }

    /// <summary>Returns the total number of beats fired since the track started.</summary>
    public int GetBeatCount() => beatCount;

    /// <summary>
    /// Evaluates the player's timing accuracy and returns a <see cref="BeatHitQuality"/> grade.
    /// </summary>
    public BeatHitQuality GetHitQuality()
    {
        if (!isPlaying || currentTrack == null)
            return BeatHitQuality.OffBeat;

        double compensatedTime = AudioSettings.dspTime - latencyCompensation;
        double songPos  = compensatedTime - startDSPTime;
        double timeSinceLastBeat = songPos % beatInterval;
        double distanceToBeat    = Math.Min(timeSinceLastBeat, beatInterval - timeSinceLastBeat);

        // Thresholds as a fraction of the beat interval — stays fair at any BPM
        return distanceToBeat <= beatInterval * onBeatPercentage
            ? BeatHitQuality.OnBeat
            : BeatHitQuality.OffBeat;
    }
    /// <summary>Returns whether the rhythm system is currently running.</summary>
    public bool IsPlaying => isPlaying;

    #endregion
    // ─────────────────────────────────────────────────────────────────────────────
    #region Private Helpers

    /// <summary>Fires the beat event and increments the beat counter.</summary>
    private void Beat()
    {
        beatCount++;
        OnBeat?.Invoke();
    }
    #endregion
}