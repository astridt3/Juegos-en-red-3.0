
using Fusion;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerVoice : NetworkBehaviour
{
    private const byte GlobalGroup = 3;

    private VoiceNetworkObject voiceNetworkObject;
    private Recorder recorder;
    private FusionVoiceClient voiceClient;
    private Player player;

    private byte currentTeam;
    private bool groupsConfigured;

    private bool isMuted;
    private bool isGlobalChat;

    public override void Spawned()
    {
        if (!Object.HasInputAuthority)
            return;

        voiceNetworkObject = GetComponent<VoiceNetworkObject>();
        voiceClient = FindFirstObjectByType<FusionVoiceClient>();
        player = GetComponent<Player>();

        if (voiceNetworkObject == null || voiceClient == null || player == null)
        {
            Debug.LogError("Faltan componentes para configurar el Voice Chat.");
            return;
        }

        recorder = voiceNetworkObject.RecorderInUse;

        if (recorder == null)
        {
            Debug.LogError("No se encontró el Recorder.");
            return;
        }

        recorder.RecordingEnabled = true;
        recorder.TransmitEnabled = true;

        Debug.Log("PlayerVoice iniciado.");
    }

    private void Update()
    {
        if (!Object.HasInputAuthority || recorder == null)
            return;

        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
        {
            isMuted = !isMuted;
            recorder.TransmitEnabled = !isMuted;

            Debug.Log(isMuted ? "Micrófono silenciado." : "Micrófono activado.");
        }

        if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
        {
            isGlobalChat = !isGlobalChat;

            if (currentTeam != 0 && groupsConfigured)
            {
                SetVoiceGroup();
            }

            Debug.Log(isGlobalChat ? "Chat GLOBAL activado." : "Chat de EQUIPO activado.");
        }

        if (player == null || player.Team == 0)
            return;

        if (voiceClient == null || voiceClient.Client == null || !voiceClient.Client.InRoom)
            return;

        if (!groupsConfigured || currentTeam != player.Team)
        {
            ConfigureVoiceGroups(player.Team);
        }
    }

    private void ConfigureVoiceGroups(byte teamID)
    {
        byte[] groupsToRemove = null;

        if (currentTeam != 0 && currentTeam != teamID)
        {
            groupsToRemove = new byte[] { currentTeam };
        }

        voiceClient.Client.OpChangeGroups(
            groupsToRemove,
            new byte[] { teamID, GlobalGroup }
        );

        currentTeam = teamID;
        groupsConfigured = true;

        SetVoiceGroup();

        Debug.Log("Voice configurado para el Equipo " + teamID);
    }

    private void SetVoiceGroup()
    {
        if (recorder == null)
            return;

        recorder.InterestGroup = isGlobalChat ? GlobalGroup : currentTeam;
    }
}
