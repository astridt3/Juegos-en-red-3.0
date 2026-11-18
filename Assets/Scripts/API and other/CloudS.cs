
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using UnityEngine;

public class CloudS : MonoBehaviour
{
    private const string leaderboardId = "general";

    private const string winsKey = "victorias";

    private Task initializationTask;

    private async Task AsegurarServicios()
    {
        if (initializationTask == null)
        {
            initializationTask = InicializarServicios();
        }

        await initializationTask;
    }

    private async Task InicializarServicios()
    {
        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        Debug.Log("Autenticación exitosa.");
        Debug.Log("Player ID: " + AuthenticationService.Instance.PlayerId);
    }

    public async Task RegistrarVictoria()
    {
        await AsegurarServicios();

        var datos = await CloudSaveService.Instance.Data.Player.LoadAsync(
            new HashSet<string> { winsKey }
        );

        int victorias = 0;

        if (datos.TryGetValue(winsKey, out var valor))
        {
            victorias = valor.Value.GetAs<int>();
        }

        victorias++;

        var nuevosDatos = new Dictionary<string, object>
        {
            { winsKey, victorias }
        };

        await CloudSaveService.Instance.Data.Player.SaveAsync(nuevosDatos);

        Debug.Log("Victorias guardadas: " + victorias);

        await LeaderboardsService.Instance.AddPlayerScoreAsync(
            leaderboardId,
            victorias
        );

        Debug.Log("Ranking actualizado: " + victorias);
    }

    public async Task MostrarRanking()
    {
        await AsegurarServicios();

        var resultado = await LeaderboardsService.Instance.GetScoresAsync(
            leaderboardId,
            new GetScoresOptions
            {
                Offset = 0,
                Limit = 100
            }
        );

        foreach (var jugador in resultado.Results)
        {
            Debug.Log(
                "Posición: " + (jugador.Rank + 1) +
                " | ID: " + jugador.PlayerId +
                " | Victorias: " + jugador.Score
            );
        }
    }

    public async Task MostrarMiPuntaje()
    {
        await AsegurarServicios();

        var jugador = await LeaderboardsService.Instance.GetPlayerScoreAsync(
            leaderboardId
        );

        Debug.Log(
            "Mi posición: " + (jugador.Rank + 1) +
            " | Victorias: " + jugador.Score
        );
    }
    public async Task<string> ObtenerRankingTexto()
    {
        await AsegurarServicios();

        var resultado = await LeaderboardsService.Instance.GetScoresAsync(
            leaderboardId,
            new GetScoresOptions
            {
                Offset = 0,
                Limit = 3
            }
        );

        if (resultado.Results.Count == 0)
        {
            return "Todavía no hay jugadores en el ranking.";
        }

        StringBuilder texto = new StringBuilder();

        texto.AppendLine("POS.       JUGADOR       VICTORIAS");
        texto.AppendLine("--------------------------------");

        foreach (var jugador in resultado.Results)
        {
            string id = jugador.PlayerId;

            if (id.Length > 8)
                id = id.Substring(0, 8);

            texto.AppendLine(
                (jugador.Rank + 1) + "º          " +
                "Jugador " + id + "          " +
                jugador.Score.ToString("0")
            );
        }

        return texto.ToString();
    }
}
