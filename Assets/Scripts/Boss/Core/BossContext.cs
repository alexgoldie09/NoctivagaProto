using UnityEngine;

/// <summary>
/// Context container passed to boss actions for shared references.
/// </summary>
public class BossContext
{
    public BossControllerBase controller;
    public BossHealth health;
    public TilemapGridManager grid;
    public PlayerController player;
    public Animator animator;
    public SpriteRenderer spriteRenderer;
    public BossPostFxController postFx;
    public BossShieldHealth shieldHealth;
}