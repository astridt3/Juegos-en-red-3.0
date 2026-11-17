
using Fusion;
using UnityEngine;

public class ZigZagEnemy : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float verticalSpeed = 2f;
    [SerializeField] private float horizontalSpeed = 3f;
    [SerializeField] private float horizontalLimit = 2.5f;

    [Header("Health")]
    [SerializeField] private byte initialHealth = 10;

    [Networked]
    public byte Health { get; set; }

    [Networked]
    private int Direction { get; set; }

    private Rigidbody2D rb;
    private Vector2 startPosition;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }

    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
            return;

        Health = initialHealth;
        Direction = 1;
        startPosition = rb.position;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        Vector2 position = rb.position;

        float leftLimit = startPosition.x - horizontalLimit;
        float rightLimit = startPosition.x + horizontalLimit;

        if (position.x >= rightLimit)
            Direction = -1;
        else if (position.x <= leftLimit)
            Direction = 1;

        rb.linearVelocity = new Vector2(
            Direction * horizontalSpeed,
            verticalSpeed
        );
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
}
