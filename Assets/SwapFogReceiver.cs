
using Fusion;
using UnityEngine;

public class SwapFogReceiver : NetworkBehaviour
{
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_ShowFog(PlayerRef recipient)
    {
        // Solo los dos jugadores involucrados ejecutan el efecto
        if (Runner.LocalPlayer != recipient)
            return;

        Debug.Log("Niebla recibida por: " + recipient);

        FogScreenEffect.ShowFog();
    }
}
