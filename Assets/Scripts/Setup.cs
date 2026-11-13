using UnityEngine;

public class Setup : MonoBehaviour
{
    [SerializeField] private AudioManager audioManager;

    private static Setup instance;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        DontDestroyOnLoad(gameObject);

        ServiceLocator.Register<IAudioService>(audioManager);
    }
}
