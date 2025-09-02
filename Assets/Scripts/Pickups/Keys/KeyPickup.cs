using UnityEngine;

public class KeyPickup : MonoBehaviour
{
    public string keyID = "default";
    public Vector2Int gridPosition;

    void Start()
    {
        transform.position = new Vector3(gridPosition.x,gridPosition.y,0f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.tag == "Player")
        {
            var inventory = other.GetComponent<PlayerInventory>();
            if (inventory != null)
            {
                inventory.AddKey(keyID);
                Destroy(gameObject); // Remove from scene after pickup
            }
        }
    }
}