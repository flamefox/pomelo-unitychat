using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using UnityEngine;

namespace Pomelo.DotNetClient
{
    class TransporterSSL : TransporterTCP
    {
        private SslStream sslstream;
        private byte[] clientcert = null;
        private string clientpwd = "";
        private static List<string> ca_thumbprints = new List<string>();
        private static List<string> target_hosts = new List<string>();
        private string target_host;

        public static void clearCAThumbprintList()
        {
            ca_thumbprints.Clear();
        }

        public static bool ValidateServerCertificate(
        object sender,
        X509Certificate certificate,
        X509Chain chain,
        SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;
            if (chain.ChainElements.Count < 1)
            {
                Debug.LogError("certificate failed. empty chain!");
                return false;
            }

            //check cert validity
            bool cert_is_ok = false;
            X509Certificate2 root = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
            for (int i = 0; i < ca_thumbprints.Count; ++i)
            {
                if (root.Thumbprint == ca_thumbprints[i])
                {
                    cert_is_ok = true;
                    break;
                }
            }
            if (!cert_is_ok)
            {
                Debug.LogError("certificate failed. unknown cert printer: " + root.Thumbprint);
                return false;
            }

            cert_is_ok = false;
            //check host
            for (int i = 0; i < target_hosts.Count; ++i)
            {
                if (root.Subject.Contains("CN=" + target_hosts[i]))
                {
                    cert_is_ok = true;
                    break;
                }
            }
            if (!cert_is_ok)
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
        public TransporterSSL(byte[] clientcert = null, string clientpwd = "", string cathumbprint = null)
        {
            this.sslstream = new SslStream(
                new NetworkStream(socket),
                false,
                new RemoteCertificateValidationCallback(ValidateServerCertificate),
                null
                );
           
            ca_thumbprints.Add(cathumbprint);
            this.clientcert = clientcert;
            this.clientpwd = clientpwd;
        }

        public new void Connect(IPEndPoint ep, int nTimeout = 8000)
        {
            target_hosts.Add(ep.Address.ToString());
            this.target_host = ep.Address.ToString();
            base.Connect(ep, nTimeout);
        }

        bool authorized()
        {
            if (null == sslstream)
            {
                return false;
            }
            try
            {
                if (this.clientcert != null)
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
                this.close();
            }
            return false;
        }

        public new void receive()
        {
            if (!this.authorized())
            {
                return;
            }
            this._receive();
        }

        private void _receive()
        {
            //Console.WriteLine("receive state : {0}, {1}", this.transportState, socket.Available);
            try
            {
                this.asyncReceive = sslstream.BeginRead(stateObject.buffer, 0, stateObject.buffer.Length, new AsyncCallback(endReceive), stateObject);
            }
            catch (Exception e)
            {

                Debug.Log(e);
                
                this.close();
            }
        }

        public new void send(byte[] buffer)
        {
            if (this.transportState != TransportState.closed)
            {
                try
                {
                    this.asyncSend = sslstream.BeginWrite(buffer, 0, buffer.Length, new AsyncCallback(sendCallback), stateObject);
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                    this.close();
                }
            }
        }

        private new void sendCallback(IAsyncResult asyncSend)
        {
            try
            {
                sslstream.EndWrite(asyncSend);
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
        }

    }
}
