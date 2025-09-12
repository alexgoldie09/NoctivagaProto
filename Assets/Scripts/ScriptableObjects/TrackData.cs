using UnityEngine;

[CreateAssetMenu(fileName = "New Track", menuName = "Rhythm/Track Data")]
public class TrackData : ScriptableObject
{
    [Header("Audio")]
    public AudioClip musicClip;              // The actual audio/music file

    [Header("Timing")]
    [Tooltip("Beats per minute of the track.")]
    public float bpm = 120f;

    [Tooltip("Offset to align the track's beat start.")]
    public float beatOffset = 0f;

    [Header("Looping")]
    [Tooltip("Enable if the track should loop.")]
    public bool loop = true;

    [Tooltip("Optional loop start point in seconds.")]
    public float loopStartTime = 0f;
}