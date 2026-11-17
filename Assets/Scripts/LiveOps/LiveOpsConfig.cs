
using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.RemoteConfig;

public static class LiveOpsConfig
{
    private const float DefaultEnemySpeedMultiplier = 1f;

    private const string EnemySpeedKey = "enemy_speed_multiplier";

    public static float EnemySpeedMultiplier { get; private set; }
        = DefaultEnemySpeedMultiplier;

    public struct UserAttributes { }
    public struct AppAttributes { }

    public static async Task LoadConfig()
    {
        // Siempre empezamos con el valor predeterminado.
        EnemySpeedMultiplier = DefaultEnemySpeedMultiplier;

        try
        {
            // Inicializar Unity Services.
            await UnityServices.InitializeAsync();

            // Autenticación anónima.
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            // Descargar configuración remota.
            await RemoteConfigService.Instance.FetchConfigsAsync(
                new UserAttributes(),
                new AppAttributes()
            );

            // Obtener el multiplicador.
            float remoteValue =
                RemoteConfigService.Instance.appConfig.GetFloat(
                    EnemySpeedKey,
                    DefaultEnemySpeedMultiplier
                );

            // Validar el valor recibido.
            if (float.IsNaN(remoteValue) ||
                float.IsInfinity(remoteValue) ||
                remoteValue < 0.25f ||
                remoteValue > 3f)
            {
                Debug.LogWarning(
                    "Remote Config: multiplicador inválido. Se utilizan defaults."
                );

                return;
            }

            EnemySpeedMultiplier = remoteValue;

            Debug.Log(
                "Remote Config cargado. Enemy Speed Multiplier: "
                + EnemySpeedMultiplier
            );
        }
        catch (Exception e)
        {
            Debug.LogWarning(
                "No se pudo descargar Remote Config. "
                + "Se utilizan valores predeterminados. "
                + e.Message
            );

            EnemySpeedMultiplier = DefaultEnemySpeedMultiplier;
        }
    }
}
