using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PortalTransitionController : MonoBehaviour
{
    private const string ObjectName = "Portal Transition";
    private const string OverlayCanvasName = "Portal Transition Canvas";
    private const string ScreenCanvasName = "UI Canvas";
    private const string TransitionInStateName = "Transition In";
    private const string TransitionOutStateName = "Transition Out";
    private const float TransitionClipDuration = 1.75f;
    private const string MainSceneYearText = "1810\u5e74";
    private const string FutureSceneYearText = "2026\u5e74";

    private static readonly int TransitionInHash = Animator.StringToHash(TransitionInStateName);
    private static readonly int TransitionOutHash = Animator.StringToHash(TransitionOutStateName);

    public static PortalTransitionController Instance { get; private set; }

    [SerializeField] private Animator _animator;
    [Tooltip("Year label on the transition overlay: 1810 in the Main scene, 2026 in the portal scene.")]
    [SerializeField] private TextMeshProUGUI _yearText;

    private GameObject _overlayCanvasObject;
    private bool _isTransitioning;
    private Coroutine _leaveRoutine;

    public static bool IsTransitioning => Instance != null && Instance._isTransitioning;

    public static void EnsureReady()
    {
        if (Instance != null)
            return;

        PortalTransitionController existing = FindExistingIncludingInactive();
        if (existing != null)
        {
            existing.BecomePersistent();
            return;
        }

        GameObject root = FindNamedObjectIncludingInactive(ObjectName);
        if (root == null)
            return;

        PortalTransitionController controller = root.GetComponent<PortalTransitionController>()
            ?? root.AddComponent<PortalTransitionController>();
        controller.BecomePersistent();
    }

    public static void PlayLeaveThenLoad(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;

        EnsureReady();

        if (Instance == null)
        {
            SceneManager.LoadScene(sceneName);
            return;
        }

        Instance.BeginLeaveThenLoad(sceneName);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        BecomePersistent();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void BecomePersistent()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (_animator == null)
            _animator = GetComponent<Animator>();

        if (_animator != null)
        {
            _animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _animator.keepAnimatorStateOnDisable = true;
        }

        Image image = GetComponent<Image>();
        if (image != null)
            image.raycastTarget = true;

        ResolveYearText();
        RefreshYearText();
        EnsurePersistentOverlayCanvas();

        if (!_isTransitioning)
            SetOverlayVisible(false);
    }

    private void BeginLeaveThenLoad(string sceneName)
    {
        if (_isTransitioning)
            return;

        _isTransitioning = true;

        RefreshYearText();
        SetOverlayVisible(true);

        if (_leaveRoutine != null)
            StopCoroutine(_leaveRoutine);

        _leaveRoutine = StartCoroutine(LeaveThenLoadRoutine(sceneName));
    }

    private IEnumerator LeaveThenLoadRoutine(string sceneName)
    {
        yield return PlayState(TransitionInHash, TransitionClipDuration);

        if (_animator != null)
            _animator.speed = 0f;

        SceneManager.LoadScene(sceneName);

        // Wait until Unity finishes its scene-load temp allocator scope.
        // Doing animator / FindObjects work inside sceneLoaded (while LoadScene
        // is still on the stack) triggers ALLOC_TEMP_TLS warnings.
        yield return null;

        DestroySceneDuplicates();

        // Screen is fully covered here, so the year flips to the scene we just entered.
        RefreshYearText();
        SetOverlayVisible(true);

        if (_animator != null)
        {
            _animator.enabled = true;
            _animator.speed = 1f;
        }

        yield return PlayState(TransitionOutHash, TransitionClipDuration);

        SetOverlayVisible(false);
        _isTransitioning = false;
        _leaveRoutine = null;
    }

    private IEnumerator PlayState(int stateHash, float fallbackDuration)
    {
        if (_animator == null)
        {
            yield return new WaitForSecondsRealtime(fallbackDuration);
            yield break;
        }

        _animator.enabled = true;
        _animator.speed = 1f;
        _animator.Play(stateHash, 0, 0f);
        _animator.Update(0f);

        float duration = ResolveCurrentClipDuration(fallbackDuration);
        float elapsed = 0f;
        bool enteredState = false;

        while (elapsed < duration + 0.25f)
        {
            AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash == stateHash)
            {
                enteredState = true;
                if (info.normalizedTime >= 0.99f)
                    yield break;
            }
            else if (enteredState && elapsed > 0.05f)
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private float ResolveCurrentClipDuration(float fallbackDuration)
    {
        if (_animator == null)
            return fallbackDuration;

        AnimatorClipInfo[] clipInfo = _animator.GetCurrentAnimatorClipInfo(0);
        if (clipInfo != null && clipInfo.Length > 0 && clipInfo[0].clip != null)
            return Mathf.Max(0.01f, clipInfo[0].clip.length);

        return fallbackDuration;
    }

    private void ResolveYearText()
    {
        if (_yearText == null)
            _yearText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void RefreshYearText()
    {
        ResolveYearText();

        if (_yearText == null)
            return;

        if (RestaurantSceneMode.IsFutureScene)
            _yearText.text = FutureSceneYearText;
        else if (RestaurantSceneMode.IsMainScene)
            _yearText.text = MainSceneYearText;
    }

    private void SetOverlayVisible(bool visible)
    {
        if (_overlayCanvasObject != null && _overlayCanvasObject.activeSelf != visible)
            _overlayCanvasObject.SetActive(visible);

        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
    }

    private void EnsurePersistentOverlayCanvas()
    {
        if (_overlayCanvasObject == null && transform.parent != null
            && string.Equals(transform.parent.name, OverlayCanvasName, System.StringComparison.Ordinal))
        {
            _overlayCanvasObject = transform.parent.gameObject;
        }

        if (_overlayCanvasObject == null)
        {
            _overlayCanvasObject = new GameObject(OverlayCanvasName);
            Canvas canvas = _overlayCanvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32767;
            _overlayCanvasObject.AddComponent<GraphicRaycaster>();
            CopyCanvasScaler(_overlayCanvasObject);

            RectTransform overlayRect = transform as RectTransform;
            overlayRect.SetParent(_overlayCanvasObject.transform, false);
            overlayRect.anchorMin = new Vector2(0.5f, 0.5f);
            overlayRect.anchorMax = new Vector2(0.5f, 0.5f);
            overlayRect.pivot = new Vector2(0.5f, 0.5f);
            overlayRect.anchoredPosition = Vector2.zero;
        }

        DontDestroyOnLoad(_overlayCanvasObject);
    }

    private void CopyCanvasScaler(GameObject canvasObject)
    {
        CanvasScaler destination = canvasObject.GetComponent<CanvasScaler>();
        if (destination == null)
            destination = canvasObject.AddComponent<CanvasScaler>();

        Canvas sceneCanvas = FindScreenCanvas();
        CanvasScaler source = sceneCanvas != null ? sceneCanvas.GetComponent<CanvasScaler>() : null;

        if (source != null)
        {
            destination.uiScaleMode = source.uiScaleMode;
            destination.referenceResolution = source.referenceResolution;
            destination.screenMatchMode = source.screenMatchMode;
            destination.matchWidthOrHeight = source.matchWidthOrHeight;
            destination.referencePixelsPerUnit = source.referencePixelsPerUnit;
            return;
        }

        destination.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        destination.referenceResolution = new Vector2(1156f, 2510f);
        destination.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        destination.matchWidthOrHeight = 0.5f;
    }

    private void DestroySceneDuplicates()
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate == transform || candidate == (_overlayCanvasObject != null ? _overlayCanvasObject.transform : null))
                continue;

            if (string.Equals(candidate.name, ObjectName, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.name, OverlayCanvasName, System.StringComparison.OrdinalIgnoreCase))
            {
                Destroy(candidate.gameObject);
            }
        }
    }

    private static PortalTransitionController FindExistingIncludingInactive()
    {
        PortalTransitionController[] controllers =
            FindObjectsByType<PortalTransitionController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null)
                return controllers[i];
        }

        return null;
    }

    private static GameObject FindNamedObjectIncludingInactive(string objectName)
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null
                && string.Equals(candidate.name, objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                return candidate.gameObject;
            }
        }

        return null;
    }

    private static Canvas FindScreenCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
                continue;

            if (string.Equals(canvas.name, ScreenCanvasName, System.StringComparison.OrdinalIgnoreCase))
                return canvas;
        }

        return null;
    }
}
