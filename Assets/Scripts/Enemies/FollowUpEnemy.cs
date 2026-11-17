
using Fusion;
using UnityEngine;
using System.Collections.Generic;

public class FollowUpEnemy : NetworkBehaviour
{
    [Header("Target")]
    [SerializeField] private float detectionRadious = 5;
    [SerializeField] private LayerMask playerLayer;

    [Header("Lag Compensation")]
    [SerializeField] private float collisionCheckRadius = 0.5f;
    [SerializeField] private LayerMask playerHitboxLayer;

    [Header("Enemy settings")]
    [SerializeField] private byte initialHealth = 10;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private AudioDefinition attackSound;

    [Networked] public byte Health { get; set; }

    private Rigidbody2D rb;
    private NetworkObject target;
    private IAudioService audioService;

    private readonly List<LagCompensatedHit> lagCompensationHits =
        new List<LagCompensatedHit>();

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        audioService = ServiceLocator.Get<IAudioService>();
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
            Health = initialHealth;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || rb == null)
            return;

        GetClosestPlayer();
        FollowPlayer();
        CheckPlayerWithLagCompensation();
    }

    private void CheckPlayerWithLagCompensation()
    {
        if (target == null)
            return;

        Player player = target.GetComponent<Player>();

        if (player == null || !player.IsAlive)
            return;

        int hitsFound = Runner.LagCompensation.OverlapSphere(
            transform.position,
            collisionCheckRadius,
            target.InputAuthority,
            lagCompensationHits,
            playerHitboxLayer,
            HitOptions.SubtickAccuracy
        );

        for (int i = 0; i < hitsFound; i++)
        {
            if (lagCompensationHits[i].Hitbox == null)
                continue;

            Player hitPlayer =
                lagCompensationHits[i].Hitbox.GetComponentInParent<Player>();

            if (hitPlayer == player && hitPlayer.IsAlive)
            {
                hitPlayer.Kill();
                return;
            }
        }
    }

    private void GetClosestPlayer()
    {
        Collider2D[] players = Physics2D.OverlapCircleAll(
            rb.position,
            detectionRadious,
            playerLayer
        );

        target = null;

        float closestDistance = Mathf.Infinity;

        foreach (Collider2D playerCollider in players)
        {
            // Busca el NetworkObject en el padre del collider.
            NetworkObject networkObject =
                playerCollider.GetComponentInParent<NetworkObject>();

            if (networkObject == null || networkObject == Object)
                continue;

            // Busca el Player en el padre.
            Player player = networkObject.GetComponent<Player>();

            if (player == null || !player.IsAlive)
                continue;

            float distance = Vector2.Distance(
                rb.position,
                networkObject.transform.position
            );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                target = networkObject;
            }
        }
    }

    private void FollowPlayer()
    {
        if (target == null)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 direction =
            ((Vector2)target.transform.position - rb.position).normalized;

        rb.linearVelocity = direction * moveSpeed;
    }

    public void TakeDamage(byte amount)
    {
        if (!Object.HasStateAuthority || amount == 0 || Health == 0)
            return;

        Health = (byte)Mathf.Max(0, (int)Health - amount);

        if (audioService != null && attackSound != null)
            audioService.PlaySFX(attackSound);

        if (Health == 0)
            Die();
    }

    private void Die()
    {
        if (!Object.HasStateAuthority)
            return;

        rb.linearVelocity = Vector2.zero;

        Runner.Despawn(Object);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!Object.HasStateAuthority)
            return;

        // Busca Player en el collider o en sus padres.
        Player player =
            collision.collider.GetComponentInParent<Player>();

        if (player == null || !player.IsAlive)
            return;

        Debug.Log("FollowUpEnemy golpeó al jugador.");
        rb.linearVelocity = Vector2.zero;
        player.Kill();
    }
}
