
using Fusion;
using UnityEngine;

public class ChargeEnemy : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float waitDuration = 1.5f;
    [SerializeField] private float dashSpeed = 12f;
    [SerializeField] private float maxDashDuration = 2.5f;
    [SerializeField] private float stoppingDistance = 0.2f;

    [Header("Health")]
    [SerializeField] private byte initialHealth = 10;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color preparingColor = Color.orange;
    [SerializeField] private Color dashingColor = Color.red;

    [Networked]
    public byte Health { get; set; }

    [Networked]
    private NetworkBool IsPreparing { get; set; }

    [Networked]
    private NetworkBool IsDashing { get; set; }

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;

    private Vector2 targetPosition;
    private Vector2 dashDirection;

    private float timer;
    private bool hasTarget;
    private bool dashing;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        rb.gravityScale = 0f;
    }

    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
            return;

        Health = initialHealth;
        StartWaiting();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        timer -= Runner.DeltaTime;

        if (!dashing)
        {
            rb.linearVelocity = Vector2.zero;

            if (timer <= 0f)
            {
                StartDash();
            }
        }
        else
        {
            Vector2 distanceToTarget = targetPosition - rb.position;

            if (distanceToTarget.magnitude <= stoppingDistance ||
                timer <= 0f)
            {
                StartWaiting();
                return;
            }

            float speed = Mathf.Min(dashSpeed, distanceToTarget.magnitude / Runner.DeltaTime);

            rb.linearVelocity = dashDirection * speed;
        }
    }

    private void StartWaiting()
    {
        dashing = false;

        IsPreparing = true;
        IsDashing = false;

        rb.linearVelocity = Vector2.zero;

        timer = waitDuration;

        hasTarget = TryGetClosestPlayerPosition(out targetPosition);

    }

    private void StartDash()
    {
        if (!hasTarget)
        {
            StartWaiting();
            return;
        }

        Vector2 direction = targetPosition - rb.position;

        if (direction.sqrMagnitude < 0.001f)
        {
            StartWaiting();
            return;
        }

        dashDirection = direction.normalized;

        dashing = true;

        IsPreparing = false;
        IsDashing = true;

        timer = maxDashDuration;

        Debug.Log("ChargeEnemy embistiendo.");
    }

    private bool TryGetClosestPlayerPosition(out Vector2 closestPosition)
    {
        closestPosition = Vector2.zero;

        float closestDistance = Mathf.Infinity;
        bool foundPlayer = false;

        foreach (PlayerRef playerRef in Runner.ActivePlayers)
        {
            NetworkObject playerObject = Runner.GetPlayerObject(playerRef);

            if (playerObject == null)
                continue;

            Player player = playerObject.GetComponent<Player>();

            if (player == null || !player.IsAlive)
                continue;

            Vector2 position = playerObject.transform.position;

            float distance = (position - rb.position).sqrMagnitude;

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPosition = position;
                foundPlayer = true;
            }
        }

        return foundPlayer;
    }

    public void TakeDamage(byte amount)
    {
        if (!Object.HasStateAuthority || amount == 0 || Health == 0)
            return;

        Health = (byte)Mathf.Max(0, Health - amount);

        if (Health == 0)
            Die();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!Object.HasStateAuthority || !dashing)
            return;

        Player player = collision.gameObject.GetComponentInParent<Player>();

        if (player != null && player.IsAlive)
            player.Kill();
    }

    private void Die()
    {
        if (!Object.HasStateAuthority)
            return;

        rb.linearVelocity = Vector2.zero;
        Runner.Despawn(Object);
    }

    public override void Render()
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.color = IsDashing
            ? dashingColor
            : IsPreparing ? preparingColor : normalColor;
    }
}
