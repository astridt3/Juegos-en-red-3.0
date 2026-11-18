
using Fusion;
using UnityEngine;

public class SniperEnemy : NetworkBehaviour
{
    [Header("Detection")]
    [SerializeField] private float detectionRange = 8f;

    [Header("Aiming")]
    [SerializeField] private float aimDuration = 1.5f;
    [SerializeField] private float reloadDuration = 1f;
    [SerializeField] private float aimLineLength = 10f;
    [SerializeField] private LineRenderer aimLine;
    [SerializeField] private Transform firePoint;

    [Header("Projectile")]
    [SerializeField] private NetworkPrefabRef projectilePrefab;
    [SerializeField] private float projectileSpeed = 15f;

    [Header("Animation")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite standingSprite;
    [SerializeField] private Sprite crouchingSprite;
    [SerializeField] private Sprite aimingSprite;
    [SerializeField] private Sprite firingSprite;
    [SerializeField] private Sprite recoilSprite;
    [SerializeField] private Sprite reloadingSprite;

    [SerializeField] private AudioDefinition disparo;
    private IAudioService audioService;

    [SerializeField] private float crouchDuration = 0.2f;
    [SerializeField] private float shotFrameDuration = 0.12f;

    [Header("Health")]
    [SerializeField] private byte initialHealth = 10;

    [Networked]
    public byte Health { get; set; }

    [Networked]
    private NetworkBool IsAiming { get; set; }

    [Networked]
    private Vector2 AimDirection { get; set; }

    [Networked]
    private int AnimationFrame { get; set; }

    private Rigidbody2D rb;
    private NetworkObject target;
    private float timer;

    private EnemyPhase currentPhase;

    private enum EnemyPhase
    {
        Searching,
        Crouching,
        Aiming,
        Shooting
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

        if (aimLine != null)
        {
            aimLine.positionCount = 2;
            aimLine.useWorldSpace = true;
            aimLine.enabled = false;
        }
    }

    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
            return;

        Health = initialHealth;
        currentPhase = EnemyPhase.Searching;
        IsAiming = false;
        AnimationFrame = 0;

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
                    StartCrouching(player);
                break;

            case EnemyPhase.Crouching:
                if (!IsTargetValid())
                {
                    CancelAiming();
                    return;
                }

                UpdateAimDirection();
                timer -= Runner.DeltaTime;

                if (timer <= 0f)
                {
                    AnimationFrame = 2;
                    IsAiming = true;
                    currentPhase = EnemyPhase.Aiming;
                    timer = aimDuration;
                }
                break;

            case EnemyPhase.Aiming:
                AimAndShoot();
                break;

            case EnemyPhase.Shooting:
                PlayShotAnimation();
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

    private void StartCrouching(NetworkObject player)
    {
        target = player;

        currentPhase = EnemyPhase.Crouching;
        AnimationFrame = 1;
        IsAiming = false;

        timer = crouchDuration;

        UpdateAimDirection();
    }

    private void AimAndShoot()
    {
        if (!IsTargetValid())
        {
            CancelAiming();
            return;
        }

        UpdateAimDirection();

        timer -= Runner.DeltaTime;

        if (timer <= 0f)
            Shoot();
    }

    private void PlayShotAnimation()
    {
        timer -= Runner.DeltaTime;

        if (timer > 0f)
            return;

        if (AnimationFrame < 5)
        {
            AnimationFrame++;
            timer = shotFrameDuration;
        }
        else
        {
            if (IsTargetValid())
            {
                AnimationFrame = 2;
                IsAiming = true;
                currentPhase = EnemyPhase.Aiming;
                timer = reloadDuration;
            }
            else
            {
                CancelAiming();
            }
        }
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
        Vector2 origin = firePoint != null
            ? (Vector2)firePoint.position
            : rb.position;

        Vector2 direction = (Vector2)target.transform.position - origin;

        if (direction.sqrMagnitude > 0.001f)
            AimDirection = direction.normalized;
    }

    private void Shoot()
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
            SniperProjectile projectile =
                projectileObject.GetComponent<SniperProjectile>();

            if (projectile != null)
                projectile.Launch(AimDirection, projectileSpeed);
        }
        if (audioService != null && disparo != null)
            audioService.PlaySFX(disparo);

        IsAiming = false;
        AnimationFrame = 3;
        currentPhase = EnemyPhase.Shooting;
        timer = shotFrameDuration;
    }

    private void CancelAiming()
    {
        target = null;
        IsAiming = false;
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

        Debug.Log("SniperEnemy chocó con un jugador.");

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
        {            
            if (Mathf.Abs(AimDirection.x) > 0.001f)
                spriteRenderer.flipX = AimDirection.x < 0f;

            Sprite selectedSprite = null;

            switch (AnimationFrame)
            {
                case 0: selectedSprite = standingSprite; break;
                case 1: selectedSprite = crouchingSprite; break;
                case 2: selectedSprite = aimingSprite; break;
                case 3: selectedSprite = firingSprite; break;
                case 4: selectedSprite = recoilSprite; break;
                case 5: selectedSprite = reloadingSprite; break;
            }

            if (selectedSprite != null)
                spriteRenderer.sprite = selectedSprite;
        }

        if (aimLine == null)
            return;

        aimLine.enabled = IsAiming;

        if (!IsAiming)
            return;

        Vector3 startPosition = firePoint != null
            ? firePoint.position
            : transform.position;

        Vector2 direction = AimDirection.sqrMagnitude > 0.001f
            ? AimDirection.normalized
            : Vector2.down;

        aimLine.SetPosition(0, startPosition);
        aimLine.SetPosition(
            1,
            startPosition + (Vector3)(direction * aimLineLength)
        );
    }

}
