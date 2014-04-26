using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stacks
{
    public class SslClient : IRawByteClient
    {
        public event Action<ArraySegment<byte>> Received;
        public event Action<int> Sent;
        public event Action<Exception> Disconnected;

        private IRawByteClient client;
        private Socket socket;
        private SslStream sslStream;

        public SslClient(IRawByteClient client)
            : this(client, allowEveryCertificate: false)
        { }

        public SslClient(IRawByteClient client, bool allowEveryCertificate)
        {
            if (allowEveryCertificate)
                Initialize(client, AllowEveryCertificate);
            else
                Initialize(client, null);
        }

        public SslClient(IRawByteClient client, 
                         RemoteCertificateValidationCallback remoteCertificateValidationCallback)
        {
            Initialize(client, remoteCertificateValidationCallback);
        }

        private void Initialize(IRawByteClient client,
                                RemoteCertificateValidationCallback remoteCertificateValidationCallback)
        {
            this.client = client;
            this.sslStream = new SslStream(
                new RawByteClientStream(this.client), 
                true,
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
            this.sslStream.Write(buffer);
        }

        public void Send(ArraySegment<byte> buffer)
        {
            this.sslStream.Write(buffer.Array, buffer.Offset, buffer.Count);
        }

        public void Close()
        {
            this.client.Close();
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

    public class RawByteClientStream : Stream
    {
        private IRawByteClient client;
        ResizableCyclicBuffer buffer;

        public RawByteClientStream(IRawByteClient client)
        {
            this.client = client;
            this.buffer = new ResizableCyclicBuffer(4096);
            this.client.Received += DataReceived;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override void Flush()
        {
           
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            lock(this.buffer)
            {
                var segment = new ArraySegment<byte>(buffer, offset, count);
                var read = this.buffer.ReadRawBytes(segment);

                return read;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            client.Send(new ArraySegment<byte>(buffer, offset, count));
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        private void DataReceived(ArraySegment<byte> data)
        {
            lock(this.buffer)
            {
                this.buffer.AddData(data);
            }
        }

    }
}
