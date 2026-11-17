
using Fusion;
using UnityEngine;

public class DownEnemy : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Despawn")]
    [SerializeField] private float lifeTime = 10f;

    [Header("Enemy Sounds")]
    [SerializeField] private AudioDefinition enemy2Spawn;
    [SerializeField] private AudioDefinition playerKill;

    [Networked]
    private float NetworkMoveSpeed { get; set; }

    private IAudioService audioService;
    private Rigidbody2D rb;
    private float lifeTimer;

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
        if (!Object.HasStateAuthority)
            return;

        lifeTimer = lifeTime;

        NetworkMoveSpeed =
            moveSpeed * LiveOpsConfig.EnemySpeedMultiplier;

        Debug.Log("Velocidad del enemigo: " + NetworkMoveSpeed);

        if (audioService != null && enemy2Spawn != null)
            audioService.PlaySFX(enemy2Spawn);
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        transform.position += Vector3.down * NetworkMoveSpeed * Runner.DeltaTime;

        lifeTimer -= Runner.DeltaTime;

        if (lifeTimer <= 0)
            Runner.Despawn(Object);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!Object.HasStateAuthority)
            return;

        // Busca Player en el collider o en sus padres.
        Player player = collision.collider.GetComponentInParent<Player>();

        if (player == null || !player.IsAlive)
            return;

        if (audioService != null && playerKill != null)
            audioService.PlaySFX(playerKill);

        Debug.Log("DownEnemy golpeó al jugador.");

        player.Kill();
    }
}
