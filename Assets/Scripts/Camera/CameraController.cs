using UnityEngine;

/// <summary>
/// Controls the camera positioning and zoom to fit the entire grid area on screen.
/// Automatically centers and resizes the orthographic camera based on GridManager dimensions.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour 
{
    public float padding = 1f;              // Extra space around grid edges
    public float cameraZOffset = -10f;      // Z-position offset for 2D camera depth

    private Camera cam;                     // Cached reference to the Camera component

    /// <summary>
    /// Initializes the camera on game start.
    /// Attempts to center and resize camera based on grid size.
    /// </summary>
    void Start() 
    {
        cam = GetComponent<Camera>();

        if (GridManager.Instance != null) 
        {
            CenterAndResizeCamera();
        } 
        else 
        {
            Debug.LogWarning("CameraController: GridManager.Instance is null.");
        }
    }

    /// <summary>
    /// Calculates and sets camera position and orthographic size
    /// to fully encompass the grid area with proper padding.
    /// </summary>
    public void CenterAndResizeCamera() 
    {
        int width = GridManager.Instance.width;
        int height = GridManager.Instance.height;

        // Position camera in the center of the grid at a fixed Z offset
        Vector3 centerPos = new Vector3(width / 2f - 0.5f, height / 2f - 0.5f, cameraZOffset);
        transform.position = centerPos;

        float aspect = cam.aspect;

        // Compute orthographic size based on both height and width
        float sizeBasedOnHeight = (height / 2f) + padding;
        float sizeBasedOnWidth = ((width / aspect) / 2f) + padding;

        // Choose the larger size to ensure full visibility
        cam.orthographicSize = Mathf.Max(sizeBasedOnHeight, sizeBasedOnWidth);
    }
}