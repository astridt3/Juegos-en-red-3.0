
using Fusion;
using UnityEngine;

public class DashEnemy : NetworkBehaviour
{
    [Header("Slow Descent")]
    [SerializeField] private float descendSpeed = 1.2f;
    [SerializeField] private float descendDuration = 3f;

    [Header("Waiting")]
    [SerializeField] private float waitSpeed = -2f;

    [Header("Dash")]
    [SerializeField] private float waitBeforeDash = 0.5f;
    [SerializeField] private float dashSpeed = 12f;

    [Header("Lifetime")]
    [SerializeField] private float lifeTime = 10f;

    [Header("Health")]
    [SerializeField] private byte initialHealth = 10;

    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;

    [Networked]
    private NetworkBool IsDashing { get; set; }

    public byte Health { get; set; }

    private float lifeTimer;
    private float phaseTimer;
    private Vector2 dashDirection;

    private EnemyPhase currentPhase;

    private enum EnemyPhase
    {
        Descending,
        Waiting,
        Dashing
    }
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.angularVelocity = 0f;
            rb.freezeRotation = true;
        }
    }


    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
            return;

        Health = initialHealth;

        lifeTimer = lifeTime;
        phaseTimer = descendDuration;

        currentPhase = EnemyPhase.Descending;
        dashDirection = Vector2.down;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || rb == null)
            return;

        lifeTimer -= Runner.DeltaTime;

        if (lifeTimer <= 0f)
        {
            Die();
            return;
        }

        switch (currentPhase)
        {
            case EnemyPhase.Descending:
                Descend();
                break;

            case EnemyPhase.Waiting:
                WaitBeforeDashing();
                break;

            case EnemyPhase.Dashing:
                Dash();
                break;
        }
    }

    private void Descend()
    {
        rb.linearVelocity = Vector2.down * descendSpeed;

        phaseTimer -= Runner.DeltaTime;

        if (phaseTimer <= 0f)
        {
            currentPhase = EnemyPhase.Waiting;
            phaseTimer = waitBeforeDash;
            rb.linearVelocity = Vector2.zero;

            Debug.Log("Enemy3 esperando.");
        }
    }

    private void WaitBeforeDashing()
    {
        rb.linearVelocity = Vector2.down * waitSpeed;

        phaseTimer -= Runner.DeltaTime;

        if (phaseTimer <= 0f)
            StartDash();
    }

    private void StartDash()
    {
        Vector2 targetPosition;

        if (TryGetClosestPlayerPosition(out targetPosition))
        {
            Vector2 direction = targetPosition - rb.position;

            dashDirection = direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : Vector2.down;
        }
        else
        {
            dashDirection = Vector2.down;
        }

        currentPhase = EnemyPhase.Dashing;
        IsDashing = true;

        rb.linearVelocity = dashDirection * dashSpeed;

        Debug.Log("El enemy3 se manda");
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

            Vector2 playerPosition = playerObject.transform.position;
            float distance = (playerPosition - rb.position).sqrMagnitude;

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPosition = playerPosition;
                foundPlayer = true;
            }
        }

        return foundPlayer;
    }

    private void Dash()
    {
        rb.linearVelocity = dashDirection * dashSpeed;
    }

    public void TakeDamage(byte amount)
    {
        if (!Object.HasStateAuthority)
            return;

        if (amount == 0 || Health == 0)
            return;

        Health = (byte)Mathf.Max(0, (int)Health - amount);

        if (Health == 0)
            Die();
    }


    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!Object.HasStateAuthority)
            return;

        Player player = collision.collider.GetComponentInParent<Player>();

        if (player == null || !player.IsAlive)
            return;

        rb.linearVelocity = Vector2.zero;
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
        if (spriteRenderer != null)
            spriteRenderer.color = IsDashing ? Color.red : Color.white;
    }
}
