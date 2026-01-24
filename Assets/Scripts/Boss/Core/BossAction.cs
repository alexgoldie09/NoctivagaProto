using System.Collections;
using UnityEngine;

/// <summary>
/// Base component for a boss action module.
/// </summary>
public abstract class BossAction : MonoBehaviour
{
    [Header("Action Identity")]
    [Tooltip("The name of the boss action. Used for lookup and identification.")]
    [SerializeField] private string actionName;

    /// <summary>
    /// Action identifier used for lookups.
    /// </summary>
    public string ActionName => actionName;
    
    /// <summary>
    /// Returns whether this action can run given the current boss context.
    /// </summary>
    public abstract bool CanRun(BossContext context);

    /// <summary>
    /// Executes the action logic as a coroutine.
    /// </summary>
    public abstract IEnumerator Execute(BossContext context);
}
