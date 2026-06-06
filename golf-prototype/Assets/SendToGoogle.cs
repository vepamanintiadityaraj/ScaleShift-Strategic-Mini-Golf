using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using System;
using System.Collections;

public class SendToGoogle : MonoBehaviour
{
    [SerializeField] private string URL;

    private long sessionID;

    private void Awake()
    {
        sessionID = DateTime.Now.Ticks;
        Debug.Log("Analytics Session ID: " + sessionID);
    }

    public void Send(
        string eventName,
        string playerNumber,
        string ballSize,
        string shotsRemaining,
        string gameResult,
        string ballPosition = "N/A",
        string wallName = "N/A",
        string repeatCount = "N/A",
        string powerUpUsage = "N/A"
    )
    {
        if (string.IsNullOrEmpty(eventName)) eventName = "UnknownEvent";
        if (string.IsNullOrEmpty(playerNumber)) playerNumber = "0";
        if (string.IsNullOrEmpty(ballSize)) ballSize = "N/A";
        if (string.IsNullOrEmpty(shotsRemaining)) shotsRemaining = "0";
        if (string.IsNullOrEmpty(gameResult)) gameResult = "N/A";
        if (string.IsNullOrEmpty(ballPosition)) ballPosition = "N/A";
        if (string.IsNullOrEmpty(wallName)) wallName = "N/A";
        if (string.IsNullOrEmpty(repeatCount)) repeatCount = "N/A";
        if (string.IsNullOrEmpty(powerUpUsage)) powerUpUsage = "N/A";


        string gameLevel = SceneManager.GetActiveScene().name;

        StartCoroutine(Post(
            sessionID.ToString(),
            eventName,
            playerNumber,
            ballSize,
            shotsRemaining,
            gameResult,
            gameLevel,
            ballPosition,
            wallName,
            repeatCount,
            powerUpUsage
        ));
    }

    private IEnumerator Post(
        string sessionID,
        string eventName,
        string playerNumber,
        string ballSize,
        string shotsRemaining,
        string gameResult,
        string gameLevel,
        string ballPosition,
        string wallName,
        string repeatCount,
        string powerUpUsage
    )
    {
        WWWForm form = new WWWForm();

        form.AddField("entry.340943453", sessionID);
        form.AddField("entry.76796546", eventName);
        form.AddField("entry.1145489132", playerNumber);
        form.AddField("entry.223403144", ballSize);
        form.AddField("entry.360795417", shotsRemaining);
        form.AddField("entry.1053936860", gameResult);
        form.AddField("entry.422701348", gameLevel);
        form.AddField("entry.2061750595", ballPosition);

        form.AddField("entry.1515630975", wallName);
        form.AddField("entry.1894764003", repeatCount);
        form.AddField("entry.578439450", powerUpUsage);

        using (UnityWebRequest www = UnityWebRequest.Post(URL, form))
        {
            yield return www.SendWebRequest();

            Debug.Log("Response Code: " + www.responseCode);

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Analytics Error: " + www.error);
                if (www.downloadHandler != null)
                    Debug.LogError("Server Response: " + www.downloadHandler.text);
            }
            else
            {
                Debug.Log("Analytics Sent!");
            }
        }
    }
}