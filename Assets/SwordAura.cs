
using UnityEngine;

public class SwordAura : MonoBehaviour
{
    [Header("Lava Ring")]
    [SerializeField] private float radius = 0.7f;
    [SerializeField] private float thickness = 0.12f;
    [SerializeField] private int segments = 120;

    [Header("Colors")]
    [SerializeField] private Color lavaOrange = new Color(1f, 0.25f, 0f, 0.7f);
    [SerializeField] private Color lavaRed = new Color(0.8f, 0.02f, 0f, 0.45f);
    [SerializeField] private Color lavaYellow = new Color(1f, 0.7f, 0.05f, 0.9f);

    [Header("Animation")]
    [SerializeField] private float animationSpeed = 3f;
    [SerializeField] private float pulseSpeed = 4f;
    [SerializeField] private float irregularity = 0.045f;

    private LineRenderer outerRing;
    private LineRenderer innerRing;
    private LineRenderer hotRing;

    private bool activeAura;

    private void Awake()
    {
        outerRing = CreateRing("Outer Lava", thickness * 2f, lavaRed, 0);
        innerRing = CreateRing("Lava Ring", thickness, lavaOrange, 1);
        hotRing = CreateRing("Hot Lava", thickness * 0.3f, lavaYellow, 2);

        ActivateAura(false);
    }

    private LineRenderer CreateRing(string objectName, float width, Color color, int order)
    {
        GameObject obj = new GameObject(objectName);
        obj.transform.SetParent(transform, false);

        LineRenderer ring = obj.AddComponent<LineRenderer>();

        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = segments;
        ring.startWidth = width;
        ring.endWidth = width;
        ring.startColor = color;
        ring.endColor = color;
        ring.sortingOrder = order + 10;

        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = Color.white;
        ring.material = mat;

        return ring;
    }

    private void Update()
    {
        if (!activeAura)
            return;

        float time = Time.time * animationSpeed;
        float pulse = Mathf.Sin(Time.time * pulseSpeed) * 0.04f;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;

            float wave = Mathf.Sin(angle * 7f + time) * irregularity;
            float wave2 = Mathf.Sin(angle * 13f - time * 1.5f) * irregularity * 0.4f;

            float currentRadius = radius + wave + wave2 + pulse;

            float x = Mathf.Cos(angle) * currentRadius;
            float y = Mathf.Sin(angle) * currentRadius * 0.35f;

            Vector3 position = new Vector3(x, y, 0);

            outerRing.SetPosition(i, position);
            innerRing.SetPosition(i, position);
            hotRing.SetPosition(i, position);
        }

        float glow = 0.65f + Mathf.Sin(Time.time * pulseSpeed) * 0.2f;

        outerRing.startColor = new Color(lavaRed.r, lavaRed.g, lavaRed.b, glow * 0.5f);
        outerRing.endColor = outerRing.startColor;

        innerRing.startColor = new Color(lavaOrange.r, lavaOrange.g, lavaOrange.b, glow);
        innerRing.endColor = innerRing.startColor;

        hotRing.startColor = new Color(lavaYellow.r, lavaYellow.g, lavaYellow.b, glow);
        hotRing.endColor = hotRing.startColor;
    }

    public void ActivateAura(bool active)
    {
        activeAura = active;

        if (outerRing != null) outerRing.enabled = active;
        if (innerRing != null) innerRing.enabled = active;
        if (hotRing != null) hotRing.enabled = active;
    }
}
