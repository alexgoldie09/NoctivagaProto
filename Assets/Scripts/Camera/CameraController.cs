using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour 
{
    public float padding = 1f;
    public float cameraZOffset = -10f;

    private Camera cam;

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

    public void CenterAndResizeCamera() 
    {
        int width = GridManager.Instance.width;
        int height = GridManager.Instance.height;

        Vector3 centerPos = new Vector3(width / 2f - 0.5f, height / 2f - 0.5f, cameraZOffset);
        transform.position = centerPos;

        float aspect = cam.aspect;
        float sizeBasedOnHeight = (height / 2f) + padding;
        float sizeBasedOnWidth = ((width / aspect) / 2f) + padding;

        cam.orthographicSize = Mathf.Max(sizeBasedOnHeight, sizeBasedOnWidth);
    }
}