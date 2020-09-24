using System;
using System.Collections;
using System.Collections.Generic;
using BestHTTP.WebSocket;
using UnityEngine;
using UnityEngine.XR.ARFoundation.Samples;

public class MainLogic : MonoBehaviour
{
    public string client = "from";
    private string address = "ws://192.168.3.10:8080";
    private WebSocket webSocket;

    // Start is called before the first frame update
    void Start()
    {
        // Create the WebSocket instance
        this.webSocket = new WebSocket(new Uri(address));

        this.webSocket.StartPingThread = true;

        // Subscribe to the WS events
        this.webSocket.OnOpen += OnOpen;
        this.webSocket.OnMessage += OnMessageReceived;
        this.webSocket.OnClosed += OnClosed;
        this.webSocket.OnError += OnError;

        // Start connecting to the server
        this.webSocket.Open();
    }

    public void SendText(string msg)
    {
        var goes = GameObject.FindGameObjectsWithTag("robot");
        if (goes.Length != 1)
        {
            Debug.Log("not found robot");
            return;
        }
        Debug.Log(msg);
        this.webSocket.Send(msg);
    }

    void OnDestroy()
    {
        if (this.webSocket != null)
        {
            this.webSocket.Close();
            this.webSocket = null;
        }
    }

    void OnOpen(WebSocket ws)
    {
        Debug.Log(client);
        this.webSocket.Send(client);
    }

    /// <summary>
    /// Called when we received a text message from the server
    /// </summary>
    void OnMessageReceived(WebSocket ws, string message)
    {
        if (client == "to")
        {
            Debug.Log(DateTime.Now);
            this.transform.GetComponentInChildren<BoneController>().ApplyBonePoseFromServer(message);
        }
    }

    /// <summary>
    /// Called when the web socket closed
    /// </summary>
    void OnClosed(WebSocket ws, UInt16 code, string message)
    {
        webSocket = null;
    }

    /// <summary>
    /// Called when an error occured on client side
    /// </summary>
    void OnError(WebSocket ws, string error)
    {
        webSocket = null;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
