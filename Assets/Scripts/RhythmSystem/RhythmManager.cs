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
    
    [Header("Input Offset")]
    [Tooltip("Shifts hit evaluation earlier by this many seconds. Use a small positive value " +
             "if player input feels consistently late compared to the music.")]
    [Range(0f, 0.3f)]
    public float inputOffsetSeconds = 0.06f;
    
    private double startDSPTime;            // DSP time when the track was started
    private double beatInterval;            // Seconds per beat at the current tempo
    private int    beatCount = 0;           // Total beats fired since track start
    private float  tempoMultiplier = 1f;    // Tempo scale (1 = normal, 2 = double speed)

    /// <summary>Broadcast on every beat. Subscribe to drive gameplay reactions.</summary>
    public static event Action OnBeat;

    private bool isPlaying = false;
    private bool pendingStartSync = false;

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
            AudioManager.Instance?.RequestTrack(currentTrack);
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

        if (pendingStartSync)
            SyncStartTimeFromAudioSource();

        double songPos = GetSongPosition(AudioSettings.dspTime);
        if (songPos < 0d)
            return;
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

        // Schedule slightly ahead to avoid race conditions at start.
        double dspStartTime = AudioSettings.dspTime + 0.1;
        startDSPTime = dspStartTime;

        AudioManager.Instance.PlayRhythmTrack(track.musicClip, track.loop, dspStartTime, track.volume);

        // We will re-anchor to the source's *actual* playback position on first update.
        // This removes first-run startup variance from scoring alignment.
        pendingStartSync = true;
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
        pendingStartSync = false;
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

        double dspNow = AudioSettings.dspTime;
        double oldBeatInterval = beatInterval;
        double songPosNow = GetSongPosition(dspNow);
        double beatPosition = oldBeatInterval > 0d ? songPosNow / oldBeatInterval : 0d;

        beatInterval = (60.0 / currentTrack.bpm) / tempoMultiplier;
        AudioManager.Instance.SetRhythmPitch(tempoMultiplier);
        
        // Preserve sub-beat phase when tempo changes (important for half-time mode).
        // Using the continuous beat position avoids snapping timing to the last full beat.
        startDSPTime = dspNow - (beatPosition * beatInterval);
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
        double songPos = GetSongPosition(AudioSettings.dspTime);
        if (songPos <= 0d || beatInterval <= 0d)
            return 0f;

        return (float)GetTimeSinceLastBeat(songPos) / (float)beatInterval;
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

        double compensatedTime = AudioSettings.dspTime - inputOffsetSeconds;
        double songPos  = GetSongPosition(compensatedTime);
        if (songPos < 0d || beatInterval <= 0d)
            return BeatHitQuality.OffBeat;

        double timeSinceLastBeat = GetTimeSinceLastBeat(songPos);
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
    
    private double GetSongPosition(double dspTime)
    {
        return dspTime - startDSPTime;
    }

    private double GetTimeSinceLastBeat(double songPosition)
    {
        double wrapped = songPosition % beatInterval;
        return wrapped < 0d ? wrapped + beatInterval : wrapped;
    }
    
    private void SyncStartTimeFromAudioSource()
    {
        AudioSource source = AudioManager.Instance != null ? AudioManager.Instance.rhythmSource : null;
        if (source == null || source.clip == null)
            return;
        
        // Wait until actually playing, not just scheduled
        if (!source.isPlaying) return;

        // We only sync once the source has advanced into the clip.
        if (source.timeSamples < 0)
            return;

        double clipSeconds = source.timeSamples / (double)source.clip.frequency;
        startDSPTime = AudioSettings.dspTime - clipSeconds;
        pendingStartSync = false;
    }
    #endregion
}