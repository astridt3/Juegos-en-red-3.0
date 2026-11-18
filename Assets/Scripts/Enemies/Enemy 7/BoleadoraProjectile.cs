using Fusion;
using UnityEngine;

public class BoleadoraProjectile : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private float pushForce = 8f;

    [Networked]
    private Vector2 Direction { get; set; }

    [Networked]
    private float Speed { get; set; }

    private Rigidbody2D rb;
    private float lifeTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
            lifeTimer = lifeTime;
    }

    public void Launch(Vector2 direction, float speed)
    {
        if (!Object.HasStateAuthority)
            return;

        Direction = direction.normalized;
        Speed = speed;

        float angle = Mathf.Atan2(
            Direction.y,
            Direction.x
        ) * Mathf.Rad2Deg;

        if (rb != null)
            rb.rotation = angle;
        else
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || rb == null)
            return;

        rb.linearVelocity = Direction * Speed;

        lifeTimer -= Runner.DeltaTime;

        if (lifeTimer <= 0f)
            Runner.Despawn(Object);
    }


    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!Object.HasStateAuthority)
            return;

        Player player = collision.GetComponentInParent<Player>();

        if (player == null)
            player = collision.GetComponentInChildren<Player>();

        if (player == null || !player.IsAlive)
            return;

        Debug.Log("BoleadoraProjectile golpeó a un jugador.");

        player.ApplyKnockback(Direction, pushForce);

        Runner.Despawn(Object);
    }
}