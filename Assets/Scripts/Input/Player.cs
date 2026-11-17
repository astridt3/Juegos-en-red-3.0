
using Fusion;
using UnityEngine;

public class Player : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed;

    [Header("Visual")]
    [SerializeField] private GameObject bluePlayer;
    [SerializeField] private GameObject redPlayer;

    [SerializeField] private Color team1Color = Color.blue;
    [SerializeField] private Color team2Color = Color.red;

    [SerializeField] private GameObject indicator;

    private SpriteRenderer blueSpriteRenderer;
    private SpriteRenderer redSpriteRenderer;

    [Header("Power Up PvP")]
    [SerializeField] private float powerUpDuration = 5;

    [Networked] public byte Team { get; set; }
    [Networked] public NetworkBool IsAlive { get; set; }

    // POWER UP PVP
    [Networked] public NetworkBool CanPvp { get; set; }
    [Networked] private float PowerUpEndTime { get; set; }


    // DASH POWER UP
    [Header("Dash")]
    [SerializeField] private float dashDuration = 10;
    [SerializeField] private float dashDistance = 3;

    [Networked] public NetworkBool HasDash { get; set; }
    [Networked] private float DashEndTime { get; set; }

    [Header("Dash Visual")]
    [SerializeField] private TrailRenderer dashTrail;

    private NetworkButtons previousButtons;
    private Vector2 lastMovementDirection = Vector2.right;

    private int lastTeam = -1;


    public override void Spawned()
    {
        // INDICATOR
        if (indicator != null)
        {
            indicator.SetActive(Object.HasInputAuthority);
        }

        // REFERENCIAS VISUALES
        if (bluePlayer != null)
        {
            blueSpriteRenderer = bluePlayer.GetComponent<SpriteRenderer>();
        }

        if (redPlayer != null)
        {
            redSpriteRenderer = redPlayer.GetComponent<SpriteRenderer>();
        }

        if (Object.HasStateAuthority)
        {
            IsAlive = true;

            // DASH
            HasDash = false;
            DashEndTime = 0;
        }

        if (dashTrail != null)
        {
            dashTrail.emitting = false;
        }

        UpdateVisual();
    }


    public override void FixedUpdateNetwork()
    {
        if (!IsAlive)
            return;

        if (GetInput(out NetworkInputData data))
        {
            Vector2 movement = new Vector2(
                data.Direction.x,
                data.Direction.y
            );

            transform.Translate(
                movement * speed * Runner.DeltaTime
            );

            // GUARDAMOS LA ÚLTIMA DIRECCIÓN
            if (movement != Vector2.zero)
            {
                lastMovementDirection = movement.normalized;
            }

            // DASH
            if (Object.HasStateAuthority && HasDash)
            {
                if (Runner.SimulationTime >= DashEndTime)
                {
                    HasDash = false;

                    Debug.Log("Dash Power Up terminado");
                }
                else
                {
                    NetworkButtons pressedButtons = data.Buttons.GetPressed(previousButtons);

                    if (pressedButtons.IsSet(NetworkInputData.DashButton))
                    {
                        Dash();
                    }
                }

                previousButtons = data.Buttons;
            }
        }

        // POWER UP PVP
        if (Object.HasStateAuthority && CanPvp &&
            Runner.SimulationTime >= PowerUpEndTime)
        {
            CanPvp = false;

            Debug.Log("Power Up terminado");
        }
    }


    private void Dash()
    {
        transform.position += (Vector3)(lastMovementDirection * dashDistance);

        Debug.Log("DASH!");
    }


    public void ActivateDash()
    {
        if (!HasStateAuthority)
            return;

        HasDash = true;

        DashEndTime = Runner.SimulationTime + dashDuration;

        Debug.Log("Dash Power Up activado durante " + dashDuration + " segundos");
    }


    private void Update()
    {
        // ACTUALIZAR EQUIPO
        if (lastTeam != Team)
        {
            UpdateVisual();
        }

        // COLOR AL MORIR
        if (Team == 1 && blueSpriteRenderer != null)
        {
            blueSpriteRenderer.color = IsAlive ? team1Color : Color.gray;
        }
        else if (Team == 2 && redSpriteRenderer != null)
        {
            redSpriteRenderer.color = IsAlive ? team2Color : Color.gray;
        }

        // EFECTO VISUAL DEL DASH
        if (dashTrail != null)
        {
            dashTrail.emitting = HasDash;
        }
    }


    private void UpdateVisual()
    {
        lastTeam = Team;

        if (bluePlayer == null || redPlayer == null)
            return;

        // ACTIVAR EL PERSONAJE CORRESPONDIENTE
        bluePlayer.SetActive(Team == 1);
        redPlayer.SetActive(Team == 2);

        // COLORES
        if (Team == 1 && blueSpriteRenderer != null)
        {
            blueSpriteRenderer.color = IsAlive ? team1Color : Color.gray;
        }
        else if (Team == 2 && redSpriteRenderer != null)
        {
            redSpriteRenderer.color = IsAlive ? team2Color : Color.gray;
        }
    }


    public void ActivatePowerUp()
    {
        if (!HasStateAuthority)
            return;

        CanPvp = true;

        PowerUpEndTime = Runner.SimulationTime + powerUpDuration;

        Debug.Log("Power Up activado");
    }


    public void Kill()
    {
        if (!HasStateAuthority)
            return;

        if (!IsAlive)
            return;

        IsAlive = false;

        Debug.Log("Jugador " + Object.InputAuthority + " murió.");
    }
}
