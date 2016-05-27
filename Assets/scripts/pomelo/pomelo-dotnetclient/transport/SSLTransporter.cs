using System;
using System.Net.Sockets;
using System.Text;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;
using System.Collections.Generic;
using UnityEngine;

namespace Pomelo.DotNetClient
{
    public class SSLTransporter : ITransporter
    {
        public const int HeadLength = 4;

        private SslStream sslstream;
        private Action<byte[]> messageProcesser;

        //Used for get message
        private StateObject stateObject = new StateObject();
        private TransportState transportState;
        private IAsyncResult asyncReceive;
        private IAsyncResult asyncSend;
        private bool onSending = false;
        private bool onReceiving = false;
        private byte[] headBuffer = new byte[4];
        private byte[] buffer;
        private int bufferOffset = 0;
        private int pkgLength = 0;
        private Action onDisconnect = null;
        private byte[] clientcert = null;
        private string clientpwd = "";
        private static List<string> ca_thumbprints = new List<string>();
        private static List<string> target_hosts = new List<string>();
        private string target_host;

        //private TransportQueue<byte[]> _receiveQueue = new TransportQueue<byte[]>();
        private System.Object _lock = new System.Object();

        public static void clearCAThumbprintList()
        {
            ca_thumbprints.Clear();
        }

        public void setOnConnect(Action cb)
        {
            this.onDisconnect = cb;
        }

        public static bool ValidateServerCertificate(
              object sender,
              X509Certificate certificate,
              X509Chain chain,
              SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;
            if(chain.ChainElements.Count < 1)
            {
                Debug.LogError("certificate failed. empty chain!");
                return false;
            }

            //check cert validity
            bool cert_is_ok = false;
            X509Certificate2 root = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
            for(int i=0;i<ca_thumbprints.Count;++i)
            {
                if (root.Thumbprint == ca_thumbprints[i])
                {
                    cert_is_ok = true;
                    break;
                }
            }
            if(!cert_is_ok)
            {
                Debug.LogError("certificate failed. unknown cert printer: " + root.Thumbprint);
                return false;
            }

            cert_is_ok = false;
            //check host
            for(int i=0;i<target_hosts.Count;++i)
            {
                if(root.Subject.Contains("CN="+target_hosts[i]))
                {
                    cert_is_ok = true;
                    break;
                }
            }
            if(!cert_is_ok)
            {
                Debug.LogError("certificate failed. mismatch host: " + root.Subject);
                return false;
            }
            return true;
            //Console.WriteLine("{0}", root.Thumbprint);
            //// Do not allow this client to communicate with unauthenticated servers.
            //X509Chain customChain = new X509Chain
            //{
            //    ChainPolicy = {
            //        VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority
            //    }
            //};
            //Boolean retValue = customChain.Build(chain.ChainElements[0].Certificate);
            //chain.Reset();
            //return retValue;
        }

        public SSLTransporter(NetworkStream stream, Action<byte[]> processer, string host, byte[] clientcert = null, string clientpwd = "", string cathumbprint = null)
        {
            this.sslstream = new SslStream(
                stream,
                false,
                new RemoteCertificateValidationCallback(ValidateServerCertificate),
                null
                );
            target_hosts.Add(host);
            ca_thumbprints.Add(cathumbprint);
            this.clientcert = clientcert;
            this.clientpwd = clientpwd;
            this.target_host = host;
            this.messageProcesser = processer;
            transportState = TransportState.readHead;
        }

        internal bool authorized()
        {
            if(null == sslstream)
            {
                return false;
            }
            try
            {
                if(this.clientcert != null)
                {
                    X509CertificateCollection certs = new X509CertificateCollection();
                    X509Certificate2 cert = new X509Certificate2(this.clientcert, this.clientpwd);
                    certs.Add(cert);
                    sslstream.AuthenticateAsClient(this.target_host, certs, SslProtocols.Tls, true);
                }
                else
                {
                    sslstream.AuthenticateAsClient(this.target_host);
                }

                return true;
            }
            catch (AuthenticationException e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
                if (e.InnerException != null)
                {
                    Console.WriteLine("Inner exception: {0}", e.InnerException.Message);
                }
                Console.WriteLine("Authentication failed - closing the connection.");
                sslstream.Close();
                if(this.onDisconnect != null)
                {
                    this.onDisconnect();
                }
            }
            return false;
        }

        public void start()
        {
            if(!this.authorized())
            {
                return;
            }
            this.receive();
        }

        public void send(byte[] buffer)
        {
            if (this.transportState != TransportState.closed)
            {
                //string str = "";
                //foreach (byte code in buffer)
                //{
                //    str += code.ToString();
                //}
                //Console.WriteLine("send:" + buffer.Length + " " + str.Length + "  " + str);
                try
                {

                    this.asyncSend = sslstream.BeginWrite(buffer, 0, buffer.Length, new AsyncCallback(sendCallback), stateObject);

                    this.onSending = true;
                }
                catch (Exception e)
                {

                    Debug.Log(e);
                    if (this.onDisconnect != null)
                        this.onDisconnect();
                    this.close();
                }
            }
        }

        private void sendCallback(IAsyncResult asyncSend)
        {
            try
            {
                sslstream.EndWrite(asyncSend);
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }

            this.onSending = false;

        }

        public void receive()
        {
            //Console.WriteLine("receive state : {0}, {1}", this.transportState, socket.Available);
            try
            {
                this.asyncReceive = sslstream.BeginRead(stateObject.buffer, 0, stateObject.buffer.Length, new AsyncCallback(endReceive), stateObject);
                this.onReceiving = true;
            }
            catch (Exception e)
            {

                Debug.Log(e);
                if (this.onDisconnect != null)
                    this.onDisconnect();
                this.close();
            }
        }

        public void close()
        {
            this.transportState = TransportState.closed;
            //try{
            //    if(this.onReceiving) socket.EndReceive (this.asyncReceive);
            //    if(this.onSending) socket.EndSend(this.asyncSend);
            //}catch (Exception e){
            //    Console.WriteLine(e.Message);
            //}
        }

        private void endReceive(IAsyncResult asyncReceive)
        {

            StateObject state = (StateObject)asyncReceive.AsyncState;
            SslStream stream = this.sslstream;

            if (this.transportState == TransportState.closed)
            {
                return;
            }

            try
            {
                int length = stream.EndRead(asyncReceive);

                this.onReceiving = false;

                if (length > 0)
                {
                    processBytes(state.buffer, 0, length);
                    //Receive next message
                    //Console.WriteLine(System.Text.Encoding.UTF8.GetString(state.buffer));
                    if (this.transportState != TransportState.closed) receive();
                }
                else
                {
                    if (this.onDisconnect != null) this.onDisconnect();
                    this.close();
                }

            }
            catch (Exception e)
            {
                Debug.Log(e);
                if (this.onDisconnect != null)
                    this.onDisconnect();
                this.close();
            }
        }

        internal void processBytes(byte[] bytes, int offset, int limit)
        {
            if (this.transportState == TransportState.readHead)
            {
                readHead(bytes, offset, limit);
            }
            else if (this.transportState == TransportState.readBody)
            {
                readBody(bytes, offset, limit);
            }
        }

        private bool readHead(byte[] bytes, int offset, int limit)
        {
            int length = limit - offset;
            int headNum = HeadLength - bufferOffset;

            if (length >= headNum)
            {
                //Write head buffer
                writeBytes(bytes, offset, headNum, bufferOffset, headBuffer);
                //Get package length
                pkgLength = (headBuffer[1] << 16) + (headBuffer[2] << 8) + headBuffer[3];

                //Init message buffer
                buffer = new byte[HeadLength + pkgLength];
                writeBytes(headBuffer, 0, HeadLength, buffer);
                offset += headNum;
                bufferOffset = HeadLength;
                this.transportState = TransportState.readBody;

                if (offset <= limit) processBytes(bytes, offset, limit);
                return true;
            }
            else
            {
                writeBytes(bytes, offset, length, bufferOffset, headBuffer);
                bufferOffset += length;
                return false;
            }
        }

        private void readBody(byte[] bytes, int offset, int limit)
        {
            int length = pkgLength + HeadLength - bufferOffset;
            if ((offset + length) <= limit)
            {
                writeBytes(bytes, offset, length, bufferOffset, buffer);
                offset += length;

                //Invoke the protocol api to handle the message
                this.messageProcesser.Invoke(buffer);
                this.bufferOffset = 0;
                this.pkgLength = 0;

                if (this.transportState != TransportState.closed)
                    this.transportState = TransportState.readHead;
                if (offset < limit)
                    processBytes(bytes, offset, limit);
            }
            else
            {
                writeBytes(bytes, offset, limit - offset, bufferOffset, buffer);
                bufferOffset += limit - offset;
                this.transportState = TransportState.readBody;
            }
        }

        private void writeBytes(byte[] source, int start, int length, byte[] target)
        {
            writeBytes(source, start, length, 0, target);
        }

        private void writeBytes(byte[] source, int start, int length, int offset, byte[] target)
        {
            for (int i = 0; i < length; i++)
            {
                target[offset + i] = source[start + i];
            }
        }

        private void print(byte[] bytes, int offset, int length)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = offset; i < length; i++)
                sb.Append(Convert.ToString(bytes[i], 16) + " ");
            //Debug.Log(sb.ToString());
        }
    }
}