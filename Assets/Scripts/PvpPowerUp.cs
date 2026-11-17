
using Fusion;
using UnityEngine;

public class PvpPowerUp : NetworkBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!Object.HasStateAuthority)
            return;

        Player player = collision.GetComponentInParent<Player>();

        if (player != null)
        {
            player.ActivatePowerUp();

            Runner.Despawn(Object);
        }
    }
}
