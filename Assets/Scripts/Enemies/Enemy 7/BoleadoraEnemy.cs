
using Fusion;
using UnityEngine;

public class BoleadoraEnemy : NetworkBehaviour
{
    [Header("Detection")]
    [SerializeField] private float detectionRange = 8f;

    [Header("Attack")]
    [SerializeField] private float prepareDuration = 1f;
    [SerializeField] private float throwDuration = 0.15f;
    [SerializeField] private float recoveryDuration = 0.5f;

    [Header("Projectile")]
    [SerializeField] private NetworkPrefabRef projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float projectileSpeed = 15f;

    [SerializeField] private AudioDefinition boleadora;
    private IAudioService audioService;

    [Header("Animation")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite standingSprite;
    [SerializeField] private Sprite spinningSprite;
    [SerializeField] private Sprite throwingSprite;
    [SerializeField] private Sprite emptyHandsSprite;

    [Header("Health")]
    [SerializeField] private byte initialHealth = 10;

    [Networked]
    public byte Health { get; set; }

    [Networked]
    private int AnimationFrame { get; set; }

    [Networked]
    private Vector2 AimDirection { get; set; }

    private Rigidbody2D rb;
    private NetworkObject target;

    private float timer;
    private EnemyPhase currentPhase;

    private enum EnemyPhase
    {
        Searching,
        Preparing,
        Throwing,
        Recovery
    }
    private void Start()
    {
        audioService = ServiceLocator.Get<IAudioService>();
    }
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }
    }

    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
            return;

        Health = initialHealth;
        AnimationFrame = 0;
        currentPhase = EnemyPhase.Searching;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || rb == null)
            return;

        rb.linearVelocity = Vector2.zero;

        switch (currentPhase)
        {
            case EnemyPhase.Searching:
                if (TryGetClosestPlayer(out NetworkObject player))
                    StartPreparing(player);
                break;

            case EnemyPhase.Preparing:
                if (!IsTargetValid())
                {
                    CancelAttack();
                    return;
                }

                UpdateAimDirection();

                timer -= Runner.DeltaTime;

                if (timer <= 0f)
                    ThrowBoleadoras();
                break;

            case EnemyPhase.Throwing:
                timer -= Runner.DeltaTime;

                if (timer <= 0f)
                {
                    AnimationFrame = 3;
                    currentPhase = EnemyPhase.Recovery;
                    timer = recoveryDuration;
                }
                break;

            case EnemyPhase.Recovery:
                timer -= Runner.DeltaTime;

                if (timer <= 0f)
                {
                    if (TryGetClosestPlayer(out NetworkObject nextPlayer))
                        StartPreparing(nextPlayer);
                    else
                    {
                        AnimationFrame = 0;
                        currentPhase = EnemyPhase.Searching;
                    }
                }
                break;
        }
    }

    private bool TryGetClosestPlayer(out NetworkObject closestPlayer)
    {
        closestPlayer = null;
        float closestDistance = detectionRange * detectionRange;

        foreach (PlayerRef playerRef in Runner.ActivePlayers)
        {
            NetworkObject playerObject = Runner.GetPlayerObject(playerRef);

            if (playerObject == null)
                continue;

            Player player = playerObject.GetComponentInChildren<Player>();

            if (player == null || !player.IsAlive)
                continue;

            float distance = (
                (Vector2)playerObject.transform.position - rb.position
            ).sqrMagnitude;

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPlayer = playerObject;
            }
        }

        return closestPlayer != null;
    }

    private void StartPreparing(NetworkObject player)
    {
        target = player;
        currentPhase = EnemyPhase.Preparing;

        AnimationFrame = 1;
        timer = prepareDuration;

        UpdateAimDirection();
    }

    private bool IsTargetValid()
    {
        if (target == null)
            return false;

        Player player = target.GetComponentInChildren<Player>();

        if (player == null || !player.IsAlive)
            return false;

        float distance = (
            (Vector2)target.transform.position - rb.position
        ).sqrMagnitude;

        return distance <= detectionRange * detectionRange;
    }

    private void UpdateAimDirection()
    {
        if (target == null)
            return;

        Vector2 origin = firePoint != null
            ? (Vector2)firePoint.position
            : rb.position;

        Vector2 direction = (Vector2)target.transform.position - origin;

        if (direction.sqrMagnitude > 0.001f)
            AimDirection = direction.normalized;
    }

    private void ThrowBoleadoras()
    {
        Vector3 spawnPosition = firePoint != null
            ? firePoint.position
            : transform.position;

        NetworkObject projectileObject = Runner.Spawn(
            projectilePrefab,
            spawnPosition,
            Quaternion.identity
        );

        if (projectileObject != null)
        {
            BoleadoraProjectile projectile =
                projectileObject.GetComponent<BoleadoraProjectile>();

            if (projectile != null)
                projectile.Launch(AimDirection, projectileSpeed);
        }
        if (audioService != null && boleadora != null)
            audioService.PlaySFX(boleadora);
        AnimationFrame = 2;
        currentPhase = EnemyPhase.Throwing;
        timer = throwDuration;
        target = null;
    }

    private void CancelAttack()
    {
        target = null;
        AnimationFrame = 0;
        currentPhase = EnemyPhase.Searching;
    }

    public void TakeDamage(byte amount)
    {
        if (!Object.HasStateAuthority || amount == 0 || Health == 0)
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

        if (player == null)
            player = collision.gameObject.GetComponentInParent<Player>();

        if (player == null || !player.IsAlive)
            return;

        Debug.Log("BoleadoraEnemy chocó con un jugador.");

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

        if (Mathf.Abs(AimDirection.x) > 0.001f)
            spriteRenderer.flipX = AimDirection.x < 0f;

        Sprite selectedSprite = null;

        switch (AnimationFrame)
        {
            case 0: selectedSprite = standingSprite; break;
            case 1: selectedSprite = spinningSprite; break;
            case 2: selectedSprite = throwingSprite; break;
            case 3: selectedSprite = emptyHandsSprite; break;
        }

        if (selectedSprite != null)
            spriteRenderer.sprite = selectedSprite;
    }
}
