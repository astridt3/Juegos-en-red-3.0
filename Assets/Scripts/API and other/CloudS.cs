using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Leaderboards;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;

public class CloudS : MonoBehaviour
{
    private async void Start()
    {
        try
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            Debug.Log("Autenticacion exitosa");
            Debug.Log("PlayerID: " + AuthenticationService.Instance.PlayerId);
        }
        catch (System.Exception e)
        {
            Debug.LogError(e.Message);
        }
    }

    private async void Guardar()
    {
        try
        {
            await GuardarDatos(500, 10);
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
        }
    }
    public async Task GuardarDatos(int monedas, int level)
    {
        var datos = new Dictionary<string, object>
        {
            { "monedas", monedas },
            { "level", level }
        };

        await CloudSaveService.Instance.Data.Player.SaveAsync(datos);
        Debug.Log("Datos guardados");
    }

    private async void Cargar()
    {
        try
        {
            await CargarDatos();
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    public async Task CargarDatos()
    {
        var datos = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { "monedas", "level" });
        if (datos.TryGetValue("monedas", out var monedas))
        {
            Debug.Log("Monedas: " + monedas.Value.GetAs<int>());
        }
        if (datos.TryGetValue("level", out var level))
        {
            Debug.Log("Level: " + level.Value.GetAs<int>());
        }
    }

    public async void GuardarNuevoPuntaje(int puntos)
    {
        try
        {
            await GuardarPuntaje(puntos);
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    public async Task GuardarPuntaje(int puntos)
    {
        await LeaderboardsService.Instance.AddPlayerScoreAsync("general", puntos);
        Debug.Log("Puntaje enviado: " + puntos);
    }

    public async Task MostrarRanking()
    {
        var resultado = await LeaderboardsService.Instance.GetScoresAsync("general", new GetScoresOptions
        {
            Offset = 0,
            Limit = 100
        });

        foreach (var jugador in resultado.Results)
        {
            Debug.Log(
                "Posicion: " + (jugador.Rank + 1) +
                " | ID: " + jugador.PlayerId +
                " | Puntaje: " + jugador.Score
                );
        }
    }

    private async void MostrarPuntajePropio()
    {
        try
        {
            await MostrarMiPuntaje();
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
        }
    }
    public async Task MostrarMiPuntaje()
    {
        var jugador = await LeaderboardsService.Instance.GetPlayerScoreAsync("general");
        Debug.Log("Mi posicion: " + (jugador.Rank + 1) + " | Mi puntaje: " + jugador.Score);
    }
}