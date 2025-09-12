using UnityEngine;

/// <summary>
/// A stationary enemy. 
/// - Does nothing on beat (idle only).
/// - Plays an idle animation loop.
/// - Resets the level if the player enters its tile.
/// </summary>
[RequireComponent(typeof(Animator))]
public class EnemyStatic : EnemyBase
{
    /// <summary>
    /// On each active beat, static enemies just play idle animation feedback.
    /// </summary>
    protected override void OnBeatAction()
    {
        if (animator != null)
        {
            // Trigger a "pulse" or similar beat-synced animation if you want
            animator.SetTrigger("OnBeat");
        }
    }
}