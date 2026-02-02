using System.Collections;
using UnityEngine;

/// <summary>
/// Simple hazard spawned during decoy telegraphs that kills the player on contact.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class VampireQueenDecoyHazard : MonoBehaviour
{
    [Tooltip("Seconds before the hazard despawns automatically.")]
    [SerializeField] private float defaultLifetime = 0.5f;

    private Coroutine despawnRoutine;

    private void Awake()
    {
        var collider = GetComponent<Collider2D>();
        if (collider != null)
            collider.isTrigger = true;
    }

    /// <summary>
    /// Arms the hazard and schedules despawn after the specified lifetime.
    /// </summary>
    public void Arm()
    {
        if (despawnRoutine != null)
            StopCoroutine(despawnRoutine);
        
        despawnRoutine = StartCoroutine(DespawnAfter(defaultLifetime));
    }

    private IEnumerator DespawnAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            GameManager.Instance?.PlayerKilled();
    }
}