using UnityEngine;

[DisallowMultipleComponent]
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
public class CameraGroundCoverage : MonoBehaviour
{
    [SerializeField] private InputManager _inputManager;
    [SerializeField] private Camera _camera;
    [SerializeField] private float _extraMargin;

    private void OnEnable()
    {
        FitToPanBounds();
    }

    private void OnValidate()
    {
        FitToPanBounds();
    }

    public void FitToPanBounds()
    {
        if (_inputManager == null)
            _inputManager = FindFirstObjectByType<InputManager>();

        if (_camera == null)
            _camera = Camera.main;

        if (_inputManager == null || !_inputManager.UsePanBounds)
            return;

        Vector2 min = _inputManager.PanMinXZ;
        Vector2 max = _inputManager.PanMaxXZ;
        float margin = CalculateMargin();

        float width = Mathf.Max(1f, max.x - min.x + margin * 2f);
        float depth = Mathf.Max(1f, max.y - min.y + margin * 2f);
        float centerX = (min.x + max.x) * 0.5f;
        float centerZ = (min.y + max.y) * 0.5f;

        transform.position = new Vector3(centerX, _inputManager.GroundHeight, centerZ);
        transform.localScale = new Vector3(width / 10f, 1f, depth / 10f);
    }

    private float CalculateMargin()
    {
        if (_camera == null)
            return _extraMargin;

        Transform cameraTransform = _camera.transform;
        float horizontalOffset = new Vector2(cameraTransform.localPosition.x, cameraTransform.localPosition.z).magnitude;
        float verticalOffset = Mathf.Max(0f, cameraTransform.localPosition.y);
        float pitchRadians = Mathf.Deg2Rad * Mathf.Abs(cameraTransform.localEulerAngles.x);
        float viewReach = horizontalOffset + verticalOffset / Mathf.Max(0.15f, Mathf.Tan(pitchRadians));
        float frustumRadius = viewReach * Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad * 0.5f);

        return _extraMargin + frustumRadius;
    }
}
