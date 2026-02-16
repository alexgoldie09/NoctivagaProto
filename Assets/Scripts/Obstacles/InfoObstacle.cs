using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Obstacle that toggles displays info tips.
/// </summary>
public class InfoObstacle : ObstacleBase
{
    [Header("Info Visuals")]
    [SerializeField] private Sprite infoOffSprite;
    [SerializeField] private Sprite infoOnSprite;
    
    [Header("Info Lighting")]
    [SerializeField] private Light2D infoLight;

    private bool isOn = false;

    /// <summary>
    /// Initializes sprite renderer
    /// </summary>
    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        ApplyInfoVisual();

        if (infoLight != null)
        {
            infoLight.intensity = isOn ? 1.5f : 0f;
        }

    }

    /// <summary>
    /// Toggles target tiles, triggers player fall reset, and optionally eliminates enemies on new void tiles.
    /// </summary>
    public override void Interact()
    {
        return;
    }

    /// <summary>
    /// Info remain blocking for player movement.
    /// </summary>
    public override bool BlocksMovement() => true;

    /// <summary>
    /// Info remain blocking for shape placement.
    /// </summary>
    public override bool BlocksShapePlacement() => true;

    public override void ShowPrompt(PlayerController player)
    {
        // Toggle
        isOn = !isOn;

        // Visual update
        ApplyInfoVisual();
        
        base.ShowPrompt(player);
    }

    public override void HidePrompt()
    {
        // Toggle
        isOn = !isOn;

        // Visual update
        ApplyInfoVisual();
        
        base.HidePrompt();
    }

    /// <summary>
    /// Changes sprites for the info
    /// </summary>
    private void ApplyInfoVisual()
    {
        if (sr == null) return;

        sr.sprite = isOn ? infoOnSprite : infoOffSprite;
        
        if (infoLight != null)
        {
            infoLight.intensity = isOn ? 1.5f : 0f;
        }

        if (sr.sprite == null)
            Debug.LogWarning($"[InfoObstacle] Missing info sprite (isOn={isOn}) on {name}", this);
    }
}
