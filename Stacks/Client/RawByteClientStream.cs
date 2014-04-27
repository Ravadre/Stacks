using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stacks
{
    public class RawByteClientStream : Stream
    {
        private IRawByteClient client;
        ResizableCyclicBuffer buffer;
        private ManualResetEventSlim hasDataEvent;
        private bool disposed;

        public RawByteClientStream(IRawByteClient client)
        {
            this.disposed = false;
            this.hasDataEvent = new ManualResetEventSlim();
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
            this.hasDataEvent.Wait();

            if (this.disposed)
                throw new ObjectDisposedException("Stream");

            lock (this.buffer)
            {
                var segment = new ArraySegment<byte>(buffer, offset, count);
                var read = this.buffer.ReadRawBytes(segment);

                if (this.buffer.Count == 0)
                    this.hasDataEvent.Reset();

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
            lock (this.buffer)
            {
                this.buffer.AddData(data);
                this.hasDataEvent.Set();
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            try
            {
                disposed = true;
                this.hasDataEvent.Set();
            }
            catch { }
        }

    }
}
