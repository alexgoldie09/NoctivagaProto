using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Lightweight helper that exposes access to a URP Volume and its runtime-cloned VolumeProfile,
/// so boss actions can toggle overrides on/off without changing authored values.
/// </summary>
public class BossPostFxController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Scene Volume to drive (prefer a Global Volume). If null, will auto-find the first Volume in scene.")]
    [SerializeField] private Volume volume;

    [Header("Profile")]
    [Tooltip("Base profile asset to clone at runtime. Author your desired values here.")]
    [SerializeField] private VolumeProfile baseProfile;

    private VolumeProfile runtimeProfile;

    /// <summary>The runtime profile instance being driven (clone of baseProfile).</summary>
    public VolumeProfile RuntimeProfile => runtimeProfile;

    /// <summary>The driven scene Volume.</summary>
    public Volume Volume => volume;

    private void Awake()
    {
        if (volume == null)
            volume = FindAnyObjectByType<Volume>();

        if (volume == null)
        {
            // Debug.LogError("[BossPostFxController] No Volume found in scene.");
            enabled = false;
            return;
        }

        if (baseProfile == null)
        {
            // Debug.LogError("[BossPostFxController] Base Profile is not assigned.");
            enabled = false;
            return;
        }

        // Clone so we never mutate the asset.
        runtimeProfile = Instantiate(baseProfile);
        volume.profile = runtimeProfile;
    }

    /// <summary>
    /// Enables/disables a specific override component by type (toggles VolumeComponent.active).
    /// Does NOT change values.
    /// </summary>
    public bool SetOverrideActive<T>(bool active) where T : VolumeComponent
    {
        if (runtimeProfile == null)
            return false;

        if (!runtimeProfile.TryGet(out T component) || component == null)
            return false;

        component.active = active;
        return true;
    }
    
    /// <summary>
    /// Tries to get a Volume override from the runtime profile.
    /// Optionally adds it if missing.
    /// </summary>
    public bool TryGetOverride<T>(out T component, bool addIfMissing = false) where T : VolumeComponent
    {
        component = null;

        if (RuntimeProfile == null)
            return false;

        if (RuntimeProfile.TryGet(out component) && component != null)
            return true;

        if (!addIfMissing)
            return false;

        component = RuntimeProfile.Add<T>(true);
        return component != null;
    }

    /// <summary>
    /// Convenience: returns ColorAdjustments from runtime profile.
    /// </summary>
    public bool TryGetColorAdjustments(out ColorAdjustments color, bool addIfMissing = false)
        => TryGetOverride(out color, addIfMissing);

    /// <summary>
    /// Convenience helper to enable a common Phase 3 stack.
    /// This does NOT change values, only activates overrides you already authored.
    /// </summary>
    public void SetPhase3LookActive(bool active)
    {
        SetOverrideActive<ChromaticAberration>(active);
        SetOverrideActive<ColorAdjustments>(active);
        SetOverrideActive<Vignette>(active);
    }

    /// <summary>
    /// Disables every override in the runtime profile. Optionally keeps Bloom active.
    /// </summary>
    public void DisableAllOverrides(bool keepBloom)
    {
        if (runtimeProfile == null)
            return;

        for (int i = 0; i < runtimeProfile.components.Count; i++)
        {
            var comp = runtimeProfile.components[i];
            if (comp == null)
                continue;

            if (keepBloom && comp is Bloom)
                continue;

            comp.active = false;
        }
    }
}
