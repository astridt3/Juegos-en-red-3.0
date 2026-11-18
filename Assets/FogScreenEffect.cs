
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class FogScreenEffect : MonoBehaviour
{
    private static FogScreenEffect instance;

    [Header("Fog Settings")]
    [SerializeField] private float duration = 3f;
    [SerializeField] private float fadeTime = 0.5f;
    [SerializeField] private float fogSpeed = 0.04f;
    [SerializeField] private Color fogColor = new Color(0.65f, 0.72f, 0.78f, 0.85f);

    private RawImage fogImage;
    private CanvasGroup canvasGroup;
    private Texture2D fogTexture;
    private Coroutine fogCoroutine;

    private void Awake()
    {
        instance = this;
        CreateFog();

        Debug.Log("FogScreenEffect creado correctamente.");
    }

    private void CreateFog()
    {
        GameObject canvasObject = new GameObject("FogCanvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;

        GameObject imageObject = new GameObject("FogImage");
        imageObject.transform.SetParent(canvasObject.transform, false);

        fogImage = imageObject.AddComponent<RawImage>();

        RectTransform rect = fogImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        fogTexture = GenerateFogTexture();

        fogImage.texture = fogTexture;
        fogImage.color = fogColor;
        fogImage.uvRect = new Rect(0, 0, 2, 2);
    }

    private Texture2D GenerateFogTexture()
    {
        int size = 256;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float nx = (float)x / size;
                float ny = (float)y / size;

                float noise1 = Mathf.PerlinNoise(nx * 4f, ny * 4f);
                float noise2 = Mathf.PerlinNoise(nx * 9f + 2f, ny * 9f + 2f);

                float noise = noise1 * 0.7f + noise2 * 0.3f;

                float alpha = Mathf.Clamp01((noise - 0.25f) * 2f);

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();

        return texture;
    }

    public static void ShowFog()
    {
        if (instance == null)
        {
            Debug.LogError("No existe FogScreenEffect en esta escena.");
            return;
        }

        Debug.Log("Mostrando niebla.");

        if (instance.fogCoroutine != null)
            instance.StopCoroutine(instance.fogCoroutine);

        instance.fogCoroutine = instance.StartCoroutine(instance.PlayFog());
    }

    private IEnumerator PlayFog()
    {
        float timer = 0f;

        // Aparece
        while (timer < fadeTime)
        {
            timer += Time.deltaTime;

            canvasGroup.alpha = Mathf.Lerp(0f, 1f, timer / fadeTime);

            UpdateFog();

            yield return null;
        }

        canvasGroup.alpha = 1f;

        // Se mantiene
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            UpdateFog();

            yield return null;
        }

        // Desaparece
        timer = 0f;

        while (timer < fadeTime)
        {
            timer += Time.deltaTime;

            canvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeTime);

            UpdateFog();

            yield return null;
        }

        canvasGroup.alpha = 0f;
        fogCoroutine = null;
    }

    private void UpdateFog()
    {
        float time = Time.time * fogSpeed;

        fogImage.uvRect = new Rect(
            time,
            time * 0.5f,
            2f,
            2f
        );
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        if (fogTexture != null)
            Destroy(fogTexture);
    }
}
