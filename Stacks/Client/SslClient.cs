using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Stacks
{
    public class SslClient : IRawByteClient
    {
        public event Action<ArraySegment<byte>> Received;
        public event Action<int> Sent;
        public event Action<Exception> Disconnected;

        private IRawSocketClient client;
        private Socket socket;
        private SslStream sslStream;

        public SslClient(IRawSocketClient client)
            : this(client, allowEveryCertificate: false)
        { }

        public SslClient(IRawSocketClient client, bool allowEveryCertificate)
        {
            if (allowEveryCertificate)
                Initialize(client, AllowEveryCertificate);
            else
                Initialize(client, null);
        }

        public SslClient(IRawSocketClient client, 
                         RemoteCertificateValidationCallback remoteCertificateValidationCallback)
        {
            Initialize(client, remoteCertificateValidationCallback);
        }

        private void Initialize(IRawSocketClient client,
                                RemoteCertificateValidationCallback remoteCertificateValidationCallback)
        {
            this.client = client;
            this.socket = client.Socket;
            this.sslStream = new SslStream(
                new NetworkStream(socket, false), true,
                remoteCertificateValidationCallback,
                null,
                EncryptionPolicy.RequireEncryption);
        }

        //TODO: Finish this
        //private X509Certificate UserCertificateSelectionCallback(
        //                object sender, 
        //                string targetHost, 
        //                X509CertificateCollection localCertificates, 
        //                X509Certificate remoteCertificate, 
        //                string[] acceptableIssuers)
        //{
        //    throw new NotImplementedException();
        //}

        public void Send(byte[] buffer)
        {
            throw new NotImplementedException();
        }

        public void Send(ArraySegment<byte> buffer)
        {
            throw new NotImplementedException();
        }

        public void Close()
        {
            throw new NotImplementedException();
        }


        private bool AllowEveryCertificate(
                        object sender, 
                        X509Certificate certificate, 
                        X509Chain chain, 
                        SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}
