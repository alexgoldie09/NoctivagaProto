using UnityEngine;

/// <summary>
/// Simple straight-line projectile used by ranged enemies.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class EnemyStraightProjectile : MonoBehaviour
{
    [Header("Collision")]
    [Tooltip("Layer mask used for wall/blocks collision checks.")]
    [SerializeField] private LayerMask wallLayers;

    [Header("Movement")]
    [Tooltip("Movement speed in world units per second.")]
    [SerializeField] private float speed = 6f;
    
    [Header("Damage Force")]
    [Tooltip("If true, allow camera shake.")]
    [SerializeField] protected bool allowDamageShake = true;
    [Tooltip("Shake force for camera on damage.")]
    [SerializeField] protected float damageShakeForce = 0.8f;
    
    [Header("Lifespan")]
    [Tooltip("Lifetime before the projectile is destroyed.")]
    [SerializeField] private float lifetime = 4f;
    [Tooltip("Optional impact VFX prefab spawned when the projectile is destroyed on impact.")]
    [SerializeField] private GameObject impactVfxPrefab;

    private Vector2 direction = Vector2.right;
    private float age;

    /// <summary>
    /// Initializes the projectile with a travel direction.
    /// </summary>
    public void Initialize(Vector2 dir)
    {
        if (dir.sqrMagnitude > 0.001f)
        {
            direction = dir.normalized;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
        
        var body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
        }
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetime)
        {
            SpawnImpactVfx();
            Destroy(gameObject);
            return;
        }

        transform.position += (Vector3)(direction * speed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if ((wallLayers.value & (1 << other.gameObject.layer)) != 0)
        {
            SpawnImpactVfx();
            Destroy(gameObject);
            return;
        }

        if (other.CompareTag("Player"))
        {
            var player = other.GetComponent<PlayerController>();
            if (player != null && player.IsShadowMode)
                return;

            GameManager.Instance?.PlayerKilled();
            SpawnImpactVfx();
            Destroy(gameObject);
        }
    }
    
    private void SpawnImpactVfx()
    {
        if (impactVfxPrefab != null)
            Instantiate(impactVfxPrefab, transform.position, Quaternion.identity);
        
        // Cinemachine Impulse shake
        if (damageShakeForce > 0f && allowDamageShake)
            CameraShake.Instance?.Shake(damageShakeForce);
    }
}