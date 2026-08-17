using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SfxId
{
    None = 0,
    UiClick = 1,
    OpenBusiness = 2,
    GoldCollect = 3,
    Building = 4,
    BuildComplete = 5,
    GoldMinus = 6,
    KickWorker = 7,
    WorkerResting = 8,
    TableUpgrade = 9,
    VipArrival = 11,
    Happy = 12,
    PranksterBreakTable = 13,
    PranksterLaugh = 14,
    Unhappy = 16,
    Alert = 17,
    Mission = 18
}

public enum BgmId
{
    None = 0,
    Main,
    Sleeping,
    ChefCooking,
    Future,
    Performer
}

[Serializable]
public struct SfxClipBinding
{
    public SfxId Sfx;
    public AudioClip Clip;

    [Range(0f, 1f)]
    public float Volume;
}

[Serializable]
public struct BgmClipBinding
{
    public BgmId Bgm;
    public AudioClip Clip;

    [Range(0f, 1f)]
    public float Volume;
}

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("SFX Clips")]
    [SerializeField] private SfxClipBinding[] _sfxClips;

    [Header("BGM Clips")]
    [SerializeField] private BgmClipBinding[] _bgmClips;
    [SerializeField] private BgmId _defaultBgm = BgmId.Main;
    [SerializeField] private float _bgmCrossfadeDuration = 1.5f;

    [Header("Volume")]
    [SerializeField] [Range(0f, 1f)] private float _masterVolume = 1f;

    private readonly Dictionary<SfxId, SfxClipBinding> _sfxById = new();
    private readonly Dictionary<BgmId, BgmClipBinding> _bgmById = new();
    private AudioSource _sfxSource;
    private AudioSource _bgmSource;
    private AudioSource _bgmFadeSource;
    private AudioSource _loopSfxSource;
    private BgmId _currentBgm = BgmId.None;
    private SfxId _currentLoopSfx = SfxId.None;
    private Coroutine _bgmFadeRoutine;

    public float MasterVolume
    {
        get => _masterVolume;
        set
        {
            _masterVolume = Mathf.Clamp01(value);
            ApplyBgmVolume();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (transform.parent != null)
            transform.SetParent(null, worldPositionStays: false);

        DontDestroyOnLoad(gameObject);

        EnsureAudioSources();
        RebuildClipLookups();

        BgmId startBgm = ResolveBgmForActiveScene();
        if (startBgm == BgmId.None)
            startBgm = _defaultBgm;

        if (startBgm != BgmId.None)
            PlayBgmInternal(startBgm);
    }

    private void OnDestroy()
    {
        StopBgmFadeRoutine();

        if (Instance == this)
            Instance = null;
    }

    public static void Play(SfxId sfx, float volumeMultiplier = 1f)
    {
        if (Instance == null)
            return;

        Instance.PlaySfx(sfx, volumeMultiplier);
    }

    public static void PlayOn(AudioSource source, SfxId sfx, float volumeMultiplier = 1f)
    {
        if (Instance == null)
            return;

        Instance.PlaySfxOnSource(source, sfx, volumeMultiplier);
    }

    public static void PlayLooping(SfxId sfx, float volumeMultiplier = 1f)
    {
        if (Instance == null)
            return;

        Instance.PlayLoopingInternal(sfx, volumeMultiplier);
    }

    public static void StopLooping(SfxId sfx = SfxId.None)
    {
        if (Instance == null)
            return;

        Instance.StopLoopingInternal(sfx);
    }

    public static void PlayBgm(BgmId bgm)
    {
        if (Instance == null)
            return;

        Instance.PlayBgmInternal(bgm);
    }

    public static void PlayBgmOn(AudioSource source, BgmId bgm)
    {
        if (Instance == null)
            return;

        Instance.PlayBgmOnSource(source, bgm);
    }

    public static void CrossfadeBgm(BgmId bgm, float duration = -1f)
    {
        if (Instance == null)
            return;

        Instance.CrossfadeBgmInternal(bgm, duration);
    }

    public static void CrossfadeBgmForScene(string sceneName, float duration = -1f)
    {
        if (Instance == null)
            return;

        BgmId bgm = ResolveBgmForScene(sceneName);
        if (bgm == BgmId.None)
            return;

        Instance.CrossfadeBgmInternal(bgm, duration);
    }

    public static void StopBgm()
    {
        if (Instance == null)
            return;

        Instance.StopBgmInternal();
    }

    public static void StopSource(AudioSource source)
    {
        if (source == null || !source.isPlaying)
            return;

        source.Stop();
        source.clip = null;
    }

    public void PlaySfx(SfxId sfx, float volumeMultiplier = 1f)
    {
        PlaySfxOnSource(_sfxSource, sfx, volumeMultiplier);
    }

    public void PlaySfxOnSource(AudioSource source, SfxId sfx, float volumeMultiplier = 1f)
    {
        if (source == null)
            return;

        if (sfx == SfxId.None || !_sfxById.TryGetValue(sfx, out SfxClipBinding binding) || binding.Clip == null)
            return;

        float clipVolume = binding.Volume > 0f ? binding.Volume : 1f;
        float volume = Mathf.Clamp01(_masterVolume * clipVolume * volumeMultiplier);
        source.PlayOneShot(binding.Clip, volume);
    }

    public void PlayLoopingInternal(SfxId sfx, float volumeMultiplier = 1f)
    {
        EnsureAudioSources();

        if (_loopSfxSource == null)
            return;

        if (sfx == SfxId.None || !_sfxById.TryGetValue(sfx, out SfxClipBinding binding) || binding.Clip == null)
            return;

        if (_currentLoopSfx == sfx && _loopSfxSource.isPlaying && _loopSfxSource.clip == binding.Clip)
            return;

        float clipVolume = binding.Volume > 0f ? binding.Volume : 1f;
        _currentLoopSfx = sfx;
        _loopSfxSource.clip = binding.Clip;
        _loopSfxSource.loop = true;
        _loopSfxSource.volume = Mathf.Clamp01(_masterVolume * clipVolume * volumeMultiplier);
        _loopSfxSource.Play();
    }

    public void StopLoopingInternal(SfxId sfx)
    {
        if (_loopSfxSource == null)
            return;

        if (sfx != SfxId.None && _currentLoopSfx != sfx)
            return;

        _loopSfxSource.Stop();
        _loopSfxSource.clip = null;
        _currentLoopSfx = SfxId.None;
    }

    public void PlayBgmInternal(BgmId bgm)
    {
        EnsureAudioSources();
        StopBgmFadeRoutine();
        PromoteFadeSourceIfPlaying();

        if (bgm == BgmId.None || !_bgmById.TryGetValue(bgm, out BgmClipBinding binding) || binding.Clip == null)
            return;

        if (_currentBgm == bgm && _bgmSource.isPlaying && _bgmSource.clip == binding.Clip)
        {
            ApplyBgmVolume();
            return;
        }

        StopFadeSource();
        _currentBgm = bgm;
        _bgmSource.clip = binding.Clip;
        ApplyBgmVolume();
        _bgmSource.loop = true;
        _bgmSource.Play();
    }

    public void PlayBgmOnSource(AudioSource source, BgmId bgm)
    {
        if (source == null)
            return;

        if (bgm == BgmId.None || !_bgmById.TryGetValue(bgm, out BgmClipBinding binding) || binding.Clip == null)
            return;

        float clipVolume = binding.Volume > 0f ? binding.Volume : 1f;
        source.clip = binding.Clip;
        source.loop = true;
        source.volume = Mathf.Clamp01(_masterVolume * clipVolume);
        source.Play();
    }

    public void CrossfadeBgmInternal(BgmId bgm, float duration)
    {
        EnsureAudioSources();

        if (bgm == BgmId.None || !_bgmById.TryGetValue(bgm, out BgmClipBinding binding) || binding.Clip == null)
            return;

        PromoteFadeSourceIfPlaying();

        if (_currentBgm == bgm && _bgmSource != null && _bgmSource.isPlaying && _bgmSource.clip == binding.Clip)
        {
            ApplyBgmVolume();
            return;
        }

        float fadeDuration = duration >= 0f ? duration : _bgmCrossfadeDuration;
        if (fadeDuration <= 0.01f || _bgmSource == null || !_bgmSource.isPlaying)
        {
            PlayBgmInternal(bgm);
            return;
        }

        StopBgmFadeRoutine();
        _bgmFadeRoutine = StartCoroutine(CrossfadeBgmRoutine(bgm, binding, fadeDuration));
    }

    public void StopBgmInternal()
    {
        StopBgmFadeRoutine();
        StopFadeSource();
        _currentBgm = BgmId.None;

        if (_bgmSource == null)
            return;

        _bgmSource.Stop();
        _bgmSource.clip = null;
    }

    private IEnumerator CrossfadeBgmRoutine(BgmId bgm, BgmClipBinding binding, float duration)
    {
        float targetVolume = GetClipVolume(binding);
        float fromStartVolume = _bgmSource.isPlaying ? _bgmSource.volume : 0f;

        _bgmFadeSource.Stop();
        _bgmFadeSource.clip = binding.Clip;
        _bgmFadeSource.loop = true;
        _bgmFadeSource.volume = 0f;
        _bgmFadeSource.Play();

        _currentBgm = bgm;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _bgmSource.volume = Mathf.Lerp(fromStartVolume, 0f, t);
            _bgmFadeSource.volume = Mathf.Lerp(0f, targetVolume, t);
            yield return null;
        }

        _bgmSource.Stop();
        _bgmSource.clip = null;
        _bgmSource.volume = 0f;
        _bgmFadeSource.volume = targetVolume;

        AudioSource finished = _bgmSource;
        _bgmSource = _bgmFadeSource;
        _bgmFadeSource = finished;
        _bgmFadeRoutine = null;
    }

    private void ApplyBgmVolume()
    {
        if (_bgmSource == null || _bgmFadeRoutine != null)
            return;

        float clipVolume = 1f;

        if (_currentBgm != BgmId.None && _bgmById.TryGetValue(_currentBgm, out BgmClipBinding binding))
            clipVolume = binding.Volume > 0f ? binding.Volume : 1f;

        _bgmSource.volume = Mathf.Clamp01(_masterVolume * clipVolume);
    }

    private float GetClipVolume(BgmClipBinding binding)
    {
        float clipVolume = binding.Volume > 0f ? binding.Volume : 1f;
        return Mathf.Clamp01(_masterVolume * clipVolume);
    }

    private static BgmId ResolveBgmForActiveScene()
    {
        return ResolveBgmForScene(SceneManager.GetActiveScene().name);
    }

    private static BgmId ResolveBgmForScene(string sceneName)
    {
        if (string.Equals(sceneName, RestaurantSceneMode.FutureSceneName, StringComparison.Ordinal))
            return BgmId.Future;

        if (string.Equals(sceneName, RestaurantSceneMode.MainSceneName, StringComparison.Ordinal))
            return BgmId.Main;

        return BgmId.None;
    }

    private void StopBgmFadeRoutine()
    {
        if (_bgmFadeRoutine == null)
            return;

        StopCoroutine(_bgmFadeRoutine);
        _bgmFadeRoutine = null;
    }

    private void PromoteFadeSourceIfPlaying()
    {
        if (_bgmFadeSource == null || !_bgmFadeSource.isPlaying)
            return;

        if (_bgmSource != null && _bgmSource.isPlaying && _bgmSource.volume >= _bgmFadeSource.volume)
            return;

        AudioSource previous = _bgmSource;
        _bgmSource = _bgmFadeSource;
        _bgmFadeSource = previous;
        StopFadeSource();
    }

    private void StopFadeSource()
    {
        if (_bgmFadeSource == null)
            return;

        _bgmFadeSource.Stop();
        _bgmFadeSource.clip = null;
        _bgmFadeSource.volume = 0f;
    }

    private void EnsureAudioSources()
    {
        if (_sfxSource == null || _bgmSource == null || _loopSfxSource == null || _bgmFadeSource == null)
        {
            AudioSource[] sources = GetComponents<AudioSource>();

            if (_sfxSource == null)
                _sfxSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();

            if (_bgmSource == null)
                _bgmSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();

            if (_loopSfxSource == null)
                _loopSfxSource = sources.Length > 2 ? sources[2] : gameObject.AddComponent<AudioSource>();

            if (_bgmFadeSource == null)
                _bgmFadeSource = sources.Length > 3 ? sources[3] : gameObject.AddComponent<AudioSource>();
        }

        _sfxSource.playOnAwake = false;
        _sfxSource.loop = false;
        _bgmSource.playOnAwake = false;
        _bgmSource.loop = true;
        _loopSfxSource.playOnAwake = false;
        _loopSfxSource.loop = true;
        _bgmFadeSource.playOnAwake = false;
        _bgmFadeSource.loop = true;
    }

    private void RebuildClipLookups()
    {
        _sfxById.Clear();
        _bgmById.Clear();

        if (_sfxClips != null)
        {
            for (int i = 0; i < _sfxClips.Length; i++)
            {
                SfxClipBinding binding = _sfxClips[i];

                if (binding.Sfx == SfxId.None)
                    continue;

                _sfxById[binding.Sfx] = binding;
            }
        }

        if (_bgmClips != null)
        {
            for (int i = 0; i < _bgmClips.Length; i++)
            {
                BgmClipBinding binding = _bgmClips[i];

                if (binding.Bgm == BgmId.None)
                    continue;

                _bgmById[binding.Bgm] = binding;
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying && Instance == this)
        {
            RebuildClipLookups();
            ApplyBgmVolume();
        }
    }
#endif
}
