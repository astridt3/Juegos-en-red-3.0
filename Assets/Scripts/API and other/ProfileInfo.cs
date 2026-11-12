using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;


public class ProfileInfo : MonoBehaviour
{
    public string apiUrl = "https://jsonplaceholder.typicode.com";

    private void Start()
    {
        StartCoroutine(GetProfile());
        StartCoroutine(SendProfile(
            JsonUtility.ToJson(new PlayerData
    {
                id = 1,
                nickname = "Jugador1",
                level = 10
            })
        ));
    }
    IEnumerator SendProfile(string json)
    {
        byte[] body = Encoding.UTF8.GetBytes(json);

        using UnityWebRequest request = new UnityWebRequest(apiUrl, "POST");
        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(request.error);
        }
        else
        {
            Debug.Log("Jugador creado correctamente");
            Debug.Log("Datos recibidos: " + request.downloadHandler.text);
        }
    }
    IEnumerator GetProfile()
    {
        using UnityWebRequest request = UnityWebRequest.Get(apiUrl + "/users/1");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log(request.downloadHandler.text);
        }
        else
        {
            Debug.LogError(request.error);
        }
    }
}