using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base component for a boss action module.
/// </summary>
public abstract class BossAction : MonoBehaviour
{
    [Header("Action Identity")]
    [Tooltip("The name of the boss action. Used for lookup and identification.")]
    [SerializeField] private string actionName;
    
    [Header("Phase Objects")]
    [Tooltip("Objects to enable or disable while this action runs.")]
    [SerializeField] private List<GameObject> phaseObjectsToToggle = new();

    /// <summary>
    /// Action identifier used for lookups.
    /// </summary>
    public string ActionName => actionName;
    
    /// <summary>
    /// Enables or disables any phase objects configured for this action.
    /// </summary>
    /// <param name="active">Whether the objects should be active.</param>
    protected void SetPhaseObjectsActive(bool active)
    {
        if (phaseObjectsToToggle == null)
            return;

        for (int i = 0; i < phaseObjectsToToggle.Count; i++)
        {
            var obj = phaseObjectsToToggle[i];
            if (obj != null)
                obj.SetActive(active);
        }
    }
    
    /// <summary>
    /// Returns whether this action can run given the current boss context.
    /// </summary>
    public abstract bool CanRun(BossContext context);

    /// <summary>
    /// Executes the action logic as a coroutine.
    /// </summary>
    public abstract IEnumerator Execute(BossContext context);
}
