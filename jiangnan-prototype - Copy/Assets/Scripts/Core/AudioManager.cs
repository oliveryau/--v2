using System;
using System.Collections.Generic;
using UnityEngine;

public enum SfxId
{
    None = 0,
    UiClick,
    OpenBusiness,
    GoldCollect,
    Building,
    BuildComplete,
    HireWorker,
    KickWorker,
    WorkerResting,
    TableUpgrade,
    TableRepair,
    VipArrival,
    Happy,
    PranksterBreakTable,
    PranksterLaugh,
    Carriage,
    Unhappy,
    Alert,
    MenuSignature
}

public enum BgmId
{
    None = 0,
    Main,
    Sleeping,
    ChefCooking
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

    [Header("Volume")]
    [SerializeField] [Range(0f, 1f)] private float _masterVolume = 1f;

    private readonly Dictionary<SfxId, SfxClipBinding> _sfxById = new();
    private readonly Dictionary<BgmId, BgmClipBinding> _bgmById = new();
    private AudioSource _sfxSource;
    private AudioSource _bgmSource;
    private BgmId _currentBgm = BgmId.None;

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

        if (_defaultBgm != BgmId.None)
            PlayBgmInternal(_defaultBgm);
    }

    private void OnDestroy()
    {
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

    public void PlayBgmInternal(BgmId bgm)
    {
        if (bgm == BgmId.None || !_bgmById.TryGetValue(bgm, out BgmClipBinding binding) || binding.Clip == null)
            return;

        if (_currentBgm == bgm && _bgmSource.isPlaying && _bgmSource.clip == binding.Clip)
        {
            ApplyBgmVolume();
            return;
        }

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

    public void StopBgmInternal()
    {
        _currentBgm = BgmId.None;
        _bgmSource.Stop();
        _bgmSource.clip = null;
    }

    private void ApplyBgmVolume()
    {
        if (_bgmSource == null)
            return;

        float clipVolume = 1f;

        if (_currentBgm != BgmId.None && _bgmById.TryGetValue(_currentBgm, out BgmClipBinding binding))
            clipVolume = binding.Volume > 0f ? binding.Volume : 1f;

        _bgmSource.volume = Mathf.Clamp01(_masterVolume * clipVolume);
    }

    private void EnsureAudioSources()
    {
        AudioSource[] sources = GetComponents<AudioSource>();

        if (sources.Length == 0)
        {
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _bgmSource = gameObject.AddComponent<AudioSource>();
        }
        else if (sources.Length == 1)
        {
            _sfxSource = sources[0];
            _bgmSource = gameObject.AddComponent<AudioSource>();
        }
        else
        {
            _sfxSource = sources[0];
            _bgmSource = sources[1];
        }

        _sfxSource.playOnAwake = false;
        _sfxSource.loop = false;
        _bgmSource.playOnAwake = false;
        _bgmSource.loop = true;
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
