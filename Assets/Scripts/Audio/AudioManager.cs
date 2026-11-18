using UnityEngine;

public class AudioManager : MonoBehaviour, IAudioService
{
    private static AudioManager instance;

    [Header("Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSourcePool sfxPool;

    [Header("Initial Music")]
    [SerializeField] private AudioDefinition initialMusic;

    private AudioDefinition currentMusic;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        if (musicSource != null)
            musicSource.playOnAwake = false;
    }

    private void Start()
    {
        if (initialMusic != null)
            PlayMusic(initialMusic);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public void PlayMusic(AudioDefinition definition)
    {
        if (definition == null || musicSource == null)
            return;

        if (definition == currentMusic && musicSource.isPlaying)
            return;

        AudioClip clip = definition.GetClip();

        if (clip == null)
            return;

        currentMusic = definition;

        musicSource.ApplyDefinition(definition);
        musicSource.clip = clip;
        musicSource.Play();
    }

    public void PlaySFX(AudioDefinition definition)
    {
        if (definition == null || sfxPool == null)
            return;

        PooledAudioSource source = sfxPool.Get();
        source.Play(definition);
    }

    public void StopMusic()
    {
        musicSource.Stop();
        currentMusic = null;
    }

    public void StopAll()
    {
        musicSource.Stop();
        sfxPool.StopAll();
        currentMusic = null;
    }
}
