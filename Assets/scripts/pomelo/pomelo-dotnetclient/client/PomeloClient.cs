using System.Collections;
using LitJson;

using System;
using System.Text;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;

namespace Pomelo.DotNetClient
{
    /// <summary>
    /// network state enum
    /// </summary>
    public enum NetWorkState
    {
        [Description("initial state")]
        CLOSED,

        [Description("connecting server")]
        CONNECTING,

        [Description("server connected")]
        CONNECTED,

        [Description("disconnected with server")]
        DISCONNECTED,

        [Description("connect timeout")]
        TIMEOUT,

        [Description("netwrok error")]
        ERROR
    }

    public class PomeloClient : IDisposable
    {
        /// <summary>
        /// netwrok changed event
        /// </summary>
        public event Action<NetWorkState> NetWorkStateChangedEvent;


        private NetWorkState netWorkState = NetWorkState.CLOSED;   //current network state

        private EventManager eventManager;
        private Socket socket;
        private Protocol protocol;
        private bool disposed = false;
        private uint reqId = 1;

        private List<Message> msgQueue = new List<Message>();
        private List<Action>  callBackQueue = new List<Action>();

        private Action<JsonData> handShakeCallBack = null;
        private bool handShakeCallBackCanCall = false;
        private JsonData handShakeCallBackData = new JsonData();

        private ManualResetEvent timeoutEvent = new ManualResetEvent(false);
        private int timeoutMSec = 8000;    //connect timeout count in millisecond
        private ClientProtocolType client_type;
        private object guard = new object();

        public bool IsConnected
        {
            get { return netWorkState == NetWorkState.CONNECTED;  }
        }

        public PomeloClient(ClientProtocolType type = ClientProtocolType.NORMAL)
        {
            this.client_type = type;
        }

        public string GetHandShakeCache()
        {
            if(this.protocol != null)
            {
                return this.protocol.HandShakeCache;
            }
            return "";
        }

        private void InitHandShakeCache(string handshake)
        {
            if(handshake != null && handshake != "")
            {
                // TODO 做检查
                //var md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
                //JsonData handshake = JsonMapper.ToObject(this.HandShakeCache);
                //byte[] handShakeCacheByte = Encoding.UTF8.GetBytes(this.HandShakeCache);
                //byte[] hash = md5.ComputeHash(handShakeCacheByte);

                //this.protocol.HandShakeVersion = Convert.ToBase64String(hash);
                this.protocol.InitProtoCache(handshake);
            }

        }
        Action disconnCallBack;
        bool bDisconnCallBack = false;
        /// <summary>
        /// initialize pomelo client
        /// </summary>
        /// <param name="host">server name or server ip (www.xxx.com/127.0.0.1/::1/localhost etc.)</param>
        /// <param name="port">server port</param>
        /// <param name="callback">socket successfully connected callback(in network thread)</param>
        public void initClient(string host, int port, string handshake = "", Action callback = null, Action disconnCallBack = null,
            byte[] clientcert = null, string clientpwd = "", string cathumbprint = null)
        {
            this.disconnCallBack = disconnCallBack;

            timeoutEvent.Reset();
            eventManager = new EventManager();
            NetWorkChanged(NetWorkState.CONNECTING);

            IPAddress ipAddress = new IPAddress(0);
            if (!IPAddress.TryParse(host, out ipAddress))
            {
                ipAddress = null;
            }

            if(ipAddress == null)
            {
                try
                {
                    IPAddress[] addresses = Dns.GetHostEntry(host).AddressList;
                    foreach (var item in addresses)
                    {
                        if (item.AddressFamily == AddressFamily.InterNetwork)
                        {
                            ipAddress = item;
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    NetWorkChanged(NetWorkState.ERROR);
                    Debug.Log(e);
                    return;
                }
            }


            if (ipAddress == null)
            {
                throw new Exception("can not parse host : " + host);
            }

            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ie = new IPEndPoint(ipAddress, port);
            
            Debug.Log("InitHandShakeCache Finished");
            socket.BeginConnect(ie, new AsyncCallback((result) =>
            {
                try
                {
                    this.socket.EndConnect(result);
                    NetWorkChanged(NetWorkState.CONNECTED);
                    if (this.client_type == ClientProtocolType.NORMAL)
                    {
                        this.protocol = new Protocol(this, this.socket, host);
                    }
                    else if (this.client_type == ClientProtocolType.TLS)
                    {
                        this.protocol = new Protocol(this, this.socket, host,
                            client_type, clientcert, clientpwd, cathumbprint);
                    }
                    else
                    {
                        throw new Exception("unsupported client type : " + this.client_type);
                    }
                    this.InitHandShakeCache(handshake);
                    
                    lock (guard)
                    {
                    
                        if (callback != null)
                        {
                            callBackQueue.Add(callback);
                        }
                    }
                }
                catch (SocketException e)
                {
                    if (netWorkState != NetWorkState.TIMEOUT)
                    {
                        NetWorkChanged(NetWorkState.ERROR);
                    }
                    //Dispose();

                    lock(guard)
                    {
                        bDisconnCallBack = true;
                    }

                    Debug.Log(e);
                }
                finally
                {
                    timeoutEvent.Set();
                }
            }), this.socket);

            if (timeoutEvent.WaitOne(timeoutMSec, false))
            {
                if (netWorkState != NetWorkState.CONNECTED && netWorkState != NetWorkState.ERROR)
                {
                    NetWorkChanged(NetWorkState.TIMEOUT);
                    lock (guard)
                    {
                        bDisconnCallBack = true;
                    }
                    //Dispose();
                }

                if(netWorkState == NetWorkState.ERROR)
                {
                    lock (guard)
                    {
                        bDisconnCallBack = true;
                    }

                    //Dispose();
                }
            }
        }

        /// <summary>
        /// 网络状态变化
        /// </summary>
        /// <param name="state"></param>
        private void NetWorkChanged(NetWorkState state)
        {
            netWorkState = state;

            if (NetWorkStateChangedEvent != null)
            {
                NetWorkStateChangedEvent(state);
            }
        }


        public bool connect(JsonData user, Action<JsonData> handshakeCallback)
        {
            try
            {
                this.handShakeCallBack = handshakeCallback;
                protocol.start(user, delegate (JsonData data)
                {
                    lock (guard)
                    {
                        this.handShakeCallBackData = data;
                        this.handShakeCallBackCanCall = true;
                    }
                });
                return true;
            }
            catch (Exception e)
            {
                Debug.Log(e.ToString());
                return false;
            }
        }

        

        

        private JsonData emptyMsg = new JsonData();
        public void request(string route, Action<JsonData> action)
        {
            this.request(route, emptyMsg, action);
        }

        public void request(string route, JsonData msg, Action<JsonData> action)
        {
            this.eventManager.AddCallBack(reqId, action);
            protocol.send(route, reqId, msg);

            reqId++;
        }

        public void notify(string route, JsonData msg)
        {
            protocol.send(route, msg);
        }

        public void on(string eventName, Action<JsonData> action)
        {
            eventManager.AddOnEvent(eventName, action);
        }

        internal void processMessage(Message msg)
        {
            lock (guard)
            {
                msgQueue.Add(msg);
            }
            

        }

        public void poll()
        {
            lock (guard)
            {
                foreach (var msg in msgQueue)
                {
                    if (msg.type == MessageType.MSG_RESPONSE)
                    {
                        //msg.data["__route"] = msg.route;
                        //msg.data["__type"] = "resp";

                        eventManager.InvokeCallBack(msg.id, msg.data);
                    }
                    else if (msg.type == MessageType.MSG_PUSH)
                    {
                        //msg.data["__route"] = msg.route;
                        //msg.data["__type"] = "push";
                        eventManager.InvokeOnEvent(msg.route, msg.data);
                    }
                }
                msgQueue.Clear();

                foreach (var callback in callBackQueue)
                {
                    callback();
                }
                callBackQueue.Clear();

                if(this.handShakeCallBackCanCall == true)
                {
                    this.handShakeCallBack(this.handShakeCallBackData);
                    this.handShakeCallBackCanCall = false;
                }

                if(this.bDisconnCallBack == true)
                {
                    if(this.disconnCallBack != null)
                    {
                        this.disconnCallBack();
                        

                    }
                    this.bDisconnCallBack = false;
                    this.Dispose(true);
                }
            }


        }

        public void disconnect()
        {
            if (socket != null)
            {
                // socket.Disconnect(false);
                try
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
                catch (Exception)
                {
                }
                socket.Close();
                socket = null;

            }
           
            NetWorkChanged(NetWorkState.DISCONNECTED);

            lock(guard)
            {
                if(Thread.CurrentThread.ManagedThreadId == 1)
                {
                if (disconnCallBack != null)
                {
                        disconnCallBack();
                        this.Dispose(true);
                    }
                }
                else
                {
                    if (disconnCallBack != null)
                    {
                        bDisconnCallBack = true;
                    }
                }
            }

            //Dispose();

        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // The bulk of the clean-up code
        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
                return;

            if (disposing)
            {
                // free managed resources
                if (this.protocol != null)
                {
                    this.protocol.close();
                }

                if (this.eventManager != null)
                {
                    this.eventManager.Dispose();
                }

                try
                {
                    this.socket.Shutdown(SocketShutdown.Both);
                    this.socket.Close();
                    this.socket = null;
                }
                catch (Exception)
                {
                    //todo : 有待确定这里是否会出现异常，这里是参考之前官方github上pull request。emptyMsg
                }

                this.disposed = true;
            }
        }
    }
}