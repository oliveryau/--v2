using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// World Portal + Check/Enter buttons + Waiter Dialogue.
/// Lull start: small portal + Check button. Check tap grows portal, then Enter appears.
/// </summary>
[DisallowMultipleComponent]
public class PortalUiController : MonoBehaviour
{
    private const string PortalObjectName = "Portal";
    private const string EnterPortalButtonName = "Enter Portal Button";
    private const string CheckPortalButtonName = "Check Portal Button";
    private const string PortalParticleName = "Portal Particle";
    private const string WaiterDialogueName = "Waiter Dialogue";
    private const string ScreenCanvasName = "UI Canvas";
    private const string PortalPointName = "Portal Point";

    private enum PresentationPhase
    {
        Hidden = 0,
        Checking = 1,
        Growing = 2,
        Ready = 3
    }

    [SerializeField] private Transform _anchor;
    [SerializeField] private Vector3 _worldOffset;
    [SerializeField] private RectTransform _enterPortalButton;
    [SerializeField] private RectTransform _checkPortalButton;
    [SerializeField] private ParticleSystem _portalParticle;
    [Tooltip("Screen-pixel offset applied after projecting the portal to the UI canvas (Y up).")]
    [SerializeField] private Vector2 _portalButtonScreenOffset = new Vector2(0f, 80f);
    [SerializeField] private Vector2 _initialStartSizeRange = new Vector2(0.9f, 1f);
    [SerializeField] private Vector2 _expandedStartSizeRange = new Vector2(1.9f, 2f);
    [SerializeField] private float _growDurationSeconds = 1.25f;
    [Tooltip("Wait after lull start before showing the portal and Check button.")]
    [SerializeField] private float _appearDelaySeconds = 1f;
    [Tooltip("Extra wait after the grow finishes before showing Enter Portal.")]
    [SerializeField] private float _enterButtonDelayAfterGrowSeconds = 1f;
    [Tooltip("Camera position used when the portal lull presentation starts (ground floor view).")]
    [SerializeField] private Vector3 _presentationCameraPosition = new Vector3(-3f, 0f, 0.5f);
    [SerializeField] private float _portalButtonPulseMinScale = 0.97f;
    [SerializeField] private float _portalButtonPulseMaxScale = 1.03f;
    [SerializeField] private float _portalButtonPulseSpeed = 3f;
    [SerializeField] private GameObject _waiterDialogueRoot;
    [SerializeField] private Canvas _screenCanvas;
    [SerializeField] private Camera _worldCamera;

    private RectTransform _screenCanvasRect;
    private Button _enterPortalButtonComponent;
    private Button _checkPortalButtonComponent;
    private PresentationPhase _phase = PresentationPhase.Hidden;
    private bool _enterButtonWired;
    private bool _checkButtonWired;
    private Coroutine _growRoutine;
    private static Coroutine _delayedPresentRoutine;
    private static MonoBehaviour _delayedPresentHost;

    public static void PresentForLull()
    {
        if (!RestaurantSceneMode.IsMainScene)
            return;

        PortalUiController controller = FindControllerIncludingInactive();
        if (controller == null)
            return;

        StopDelayedPresent();

        // Keep the portal object fully inactive during the countdown so play-on-awake
        // particles cannot appear before Check Portal Button.
        if (controller.gameObject.activeSelf)
            controller.HidePresentation();

        MonoBehaviour host = ResolvePresentHost();
        float delay = Mathf.Max(0f, controller._appearDelaySeconds);

        if (host == null || delay <= 0f)
        {
            ShowPresentationImmediate(controller);
            return;
        }

        _delayedPresentHost = host;
        _delayedPresentRoutine = host.StartCoroutine(DelayedPresentCoroutine(controller, delay));
    }

    public static void EnsureHidden()
    {
        StopDelayedPresent();

        PortalUiController controller = FindControllerIncludingInactive();
        if (controller == null)
            return;

        controller.HidePresentation();
    }

    private static PortalUiController FindControllerIncludingInactive()
    {
        PortalUiController[] controllers =
            FindObjectsByType<PortalUiController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null)
                return controllers[i];
        }

        GameObject portalObject = FindSceneObjectByNameIncludingInactive(PortalObjectName);
        return portalObject != null ? portalObject.GetComponent<PortalUiController>() : null;
    }

    private void Awake()
    {
        ResolveAll();
        WireButtons();

        if (_phase == PresentationPhase.Hidden)
            HidePresentationUiOnly();
    }

    private void OnEnable()
    {
        ResolveAll();
        WireButtons();
        SyncWorldPortalPosition();

        if (_phase == PresentationPhase.Hidden)
        {
            HidePresentationUiOnly();
            StopPortalParticle();
        }
        else
        {
            SyncActiveButtons();
        }
    }

    private void OnDisable()
    {
        StopGrowRoutine();
        HidePresentationUiOnly();
        _phase = PresentationPhase.Hidden;
    }

    private void LateUpdate()
    {
        if (_phase == PresentationPhase.Hidden)
            return;

        SyncWorldPortalPosition();
        SyncActiveButtons();
    }

    private static IEnumerator DelayedPresentCoroutine(PortalUiController controller, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        _delayedPresentRoutine = null;
        _delayedPresentHost = null;

        if (controller == null)
            yield break;

        ShowPresentationImmediate(controller);
    }

    private static void ShowPresentationImmediate(PortalUiController controller)
    {
        if (controller == null)
            return;

        if (!controller.gameObject.activeSelf)
            controller.gameObject.SetActive(true);

        controller.PresentPresentationVisible();
    }

    private static MonoBehaviour ResolvePresentHost()
    {
        if (CustomerManager.Instance != null && CustomerManager.Instance.isActiveAndEnabled)
            return CustomerManager.Instance;

        if (UIManager.Instance != null && UIManager.Instance.isActiveAndEnabled)
            return UIManager.Instance;

        return null;
    }

    private static void StopDelayedPresent()
    {
        if (_delayedPresentHost != null && _delayedPresentRoutine != null)
            _delayedPresentHost.StopCoroutine(_delayedPresentRoutine);

        _delayedPresentRoutine = null;
        _delayedPresentHost = null;
    }

    private void PresentPresentationVisible()
    {
        StopGrowRoutine();
        ResolveAll();
        WireButtons();
        SyncWorldPortalPosition();

        ApplyParticleStartSizeRange(_initialStartSizeRange.x, _initialStartSizeRange.y, restart: true);

        _phase = PresentationPhase.Checking;
        SetEnterPortalButtonActive(false);
        SetCheckPortalButtonActive(true);
        SetWaiterDialogueActive(true);
        SyncActiveButtons();
        CharacterPanelController.Instance?.FocusCameraAt(_presentationCameraPosition, ensureGroundFloor: true);
    }

    private void HidePresentation()
    {
        StopGrowRoutine();
        _phase = PresentationPhase.Hidden;
        HidePresentationUiOnly();

        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    private void HidePresentationUiOnly()
    {
        SetEnterPortalButtonActive(false);
        SetCheckPortalButtonActive(false);
        SetWaiterDialogueActive(false);
        ResetPortalButtonScales();
    }

    private void WireButtons()
    {
        ResolveEnterPortalButton();
        ResolveCheckPortalButton();

        if (_enterPortalButton != null && !_enterButtonWired)
        {
            _enterPortalButtonComponent = _enterPortalButton.GetComponent<Button>()
                ?? _enterPortalButton.gameObject.AddComponent<Button>();
            _enterPortalButtonComponent.onClick.RemoveListener(HandleEnterPortalButtonClicked);
            _enterPortalButtonComponent.onClick.AddListener(HandleEnterPortalButtonClicked);
            _enterButtonWired = true;
        }

        if (_checkPortalButton != null && !_checkButtonWired)
        {
            _checkPortalButtonComponent = _checkPortalButton.GetComponent<Button>()
                ?? _checkPortalButton.gameObject.AddComponent<Button>();
            _checkPortalButtonComponent.onClick.RemoveListener(HandleCheckPortalButtonClicked);
            _checkPortalButtonComponent.onClick.AddListener(HandleCheckPortalButtonClicked);
            _checkButtonWired = true;
        }
    }

    private void HandleCheckPortalButtonClicked()
    {
        if (_phase != PresentationPhase.Checking)
            return;

        SetCheckPortalButtonActive(false);
        StopGrowRoutine();
        _growRoutine = StartCoroutine(GrowPortalParticleRoutine());
    }

    private void HandleEnterPortalButtonClicked()
    {
        if (_phase != PresentationPhase.Ready)
            return;

        HidePresentation();
        SceneManager.LoadScene(RestaurantSceneMode.FutureSceneName);
    }

    private IEnumerator GrowPortalParticleRoutine()
    {
        _phase = PresentationPhase.Growing;

        float duration = Mathf.Max(0.01f, _growDurationSeconds);
        float startMin = _initialStartSizeRange.x;
        float startMax = Mathf.Max(startMin, _initialStartSizeRange.y);
        float endMin = _expandedStartSizeRange.x;
        float endMax = Mathf.Max(endMin, _expandedStartSizeRange.y);
        float elapsed = 0f;

        ApplyParticleStartSizeRange(startMin, startMax, restart: false);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothed = t * t * (3f - 2f * t);
            float minSize = Mathf.Lerp(startMin, endMin, smoothed);
            float maxSize = Mathf.Lerp(startMax, endMax, smoothed);
            ApplyParticleStartSizeRange(minSize, maxSize, restart: false);
            yield return null;
        }

        ApplyParticleStartSizeRange(endMin, endMax, restart: false);

        float enterDelay = Mathf.Max(0f, _enterButtonDelayAfterGrowSeconds);
        if (enterDelay > 0f)
            yield return new WaitForSeconds(enterDelay);

        _growRoutine = null;
        _phase = PresentationPhase.Ready;
        SetEnterPortalButtonActive(true);
        SyncActiveButtons();
    }

    private void StopGrowRoutine()
    {
        if (_growRoutine == null)
            return;

        StopCoroutine(_growRoutine);
        _growRoutine = null;
    }

    private void StopPortalParticle()
    {
        ResolvePortalParticle();
        if (_portalParticle == null)
            return;

        _portalParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void ApplyParticleStartSizeRange(float minSize, float maxSize, bool restart)
    {
        ResolvePortalParticle();
        if (_portalParticle == null)
            return;

        minSize = Mathf.Max(0f, minSize);
        maxSize = Mathf.Max(minSize, maxSize);

        ParticleSystem.MainModule main = _portalParticle.main;
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);

        if (!restart)
            return;

        _portalParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _portalParticle.Play(true);
    }

    private void ResolveAll()
    {
        ResolveAnchor();
        ResolveEnterPortalButton();
        ResolveCheckPortalButton();
        ResolvePortalParticle();
        ResolveWaiterDialogue();
        ResolveScreenCanvas();
        ResolveWorldCamera();
    }

    private void ResolveAnchor()
    {
        if (_anchor == null)
        {
            GameObject portalPoint = FindSceneObjectByNameIncludingInactive(PortalPointName);
            if (portalPoint != null)
                _anchor = portalPoint.transform;
        }
    }

    private void ResolveEnterPortalButton()
    {
        if (_enterPortalButton != null)
            return;

        GameObject buttonObject = FindSceneObjectByNameIncludingInactive(EnterPortalButtonName);
        if (buttonObject != null)
            _enterPortalButton = buttonObject.transform as RectTransform;
    }

    private void ResolveCheckPortalButton()
    {
        if (_checkPortalButton != null)
            return;

        GameObject buttonObject = FindSceneObjectByNameIncludingInactive(CheckPortalButtonName);
        if (buttonObject != null)
            _checkPortalButton = buttonObject.transform as RectTransform;
    }

    private void ResolvePortalParticle()
    {
        if (_portalParticle != null)
            return;

        ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];
            if (particle == null)
                continue;

            if (string.Equals(particle.name, PortalParticleName, System.StringComparison.OrdinalIgnoreCase)
                || particles.Length == 1)
            {
                _portalParticle = particle;
                return;
            }
        }

        if (particles.Length > 0)
            _portalParticle = particles[0];
    }

    private void ResolveWaiterDialogue()
    {
        if (_waiterDialogueRoot != null)
            return;

        _waiterDialogueRoot = FindSceneObjectByNameIncludingInactive(WaiterDialogueName);
    }

    private void ResolveScreenCanvas()
    {
        if (_screenCanvas == null)
        {
            RectTransform button = _enterPortalButton != null ? _enterPortalButton : _checkPortalButton;
            if (button != null)
                _screenCanvas = button.GetComponentInParent<Canvas>();
        }

        if (_screenCanvas == null)
        {
            GameObject canvasObject = GameObject.Find(ScreenCanvasName);
            if (canvasObject != null)
                _screenCanvas = canvasObject.GetComponent<Canvas>();
        }

        if (_screenCanvas != null)
            _screenCanvasRect = _screenCanvas.transform as RectTransform;
    }

    private void ResolveWorldCamera()
    {
        if (_worldCamera == null)
            _worldCamera = Camera.main;
    }

    private Transform ResolveWorldTarget()
    {
        if (_anchor != null)
            return _anchor;

        return null;
    }

    private void SyncWorldPortalPosition()
    {
        Transform target = ResolveWorldTarget();
        if (target == null)
            return;

        ResolvePortalParticle();
        if (_portalParticle == null)
            return;

        _portalParticle.transform.position = target.position + _worldOffset;
    }

    private void SyncActiveButtons()
    {
        if (_phase == PresentationPhase.Checking)
        {
            SyncButtonToPortal(_checkPortalButton, visible: true);
            SetEnterPortalButtonActive(false);
            return;
        }

        if (_phase == PresentationPhase.Ready)
        {
            SyncButtonToPortal(_enterPortalButton, visible: true);
            SetCheckPortalButtonActive(false);
            return;
        }

        // Growing / Hidden: no interactive portal buttons.
        SetCheckPortalButtonActive(false);
        if (_phase != PresentationPhase.Ready)
            SetEnterPortalButtonActive(false);
    }

    private void SyncButtonToPortal(RectTransform button, bool visible)
    {
        if (button == null)
            return;

        if (!visible || _phase == PresentationPhase.Hidden || !isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            if (button.gameObject.activeSelf)
                button.gameObject.SetActive(false);
            button.localScale = Vector3.one;
            return;
        }

        if (!button.gameObject.activeSelf)
            button.gameObject.SetActive(true);

        button.localScale = Vector3.one * GetPortalButtonPulseScale();

        Transform target = ResolveWorldTarget();
        if (target == null)
            return;

        Vector3 worldPosition = target.position + _worldOffset;

        if (UIManager.Instance != null
            && UIManager.Instance.TryGetEdgeClampedScreenUiLocalPoint(
                worldPosition,
                button,
                _portalButtonScreenOffset,
                out Vector2 clampedLocalPoint))
        {
            button.anchoredPosition = clampedLocalPoint;
            return;
        }

        if (_worldCamera == null)
            ResolveWorldCamera();

        if (_screenCanvasRect == null)
            ResolveScreenCanvas();

        if (_worldCamera == null || _screenCanvasRect == null)
            return;

        Vector3 screenPoint = _worldCamera.WorldToScreenPoint(worldPosition);

        if (screenPoint.z <= 0f)
        {
            if (button.gameObject.activeSelf)
                button.gameObject.SetActive(false);
            button.localScale = Vector3.one;
            return;
        }

        screenPoint.x += _portalButtonScreenOffset.x;
        screenPoint.y += _portalButtonScreenOffset.y;

        Camera canvasCamera = _screenCanvas != null && _screenCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _screenCanvas.worldCamera
            : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _screenCanvasRect,
                screenPoint,
                canvasCamera,
                out Vector2 localPoint))
        {
            return;
        }

        button.anchoredPosition = localPoint;
    }

    private void SetEnterPortalButtonActive(bool active)
    {
        if (_enterPortalButton == null)
            ResolveEnterPortalButton();

        if (_enterPortalButton == null)
            return;

        if (_enterPortalButton.gameObject.activeSelf != active)
            _enterPortalButton.gameObject.SetActive(active);

        if (!active)
            _enterPortalButton.localScale = Vector3.one;
    }

    private void SetCheckPortalButtonActive(bool active)
    {
        if (_checkPortalButton == null)
            ResolveCheckPortalButton();

        if (_checkPortalButton == null)
            return;

        if (_checkPortalButton.gameObject.activeSelf != active)
            _checkPortalButton.gameObject.SetActive(active);

        if (!active)
            _checkPortalButton.localScale = Vector3.one;
    }

    private void ResetPortalButtonScales()
    {
        if (_enterPortalButton != null)
            _enterPortalButton.localScale = Vector3.one;

        if (_checkPortalButton != null)
            _checkPortalButton.localScale = Vector3.one;
    }

    private float GetPortalButtonPulseScale()
    {
        float speed = _portalButtonPulseSpeed;
        if (speed <= 0f)
            return 1f;

        float pulseT = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;
        return Mathf.Lerp(_portalButtonPulseMinScale, _portalButtonPulseMaxScale, pulseT);
    }

    private void SetWaiterDialogueActive(bool active)
    {
        if (_waiterDialogueRoot == null)
            ResolveWaiterDialogue();

        if (_waiterDialogueRoot == null)
            return;

        if (_waiterDialogueRoot.activeSelf != active)
            _waiterDialogueRoot.SetActive(active);
    }

    private static GameObject FindSceneObjectByNameIncludingInactive(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

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
}
