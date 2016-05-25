using UnityEngine;
using System.Collections;

using Pomelo.DotNetClient;
using LitJson;
using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;

using Proto;
using Proto.gate;
using Proto.connector;
using Proto.chat;


public class pomeloBehaviour : MonoBehaviour
{

    [HideInInspector]
    public PomeloClient pc;

    public Action connectEvent;
    public Action closeEvent;
    public Action updateClientEvent;

    // Use this for initialization
    void Start()
    {

    }

    [ExecuteInEditMode]
    void OnDestroy()
    {
        CloseClient();
    }

    // Update is called once per frame
    void Update()
    {
        if (pc != null)
        {
            pc.poll();
        }
    }

    public void CloseClient()
    {
        if (pc != null)
        {
            pc.disconnect();
            pc = null;

            this.UpdateClient();
        }
    }


    //TODO TLS "C7773B9D1BF0C5C956C88C60440FA23C3889A403"
    public bool ConnectServer(string host, int port,
        ClientProtocolType eProtoType = ClientProtocolType.NORMAL,
        string HandShakeCache = "",
        byte[] clientcert = null, string clientpwd = "", string cathumbprint = null)
    {
        if (eProtoType == ClientProtocolType.TLS)
        {
            if (clientcert == null || cathumbprint == null)
            {
                return false;
            }
        }

        //TODO should not disconnect at some time
        this.CloseClient();
        pc = new PomeloClient(eProtoType);
        pc.HandShakeCache = HandShakeCache;
        pc.initClient(host, port, delegate ()
        {
            if (pc.IsConnected)
            {
                this.UpdateClient();
                pc.connect(null, delegate (JsonData data)
                {
                    if (connectEvent != null)
                    {
                        connectEvent();
                    }
                });
            }
            else
            {
                if (closeEvent != null)
                {
                    closeEvent();
                }
            }
        }
        , clientcert, "", cathumbprint
        );

        return true;
    }


    private void UpdateClient()
    {
        if (updateClientEvent != null)
        {
            updateClientEvent();
        }
    }


}
