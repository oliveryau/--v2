using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class InputManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera _camera;
    [SerializeField] private Transform _panTarget;

    [Header("Pan Plane")]
    [SerializeField] private float _groundHeight;

    [Header("Bounds")]
    [SerializeField] private bool _useBounds = true;
    [SerializeField] private Vector2 _minXZ;
    [SerializeField] private Vector2 _maxXZ;

    [Header("Input")]
    [SerializeField] private bool _ignoreUI = true;
    [SerializeField] private float _dragThresholdPixels;

    [Header("Debug")]
    [SerializeField] private bool _drawBoundsGizmo = true;

    private Plane _groundPlane;
    private bool _pointerDown;
    private bool _dragging;
    private int _activePointerId = InvalidPointerId;
    private Vector2 _pointerDownScreen;
    private Vector2 _lastScreenPosition;

    private const int InvalidPointerId = -1;

    private void Awake()
    {
        if (_camera == null)
            _camera = Camera.main;

        if (_panTarget == null)
            _panTarget = transform;

        _groundPlane = new Plane(Vector3.up, new Vector3(0f, _groundHeight, 0f));
    }

    private void OnValidate()
    {
        if (_maxXZ.x < _minXZ.x)
            _maxXZ.x = _minXZ.x;

        if (_maxXZ.y < _minXZ.y)
            _maxXZ.y = _minXZ.y;

        _dragThresholdPixels = Mathf.Max(0f, _dragThresholdPixels);
    }

    private void Update()
    {
        if (_camera == null || _panTarget == null)
            return;

        if (Input.touchSupported && Input.touchCount > 0)
            HandleTouch();
        else
            HandleMouse();
    }

    private void HandleTouch()
    {
        if (Input.touchCount > 1)
        {
            EndPointer();
            return;
        }

        Touch touch = Input.GetTouch(0);

        switch (touch.phase)
        {
            case TouchPhase.Began:
                TryBeginPointer(touch.position, touch.fingerId);
                break;

            case TouchPhase.Moved:
            case TouchPhase.Stationary:
                UpdatePointer(touch.position);
                break;

            case TouchPhase.Ended:
            case TouchPhase.Canceled:
                EndPointer();
                break;
        }
    }

    private void HandleMouse()
    {
        if (Input.GetMouseButtonDown(0))
            TryBeginPointer(Input.mousePosition, InvalidPointerId);
        else if (Input.GetMouseButton(0))
            UpdatePointer(Input.mousePosition);
        else if (Input.GetMouseButtonUp(0))
            EndPointer();
    }

    private void TryBeginPointer(Vector2 screenPosition, int pointerId)
    {
        if (_ignoreUI && IsPointerOverUI(pointerId))
            return;

        _pointerDown = true;
        _dragging = false;
        _activePointerId = pointerId;
        _pointerDownScreen = screenPosition;
    }

    private void UpdatePointer(Vector2 screenPosition)
    {
        if (!_pointerDown)
            return;

        if (!_dragging)
        {
            if (Vector2.Distance(screenPosition, _pointerDownScreen) < _dragThresholdPixels)
                return;

            _dragging = true;
            _lastScreenPosition = screenPosition;
            return;
        }

        if ((screenPosition - _lastScreenPosition).sqrMagnitude < 0.01f)
            return;

        if (!TryGetGroundPoint(_lastScreenPosition, out Vector3 previousWorld))
            return;

        if (!TryGetGroundPoint(screenPosition, out Vector3 currentWorld))
            return;

        Vector3 target = _panTarget.position + (previousWorld - currentWorld);

        if (_useBounds)
        {
            target.x = Mathf.Clamp(target.x, _minXZ.x, _maxXZ.x);
            target.z = Mathf.Clamp(target.z, _minXZ.y, _maxXZ.y);
        }

        _panTarget.position = target;
        _lastScreenPosition = screenPosition;
    }

    public void SetGroundHeight(float groundHeight)
    {
        _groundHeight = groundHeight;
        _groundPlane = new Plane(Vector3.up, new Vector3(0f, _groundHeight, 0f));
    }

    public void SetPanTargetHeight(float height)
    {
        if (_panTarget == null)
            _panTarget = transform;

        Vector3 position = _panTarget.position;
        position.y = height;
        _panTarget.position = position;
        SetGroundHeight(height);
    }

    public void SetPanTargetHeightOnly(float height)
    {
        if (_panTarget == null)
            _panTarget = transform;

        Vector3 position = _panTarget.position;
        position.y = height;
        _panTarget.position = position;
    }

    public void SetPanTargetPosition(Vector3 position)
    {
        if (_panTarget == null)
            _panTarget = transform;

        if (_useBounds)
        {
            position.x = Mathf.Clamp(position.x, _minXZ.x, _maxXZ.x);
            position.z = Mathf.Clamp(position.z, _minXZ.y, _maxXZ.y);
        }

        _panTarget.position = position;
        SetGroundHeight(position.y);
    }

    public void SetPanTargetPositionOnly(Vector3 position)
    {
        if (_panTarget == null)
            _panTarget = transform;

        _panTarget.position = position;
    }

    private void EndPointer()
    {
        if (_pointerDown && !_dragging)
            TryHandleTableTap(_pointerDownScreen, _activePointerId);

        _pointerDown = false;
        _dragging = false;
        _activePointerId = InvalidPointerId;
    }

    private void TryHandleTableTap(Vector2 screenPosition, int pointerId)
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsBusiness)
            return;

        if (_ignoreUI && IsPointerOverUI(pointerId))
            return;

        if (_camera == null)
            return;

        Ray ray = _camera.ScreenPointToRay(screenPosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;

        DiningTable table = hit.collider.GetComponentInParent<DiningTable>();

        if (table != null)
            table.NotifyClicked();
    }

    private bool TryGetGroundPoint(Vector2 screenPosition, out Vector3 worldPoint)
    {
        Ray ray = _camera.ScreenPointToRay(screenPosition);

        if (_groundPlane.Raycast(ray, out float distance))
        {
            worldPoint = ray.GetPoint(distance);
            return true;
        }

        worldPoint = default;
        return false;
    }

    private static bool IsPointerOverUI(int pointerId)
    {
        if (EventSystem.current == null)
            return false;

        return pointerId == InvalidPointerId
            ? EventSystem.current.IsPointerOverGameObject()
            : EventSystem.current.IsPointerOverGameObject(pointerId);
    }

    private void OnDrawGizmosSelected()
    {
        if (!_drawBoundsGizmo || !_useBounds)
            return;

        Vector3 center = new(
            (_minXZ.x + _maxXZ.x) * 0.5f,
            _groundHeight,
            (_minXZ.y + _maxXZ.y) * 0.5f);

        Vector3 size = new(
            Mathf.Max(0.01f, _maxXZ.x - _minXZ.x),
            0.05f,
            Mathf.Max(0.01f, _maxXZ.y - _minXZ.y));

        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.85f);
        Gizmos.DrawWireCube(center, size);
    }

    public Vector2 PanMinXZ => _minXZ;
    public Vector2 PanMaxXZ => _maxXZ;
    public bool UsePanBounds => _useBounds;
    public float GroundHeight => _groundHeight;
}
