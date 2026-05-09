using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Audio/Background Music Volume Control")]
public sealed class BackgroundMusicVolumeControl : MonoBehaviour
{
    [SerializeField] AudioSource _audioSource;
    [SerializeField] AudioClip _musicClip;
    [SerializeField, Range(0f, 1f)] float _volume = 0.35f;
    [SerializeField] bool _playOnStart = true;
    [SerializeField] bool _loop = true;

    public float volume => _volume;

    public void SetVolume(float value)
    {
        _volume = Mathf.Clamp01(value);
        ApplySettings();
    }

    [ContextMenu("Apply Background Music Settings")]
    public void ApplySettings()
    {
        EnsureAudioSource();
        if (_audioSource == null)
        {
            return;
        }

        _audioSource.clip = _musicClip;
        _audioSource.volume = _volume;
        _audioSource.loop = _loop;
        _audioSource.playOnAwake = _playOnStart;
        _audioSource.spatialBlend = 0f;
    }

    void Reset()
    {
        EnsureAudioSource();
        ApplySettings();
    }

    void Awake()
    {
        ApplySettings();
    }

    void Start()
    {
        if (_playOnStart && _audioSource != null && _musicClip != null && !_audioSource.isPlaying)
        {
            _audioSource.Play();
        }
    }

    void OnValidate()
    {
        _volume = Mathf.Clamp01(_volume);
        if (!Application.isPlaying)
        {
            EnsureAudioSource();
        }

        ApplySettings();
    }

    void EnsureAudioSource()
    {
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }

        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
    }
}
