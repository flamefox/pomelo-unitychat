using UnityEngine;
using System.Collections;
using Proto.chat;
using Proto.connector;
using System;
using Proto;
using Proto.gate;

[RequireComponent(typeof(pomeloBehaviour))]
public class chatofpomelo : MonoBehaviour
{
    string user;

    [TextArea(3, 10)]
    public string HandShakeCache;

    public UnityEngine.UI.Text text;
    public UnityEngine.UI.InputField input;
    public UnityEngine.UI.Button loginBtn;
    public UnityEngine.UI.Button sendBtn;

    public string host = "127.0.0.1";
    public int port = 3014;
    pomeloBehaviour client;
    void Awake()
    {

        client = GetComponent<pomeloBehaviour>();
        client.updateClientEvent += OnUpdateClient;
    }
    // Use this for initialization
    void Start()
    {
        //if (sendBtn) { sendBtn.interactable = true; }
        ConnectToGate();
    }

    public void ConnectToGate()
    {
        client.connectEvent += OnConnectToGate;
        client.closeEvent  += OnGateServerDisconnect;
        client.ConnectServer(host, port, Pomelo.DotNetClient.ClientProtocolType.NORMAL, HandShakeCache);
    }

    public void send()
    {
        this.send(input.text);
    }

    public void send(string message)
    {
        chatHandler.send(
            "pomelo",
            message,
            user,
            "*"
            );
    }

    public void OnConnectToConnector()
    {
        client.closeEvent += OnServerDisconnect;

        this.HandShakeCache = client.GetHandShakeCache();
        ServerEvent.onChat(delegate (ServerEvent.onChat_event ret)
        {
            string strMsg = string.Format("{0} : {1}.", ret.from, ret.msg);
            if (text)
            {
                text.text = text.text.Insert(text.text.Length, strMsg);
                text.text = text.text.Insert(text.text.Length, "\n");
            }
        });

        ServerEvent.onAdd(delegate (ServerEvent.onAdd_event msg)
        {

        });

        ServerEvent.onLeave(delegate (ServerEvent.onLeave_event msg)
        {

        });


        login();
    }
    public void OnConnectToGate()
    {
        this.HandShakeCache = client.GetHandShakeCache();
        gateHandler.queryEntry("1", delegate (gateHandler.queryEntry_result result)
        {
            client.connectEvent -= OnConnectToGate;
            client.closeEvent -= OnGateServerDisconnect;

            client.CloseClient();

            if (result.code == 500)
            {
                //TODO
            }

            if (result.code == 200)
            {
                client.connectEvent += OnConnectToConnector;  
                client.ConnectServer(result.host, result.port, Pomelo.DotNetClient.ClientProtocolType.NORMAL, HandShakeCache);
            }
             
            //TODO other event
        });
    }
    public void login()
    {
        user = "pomelo" + DateTime.Now.Millisecond;

        entryHandler.enter(user, "pomelo", delegate (entryHandler.enter_result result)
        {
            //if (sendBtn) { sendBtn.interactable = true; }
        });
    }

    private void OnUpdateClient()
    {
        Proto.Version.resetClient(client.pc);
    }

    private void OnServerDisconnect()
    {
        client.connectEvent -= OnConnectToConnector;
        client.closeEvent -= OnServerDisconnect;

        if (loginBtn) { loginBtn.gameObject.SetActive(true); }
        //if (sendBtn) { loginBtn.interactable = false; }

    }

    private void OnGateServerDisconnect()
    {
        client.connectEvent -= OnConnectToGate;
        client.closeEvent -= OnGateServerDisconnect;

        if (loginBtn) { loginBtn.gameObject.SetActive(true); }
        //if (sendBtn) { sendBtn.interactable = false; }

    }
}
