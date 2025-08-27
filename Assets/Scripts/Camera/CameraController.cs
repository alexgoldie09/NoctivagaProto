using UnityEngine;

public class CameraController : MonoBehaviour 
{
    public GridManager gridManager;
    public float padding = 1f;           // Extra space around the grid
    public float cameraZOffset = -10f;   // Keep camera in 2D view
    
    private Camera cam;

    void Start() 
    {
        cam = GetComponent<Camera>();
        
        if (gridManager != null) 
        {
            CenterAndResizeCamera();
        }
    }

    public void CenterAndResizeCamera() 
    {
        int width = gridManager.width;
        int height = gridManager.height;

        // Center the camera on the grid
        Vector3 centerPos = new Vector3(width / 2f - 0.5f, height / 2f - 0.5f, cameraZOffset);
        transform.position = centerPos;

        // Adjust orthographic size to fit grid height
        float aspect = cam.aspect; // width / height
        float sizeBasedOnHeight = (height / 2f) + padding;
        float sizeBasedOnWidth = ((width / aspect) / 2f) + padding;

        // Choose the larger value to ensure full grid fits
        cam.orthographicSize = Mathf.Max(sizeBasedOnHeight, sizeBasedOnWidth);
    }
}