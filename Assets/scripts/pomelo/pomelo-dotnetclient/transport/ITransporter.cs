using System;
using LitJson;
using System.Text;
using UnityEngine;

namespace Pomelo.DotNetClient
{
    public interface ITransporter
    {
        void start();
        void send(byte[] buffer);
        void receive();
        void setOnConnect(Action cb);
        void close();
    }
}