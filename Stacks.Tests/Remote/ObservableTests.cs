using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ProtoBuf;
using Stacks.Actors;
using Xunit;

namespace Stacks.Tests.Remote
{
    public class ObservableTests
    {
        private ObservableActorServer serverImpl;
        private IActorServerProxy server;
        private IObservableActor client;

        public ObservableTests()
        {
            serverImpl = new ObservableActorServer();
        }

        [Fact]
        public void Client_should_receive_integer_stream_from_server_with_observable()
        {
            Utils.CreateServerAndClient<ObservableActorServer, IObservableActor>(serverImpl, out server, out client);

            List<int> input = new List<int>() { 3, 1, 4, 1, 5 };
            List<int> output = new List<int>();

            client.IntStream.Subscribe(x => output.Add(x));

            serverImpl.RunIntStream(input);

            for (int i = 0; i < 10; ++i)
            {
                if (output.Count == input.Count) break;
                Thread.Sleep(100);
            }

            Assert.Equal(output, input);
        }

        [Fact]
        public void Client_should_receive_complex_data_stream_via_observable()
        {
            Utils.CreateServerAndClient<ObservableActorServer, IObservableActor>(serverImpl, out server, out client);

            var input = new List<ComplexData>()
            {
                new ComplexData { A = 5, X = 3.14 },
                new ComplexData { A = 3, X = 2.67 }
            };
            var output = new List<ComplexData>();

            client.ComplexStream.Subscribe(x => output.Add(x));

            serverImpl.RunComplexStream(input);

            for (int i = 0; i < 10; ++i)
            {
                if (output.Count == input.Count) break;
                Thread.Sleep(100);
            }

            Assert.Equal(output, input);
        }
    }


    [ProtoContract]
    public class ComplexData : IEquatable<ComplexData>
    {
        [ProtoMember(1)]
        public int A { get; set; }
        [ProtoMember(2)]
        public double X { get; set; }

        public bool Equals(ComplexData other)
        {
            return A.Equals(other.A) && X.Equals(other.X);
        }

        public override bool Equals(object obj)
        {
            var y = obj as ComplexData;
            if (y == null) return false;
            return this.Equals(y);
        }

        public override int GetHashCode()
        {
            return A.GetHashCode() ^ X.GetHashCode();
        }
    }

    public interface IObservableActor
    {
        IObservable<int> IntStream { get; }
        IObservable<ComplexData> ComplexStream { get; }
    }

    public class ObservableActorServer : IObservableActor
    {
        private Subject<int> intStream;
        private Subject<ComplexData> complexStream;

        public IObservable<int> IntStream
        {
            get { return intStream; }
        }

        public IObservable<ComplexData> ComplexStream
        {
            get { return complexStream; }
        }

        public ObservableActorServer()
        {
            intStream = new Subject<int>();
            complexStream = new Subject<ComplexData>();
        }

        public void RunIntStream(IEnumerable<int> data)
        {
            foreach (var x in data)
                intStream.OnNext(x);
        }

        public void RunComplexStream(IEnumerable<ComplexData> data)
        {
            foreach (var x in data)
                complexStream.OnNext(x);
        }
    }
}
