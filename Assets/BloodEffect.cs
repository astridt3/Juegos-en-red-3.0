
using Fusion;
using UnityEngine;

public class BloodEffect : NetworkBehaviour
{
    private TickTimer lifeTimer;
    private ParticleSystem particles;

    public override void Spawned()
    {
        particles = GetComponent<ParticleSystem>();

        if (particles != null)
            particles.Play();

        if (Object.HasStateAuthority)
            lifeTimer = TickTimer.CreateFromSeconds(Runner, 2f);
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        if (lifeTimer.Expired(Runner))
        {
            Runner.Despawn(Object);
        }
    }
}
