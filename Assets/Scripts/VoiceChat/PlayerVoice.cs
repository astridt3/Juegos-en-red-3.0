using Fusion;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using UnityEngine;

public class PlayerVoice : NetworkBehaviour
{
    private VoiceNetworkObject voiceNetworkObject;
    private Recorder recorder;

    public override void Spawned()
    {
        if (!Object.HasInputAuthority)
            return;

        voiceNetworkObject = GetComponent<VoiceNetworkObject>();

        if (voiceNetworkObject == null)
        {
            Debug.LogError("No se encontró VoiceNetworkObject.");
            return;
        }

        recorder = voiceNetworkObject.RecorderInUse;

        if (recorder == null)
        {
            Debug.LogError("VoiceNetworkObject no tiene un Recorder asignado.");
            return;
        }

        recorder.InterestGroup = 0;
        recorder.TransmitEnabled = true;
        recorder.RecordingEnabled = true;

        Debug.Log("[PlayerVoice] Recorder configurado correctamente.");
    }
}
