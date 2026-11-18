using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    private NetworkRunner runner;

    private NetworkRunner GetRunner()
    {
        if (runner == null || !runner.IsRunning)
        {
            runner = FindFirstObjectByType<NetworkRunner>();
        }

        return runner;
    }

    public void CargarEscena1() => CargarEscena("Level 1");
    public void CargarEscena2() => CargarEscena("Level 2");
    public void CargarEscena3() => CargarEscena("Level 3");

    private void CargarEscena(string sceneName)
    {
        NetworkRunner activeRunner = GetRunner();

        if (activeRunner == null || !activeRunner.IsRunning)
        {
            Debug.LogError("No se encontró ningún NetworkRunner activo.");
            return;
        }

        if (!activeRunner.IsSceneAuthority)
        {
            Debug.LogWarning("Solo el host puede cargar el nivel.");
            return;
        }

        Debug.Log("Cargando escena: " + sceneName);
        activeRunner.LoadScene(sceneName, LoadSceneMode.Single);
    }
}
