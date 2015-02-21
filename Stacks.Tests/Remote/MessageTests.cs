using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ProtoBuf;
using Stacks.Actors;
using Xunit;

namespace Stacks.Tests.Remote
{
    public class MessageTests
    {
        private IActorServerProxy server;
        private IMessageActor client;

        [Fact]
        public void Calling_method_should_call_it_on_server()
        {
            var impl = new MessageActor();

            Utils.CreateServerAndClient<MessageActor, IMessageActor>(impl, out server, out client);

            client.Ping().Wait();

            Assert.Equal(1, impl.PingsCalled);
        }

        [Fact]
        public void Calling_method_with_primitive_type_as_return_parameter_should_work_correctly()
        {
            Utils.CreateServerAndClient<MessageActor, IMessageActor>(out server, out client);

            var random = client.Random().Result;
        }

        [Fact]
        public void Calling_method_with_object_that_isnt_protocontract_should_return_failed_task()
        {
            Utils.CreateServerAndClient<MessageActor, IMessageActor>(out server, out client);

            Assert.Throws(typeof(InvalidOperationException), () =>
                {
                    var res = client.NotProtoContract(new InvalidData { X = 5 });
                    try
                    {
                        res.Wait();
                    }
                    catch (AggregateException exn)
                    {
                        throw exn.InnerException;
                    }
                });
        }

        [Fact]
        public void Calling_method_with_enumerable_of_proto_contract_data_should_be_accepted()
        {
            Utils.CreateServerAndClient<MessageActor, IMessageActor>(out server, out client);

            var res = client.PassValidData(new[]
                {
                    new ValidData { X = 5, Y = 6 },
                    new ValidData { X = 10, Y = 3 }
                }).Result.Select(r => r.Result);

            Assert.Equal(new[] { 11, 13 }, res);
        }

        [Fact]
        public void Multiple_parameters_should_be_serialized_properly()
        {
            Utils.CreateServerAndClient<MessageActor, IMessageActor>(out server, out client);

            client.ValidateMonotonic(4, 6, 6, 7, 8, 10, 123, 312, 312).Wait();
        }

        [Fact]
        public void Errors_on_server_side_should_propagate_exception_messages()
        {
            Utils.CreateServerAndClient<MessageActor, IMessageActor>(out server, out client);

            try
            {
                client.ValidateMonotonic(4, 6, 6, 7, 10, 8, 123, 312, 312).Wait();
            }
            catch (AggregateException exc)
            {
                Assert.Equal("Custom fail message", exc.InnerException.Message);
            }
        }

        [Fact]
        public void Errors_on_server_side_methods_should_not_disconnect_client()
        {
            Utils.CreateServerAndClient<MessageActor, IMessageActor>(out server, out client);

            try
            {
                client.ValidateMonotonic(4, 6, 6, 7, 10, 8, 123, 312, 312).Wait();
            }
            catch (AggregateException exc)
            {
                Assert.Equal("Custom fail message", exc.InnerException.Message);
            }

            client.ValidateMonotonic(4, 6, 6, 7, 8, 10, 123, 312, 312).Wait();
        }

        [Fact]
        public void Request_should_fail_if_server_goes_down_while_processing_an_event()
        {
            Utils.CreateServerAndClient<MessageActor, IMessageActor>(out server, out client);

            var addResult = client.LongRunningAdder(5, 6);
            Thread.Sleep(50);
            server.Stop();

            Assert.Throws(typeof(SocketException), () =>
                {
                    try
                    {
                        var res = addResult.Result;
                    }
                    catch (AggregateException exc)
                    {
                        throw exc.InnerException;
                    }
                });
        }

        [Fact]
        public async void If_first_parameter_is_IActorSession_it_should_be_filled_with_reference_to_an_client()
        {
            Utils.CreateServerAndClient<MessageActor, IMessageActor>(out server, out client);
            var client2 = ActorClientProxy.CreateActor<IMessageActor>("tcp://localhost:" + server.BindEndPoint.Port).Result;

            await client.PassDataWithClient(1);
            await client.PassDataWithClient(1);
            await client2.PassDataWithClient(2);
            await client.PassDataWithClient(1);
            await client2.PassDataWithClient(2);

        }

       

        [Fact]
        public async void Explicit_interface_implementation_should_correctly_map_methods_on_server_side()
        {
            IExplicitInterfaceActor client;
            Utils.CreateServerAndClient<ExplicitInterfaceActor, IExplicitInterfaceActor>(out server, out client);

            await client.Test();
        }

        [Fact]
        public async void Explicit_interface_implementation_should_correctly_map_properties_on_server_side()
        {
            var receivedValue = new ManualResetEventSlim();
            IExplicitInterfaceActor client;
            Utils.CreateServerAndClient<ExplicitInterfaceActor, IExplicitInterfaceActor>(out server, out client);

            await client.Test();

            client.Values.Subscribe(x => receivedValue.Set());

            Assert.True(receivedValue.Wait(500));
        }
    }

    public interface IExplicitInterfaceActor
    {
        Task Test();
        IObservable<long> Values { get; }
        IObservable<long> ValuesPublic { get; }
    }

    public class ExplicitInterfaceActor : Actor, IExplicitInterfaceActor
    {
        private IObservable<long> values;

        public ExplicitInterfaceActor()
        {
            values = Observable.Interval(TimeSpan.FromMilliseconds(50));
        }

        async Task IExplicitInterfaceActor.Test()
        {
            await Context;
        }

        IObservable<long> IExplicitInterfaceActor.Values
        {
            get { return values.AsObservable(); }
        }

        public IObservable<long> ValuesPublic
        {
            get { return values.AsObservable(); }
        }
    }

    public interface IMessageActor
    {
        Task Ping();
        Task<int> Random();
        Task NotProtoContract(InvalidData data);
        Task<IEnumerable<ValidDataResponse>> PassValidData(IEnumerable<ValidData> data);
        Task PassDataWithClient(int c);
        Task PassDataForContext(int c);
        Task StressTestSession(int c);
        Task ValidateMonotonic(int x, double y, float z, int k, long l, int m, int n, int i, int j);
        Task AssertActorSessionIsNull();

        Task<int> LongRunningAdder(int x, int y);
    }

    public class InvalidData
    {
        public int X { get; set; }
    }


    [ProtoContract]
    public class ValidData
    {
        [ProtoMember(1)]
        public int X { get; set; }
        [ProtoMember(2)]
        public int Y { get; set; }
    }

    [ProtoContract]
    public class ValidDataResponse
    {
        [ProtoMember(1)]
        public int Result { get; set; }
    }

    public class MessageActor : Actor, IMessageActor
    {
        private Random rng = new Random();
        private Dictionary<int, IFramedClient> clientCache = new Dictionary<int, IFramedClient>();

        private volatile int pingsCalled;

        public int PingsCalled { get { return pingsCalled; } }

        public async Task Ping()
        {
            await Context;

            pingsCalled++;
        }

        public async Task<int> Random()
        {
            await Context;

            return rng.Next(100);
        }

        public void Close()
        {
            throw new NotImplementedException();
        }


        public async Task NotProtoContract(InvalidData data)
        {
            await Context;
        }

        public async Task<IEnumerable<ValidDataResponse>> PassValidData(IEnumerable<ValidData> data)
        {
            await Context;

            return data.Select(d => new ValidDataResponse { Result = d.X + d.Y });
        }

        public async Task PassDataWithClient(int c)
        {
            await Context;
            await Task.Delay(1);

            var client = ActorSession.Current.Client;

            Assert.NotNull(client);

            if (clientCache.ContainsKey(c))
            {
                Assert.Equal(clientCache[c], client);
            }
            else
            {
                clientCache[c] = client;
            }
        }

        public async Task StressTestSession(int c)
        {
            await Context;
            await Task.Delay(10);

            var session = ActorSession.Current;
            Assert.NotNull(session);
            var client = session.Client;
            Assert.NotNull(client);

            if (clientCache.ContainsKey(c))
            {
                Assert.Equal(clientCache[c], client);
            }
            else
            {
                clientCache[c] = client;
            }
        }

        public async Task PassDataForContext(int c)
        {
            await Context;
            await Task.Delay(1);

            var session = ActorSession.Current;

            Assert.NotNull(session);

            var client = session.Client;

            Assert.NotNull(client);

            if (clientCache.ContainsKey(c))
            {
                Assert.Equal(clientCache[c], client);
            }
            else
            {
                clientCache[c] = client;
            }
        }

        public async Task ValidateMonotonic(int x, double y, float z, int k, long l, int m, int n, int i, int j)
        {
            await Context;

            try
            {
                Assert.True(y >= x);
                Assert.True(z >= y);
                Assert.True(k >= z);
                Assert.True(l >= k);
                Assert.True(m >= l);
                Assert.True(n >= m);
                Assert.True(i >= n);
                Assert.True(j >= i);
            }
            catch (Exception)
            {
                throw new Exception("Custom fail message");
            }
        }

        public async Task<int> LongRunningAdder(int x, int y)
        {
            await Context;
            await Task.Delay(5000);
            return x + y;
        }

        public async Task AssertActorSessionIsNull()
        {
            await Context;
            Assert.Null(ActorSession.Current);
        }
    }
}
