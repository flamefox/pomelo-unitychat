using System;
using LitJson;
using System.Text;
using UnityEngine;
using System.Net.Sockets;

namespace Pomelo.DotNetClient
{
    public class Protocol
    {
        private MessageProtocol messageProtocol;
        private ProtocolState state;
        private ITransporter transporter;
        private HandShakeService handshake;
        private HeartBeatService heartBeatService = null;
        private PomeloClient pc;
        public string HandShakeCache{ get; set; }


        public PomeloClient getPomeloClient(){
            return this.pc;
        }

        public string getDictVersion()
        {
            if(messageProtocol != null)
            {
                return messageProtocol.dictVersion;
            }

            return "";
        }

        public string getProtoVersion()
        {
            if (messageProtocol != null)
            {
                return messageProtocol.protoVersion;
            }

            return "";
        }

        public string HandShakeVersion { get; set; }


        public Protocol(PomeloClient pc, System.Net.Sockets.Socket socket, string target_host,
            ClientProtocolType type = ClientProtocolType.NORMAL,
            byte[] clientcert = null, string clientpwd = "", string cathumbprint = null){
            if(type == ClientProtocolType.NORMAL)
            {
                this.pc = pc;
                this.transporter = new Transporter(socket, this.processMessage);
                this.transporter.setOnConnect(onDisconnect);

                this.handshake = new HandShakeService(this);

                this.state = ProtocolState.start;
            }
            else
            {
                this.pc = pc;
                this.transporter = new SSLTransporter(new NetworkStream(socket), 
                    this.processMessage, 
                    target_host, clientcert, clientpwd, cathumbprint);
                this.transporter.setOnConnect(onDisconnect);

                this.handshake = new HandShakeService(this);

                this.state = ProtocolState.start;
            }
        }

		internal void start(JsonData user, Action<JsonData> callback){
            this.transporter.start();
            this.handshake.request(user, callback);

            this.state = ProtocolState.handshaking;
        }

        //Send notify, do not need id
		internal void send(string route, JsonData msg){
            send(route, 0, msg);
        }

        //Send request, user request id 
		internal void send(string route, uint id, JsonData msg){
            if (this.state != ProtocolState.working) return;

            byte[] body = messageProtocol.encode(route, id, msg);

            send(PackageType.PKG_DATA, body);
        }

		internal void send(PackageType type){
            if (this.state == ProtocolState.closed) return;
            transporter.send(PackageProtocol.encode(type));
        }

        //Send system message, these message do not use messageProtocol
		internal void send(PackageType type, JsonData msg){
            //This method only used to send system package
            if (type == PackageType.PKG_DATA) return;

            byte[] body = Encoding.UTF8.GetBytes(msg.ToString());

            send(type, body);
        }

        //Send message use the transporter
		internal void send(PackageType type, byte[] body){
            if (this.state == ProtocolState.closed) return;

            byte[] pkg = PackageProtocol.encode(type, body);

            transporter.send(pkg);
        }

        //Invoke by Transporter, process the message
		internal void processMessage(byte[] bytes){
            Package pkg = PackageProtocol.decode(bytes);

            //Ignore all the message except handshading at handshake stage
			if (pkg.type == PackageType.PKG_HANDSHAKE && this.state == ProtocolState.handshaking) {

                //Ignore all the message except handshading
				JsonData data = LitJson.JsonMapper.ToObject(Encoding.UTF8.GetString(pkg.body));

                processHandshakeData(data);

                this.state = ProtocolState.working;

			}else if (pkg.type == PackageType.PKG_HEARTBEAT && this.state == ProtocolState.working){
                this.heartBeatService.resetTimeout();
			}else if (pkg.type == PackageType.PKG_DATA && this.state == ProtocolState.working) {
                this.heartBeatService.resetTimeout();
                pc.processMessage(messageProtocol.decode(pkg.body));
			}else if (pkg.type == PackageType.PKG_KICK) {
                this.close();
            }
        }

        public void InitProtoCache(string data)
        {
            JsonData handshake = JsonMapper.ToObject(data);
            this.InitProtoCache(handshake);
        }

        private void InitProtoCache(JsonData sys)
        {
            JsonData dict = new JsonData();
            JsonData routeToCode = new JsonData();
            JsonData codeToRoute = new JsonData();
            if (sys.ContainsKey("dict")) dict = (JsonData)sys["dict"];
            //if (sys.ContainsKey("routeToCode")) routeToCode = (JsonData)sys["routeToCode"];
            //if (sys.ContainsKey("codeToRoute")) codeToRoute = (JsonData)sys["codeToRoute"];


            string dictVersion = "";
            if (sys.ContainsKey("dictVersion")) dictVersion = sys["dictVersion"].ToString();

            JsonData protos = new JsonData();
            JsonData serverProtos = new JsonData();
            JsonData clientProtos = new JsonData();

            string protoVersion = "";

            if (sys.ContainsKey("protos"))
            {
                protos = (JsonData)sys["protos"];
                serverProtos = (JsonData)protos["server"];
                clientProtos = (JsonData)protos["client"];
                if (protos.ContainsKey("version")) protoVersion = protos["version"].ToString();
            }

            if(messageProtocol != null)
            {
                if ((dictVersion != "" && messageProtocol.dictVersion != dictVersion)
                    || (protoVersion != "" && messageProtocol.protoVersion != protoVersion))
                {
                    //update cache 
                    JsonData sysNew = JsonMapper.ToObject(this.HandShakeCache);
                    if(sys.ContainsKey("dict"))
                    {
                        sysNew["dict"] = new JsonData();
                        sysNew["dict"] = sys["dict"];
                    }
                    if (sys.ContainsKey("dictVersion"))
                    {
                        sysNew["dictVersion"] = sys["dictVersion"];
                    }
                    if (sys.ContainsKey("protos"))
                    {
                        sysNew["protos"] = new JsonData();
                        sysNew["protos"] = sys["protos"];
                    }
                    this.HandShakeCache = sysNew.ToJson();
                    this.messageProtocol = null;
                    this.InitProtoCache(this.HandShakeCache);
                    
                    //MessageProtocol messageProtocolNew = new MessageProtocol(dict, serverProtos, clientProtos, dictVersion, protoVersion);

                    //if (dictVersion == "" && !sys.ContainsKey("dict"))
                    //{
                    //    messageProtocolNew.dict = messageProtocol.dict;
                    //    messageProtocolNew.abbrs = messageProtocol.abbrs;
                    //    messageProtocolNew.dictVersion = messageProtocol.dictVersion;
                    //}

                    //if(protoVersion == "" && !sys.ContainsKey("protos"))
                    //{
                    //    messageProtocolNew.encodeProtos = messageProtocol.encodeProtos;
                    //    messageProtocolNew.decodeProtos = messageProtocol.decodeProtos;

                    //    messageProtocolNew.protoVersion = messageProtocol.protoVersion;
                    //}

                    //messageProtocol = messageProtocolNew;
                }
            }
            else
            {
                messageProtocol = new MessageProtocol(dict, serverProtos, clientProtos, dictVersion, protoVersion);
                this.HandShakeCache = sys.ToJson();
            }           

        }

        

        private void processHandshakeData(JsonData msg){
            //Handshake error
			if(!msg.ContainsKey("code") || !msg.ContainsKey("sys") || Convert.ToInt32(msg["code"].ToString()) != 200){
                throw new Exception("Handshake error! Please check your handshake config.");
            }
            Debug.Log(msg.ToJson());
             
            //Set compress data
			JsonData sys = (JsonData)msg["sys"];


            //Init heartbeat service
            int interval = 0;
            if (sys.ContainsKey("heartbeat")) interval = Convert.ToInt32(sys["heartbeat"].ToString());
            heartBeatService = new HeartBeatService(interval, this);

            if (interval > 0)
            {
                heartBeatService.start();
            }

            this.InitProtoCache(sys);
            
            //send ack and change protocol state
            handshake.ack();
            this.state = ProtocolState.working;

            //Invoke handshake callback
			JsonData user = new JsonData();
            if(msg.ContainsKey("user")) user = (JsonData)msg["user"];
            handshake.invokeCallback(user);
        }

        //The socket disconnect
		private void onDisconnect(){
            this.pc.disconnect();
        }

		internal void close(){
            transporter.close();

            if (heartBeatService != null) heartBeatService.stop();

            this.state = ProtocolState.closed;
        }
    }
}

