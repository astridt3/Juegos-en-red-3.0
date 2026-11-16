
using Fusion;
using UnityEngine;
using System.Collections.Generic;

public class SwapPowerUp : NetworkBehaviour
{
    [SerializeField] private float fallSpeed = 5f;
    [SerializeField] private float lifeTime = 20f;

    private Rigidbody2D rb;
    private float lifeTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public override void Spawned()
    {
        lifeTimer = lifeTime;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        rb.linearVelocity = Vector2.down * fallSpeed;

        transform.Rotate(0f, 0f, 180f * Runner.DeltaTime);

        lifeTimer -= Runner.DeltaTime;

        if (lifeTimer <= 0f)
            Runner.Despawn(Object);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!Object.HasStateAuthority)
            return;

        Player player = collision.GetComponent<Player>();

        if (player == null)
            player = collision.GetComponentInParent<Player>();

        if (player == null || !player.IsAlive)
            return;

        SwapPosition(player);
    }

    private void SwapPosition(Player player)
    {
        List<Player> players = new List<Player>();

        foreach (PlayerRef playerRef in Runner.ActivePlayers)
        {
            NetworkObject playerObject = Runner.GetPlayerObject(playerRef);

            if (playerObject == null)
                continue;

            Player otherPlayer = playerObject.GetComponent<Player>();

            if (otherPlayer == null || !otherPlayer.IsAlive || otherPlayer == player)
                continue;

            players.Add(otherPlayer);
        }

        if (players.Count == 0)
        {
            Runner.Despawn(Object);
            return;
        }

        Player target = players[Random.Range(0, players.Count)];

        Vector3 playerPosition = player.transform.position;
        Vector3 targetPosition = target.transform.position;

        player.transform.position = targetPosition;
        target.transform.position = playerPosition;

        Runner.Despawn(Object);
    }
}
