Stacks - Actor based networking library
===================================
--------------------------------------
[![Build status](https://ci.appveyor.com/api/projects/status/uxi69l39gcl63tsn/branch/develop?svg=true)](https://ci.appveyor.com/project/Ravadre/stacks/branch/develop) [![NuGet](http://img.shields.io/nuget/v/Stacks.svg?style=flat)](http://www.nuget.org/packages/Stacks/) [![NuGet](https://img.shields.io/nuget/vpre/Stacks.svg?style=flat)]

Stacks is a small networking and actor library. With Stacks it is very easy to create high performance socket client or server without worrying about synchronization or threading. Stacks is highly configurable, features like tls/ssl, framing or serialization can be composed together easily. 

Stacks introduce actor pattern in a completely new way by leveraging C#'s async/await pattern as well as F#'s Async workflows. 

Actors can be used for in-proc communication as well as for IPC. This makes Stacks a good choice when implementing system which must be able to scale, by introducing parallel and distributed techniques.

2.0 Update
----------

Version 2.0 brings some important changes to how actor model is implemented in Stacks. 
Up to version 1.3.4 local actors were implemented without any wrapper / proxy layer. This lightweight implementation
was conscious decision, however, it also imposed some limits on what framework can handle for user.
From version 2.0 onwards, similarly to how remote proxies are implemented, thin wrapper is generated and dynamically compiled
based on interface and actor implementation. This wrapper, with newly added `ActorSystem` will allow to further develop actor model in Stacks.
One example of what is possible using new mechanism is introduction of actor hierarchy, 
killing whole subtrees of actors when one of their ancestor encountered unhandled exception.

2.0 update is currently in alpha stage and as such, some changes might break an API.

Build
-----

Stacks can be build with Visual Studio 2013. Earlier versions should work as well.
Core functionality (actors and sockets) are contained in a single assembly. Serialization functionalities which require third party libraries are compiled into separate assemblies.

### Building on Mono (and Linux) (ver. 1.3.4) ###

Building requires at least Mono 3.2, previous versions will crash during compilation.
```
wget https://nuget.org/nuget.exe
fsharpi Create-Mono.fsx
mono nuget.exe restore Stacks-Mono.sln
xbuild Stacks-Mono.sln
```

> ver. 2.0.0 is not yet tested under Mono.

Concepts
--------
--------------------------

## Network ##


### Protocol layers ###

In Stacks, choosing socket behavior and features is done by composing different functionalities by chaining layers on top of themselves, just like on stack. For example, to enable framing one should create `FramedClient` on top of `SocketClient`. To enable ssl, `SslClient` should be inserted between those two layers.

Once layers are set up, only the highest layer can be used to interact with sockets, every event will be bubbled up from lower layers. Because everything is done in actor context, packet handling should not be processed directly in events. Instead, data should be passed for processing, for example to a different actor.

Sample usage of framed client:

```cs
// Server
server.Connected.Subscribe(c =>
    {
        serverClient = new FramedClient(c);
        
        // When received is called, bs will contain no more and no less
        // data than whole packet as sent from client.
        serverClient.Received.Subscribe(bs =>
            {
                var msg = encoding.GetString(bs.Array, bs.Offset, bs.Count);
                msg = "Hello, " + msg + "!";
                serverClient.SendPacket(encoding.GetBytes(msg));
            });
    });
server.Start();

// Client
client = new FramedClient(
            new SocketClient());

client.Received.Subscribe(bs =>
    {
        Console.WriteLine("Received: " + 
            encoding.GetString(bs.Array, bs.Offset, bs.Count));
    });

await client.Connect(new IPEndPoint(IPAddress.Loopback, serverPort));
client.SendPacket(encoding.GetBytes("Steve"));
```

### Message serialization ###

Stacks support message serialization using Protobuf or MessagePack. It's easy to add own serialization mechanism. Stacks handles dispatching received messages to appropriate handling functions by precompiling needed code, no reflection or by-hand casting is needed.

To react to incoming packets and deserialize them automatically, when creating a socket, `IStacksSerializer` implementation must be supplied, as well as `IMessageHandler`.

First one will be responsible for serialization and deserialization of packets.

Second one should contain methods which will be called when appropriate packets will arrive. Sample server message handler, which will respond to client with an answer looks like this:

```cs
public class ServerMessageHandler : Actor, IMessageHandler
{
    TemperatureService service;

    public ServerMessageHandler(TemperatureService service)
    {
        this.service = service;
    }

    // Every packet has its own id, which is resolved using following rules:
    // - if owning MessageClient was created without registering ids' imperatively
    //   it is assumed, that packets are decorated with StacksMessage attribute
    // - if owning MessageClient was created with imperatively set ids,
    //   each of handler's packets should be registered, otherwise, an exception is thrown.
    // Handler method should take 2 parameters:
    // IMessageClient - client which sent the packet
    // T - packet of type T, casting will be done automatically.
    public async void HandleTemperatureRequest(IMessageClient client, 
                                               TemperatureRequest request)
    {
        await Context;

        var temp = await service.GetTemperature(request.City);

        client.Send(new TemperatureResponse
        {
            City = request.City,
            Temperature = temp,
        });
    }
}
```

### Reactive Message Client ###

Using `IMessageHandler`, as shown in previous section is one of two ways of setting up message client easily. If one wants to leverage reactive programming pattern, there is a competitive implementation of `MessageClient` --- `ReactiveMessageClient<T>`.

To use it, first define an interface, which will define possible incoming packets, like in this example:

```cs
public interface IClientPacketHandler
{
    IObservable<Price> PriceChanged { get; }
    IObservable<ChartData> ChartUpdated { get; }
    // [...]
}
```

This allows `Stacks` to reason about what packet will be handled by the network client and the user will achieve strongly typed stream of deserialized packets. Once the interface is defined, one can create an instance of `ReactiveMessageClient<T>`:

```cs
client.Packets.PriceChanged.Subscribe(p =>
    {
        [...]
    });
// Reactive extensions compositions can be used
client.Packets.ChartUpdated
              .ObserveOn(otherContext) // rest of the computation 
                                       // is executed on different context.
              .Where( [...] )
              .Select( [...] )
              .Subscribe( p => 
                {
                    [...]
                });
```

Note that `IClientPacketHandler` interface is automatically implemented and exposed as a `Packets` property. Underneath, implementation is precompiled, so once class is in use, no reflection is used to handle incoming packets.

Also, no casting is required in user code, which makes the code more robust.

By default, handlers are called on socket's context, but it is very easy to reschedule computation onto different executor using `.ObserveOn` method. Notice, that all `Stacks` concepts like `IExecutor` or `ActorContext` support Reactive extension's scheduling patterns. 

## Actors ##

Everything on Stacks uses lightweight implementation of actor model underneath. Actors in Stacks, instead of passing arbitrary messages, pass only one type of message -- `Action`. Passing `Action` to an actor means that it should be executed in it's context and that is should be synchronized with other messages.

Sample actor, which implements behavior for single message:

```cs
class Formatter : Actor, IFormatter
{
    //Because every call has to be scheduled on
    //actor's context, answer will not be ready instantly,
    //so Task<T> is returned.
    public async Task<string> SayHello(string name)
    {
        //Code after this await will be run
        //on actor's context.
        await Context;

        //When an actor wants to reply to request, it just
        //has to return the response.
        return "Hello " + name + "!";
    }
}
```

Actor's `Context` is responsible for it's special behavior. Code to be executed can be `Enqueue`'d oraz `Post`'ed to `Context` and it will make sure that the code will be executed in correct order and that execution will be serial (even if it will be executed on different threads, no race condition or dirty reads can occur). Because `Context` is awaitable, instead of manually posting delegates to it, one can leverage async / await pattern to easily schedule any work on it. It is also legal to await other tasks inside actor methods, Stacks will automatically make sure that rest of the code is scheduled back to the right `Context`.

### F# ###
Stacks comes with wrapper which simplifies working with it in `F#`. After importing `Stacks.FSharp` it is possible to easily use
`Async` module to work with Stack's actors.

```fs
type Weather() = 
    let actor = ActorContext("Weather")
    let httpClient = new HttpClient()

    let xn s = XName.Get(s)

    member this.GetTemperature(city: string) =
        actor.RunAsync(async {
            let! response = sprintf "http://api.openweathermap.org/data/2.5/\
                                     weather?q=%s&mode=xml&units=metric" city
                            |> httpClient.GetStringAsync
                            |> Async.AwaitTask

            return double (XDocument.Parse(response)
                                    .Element(xn"current")
                                    .Element(xn"temperature")
                                    .Attribute(xn"value"))
        })
```
(See more samples in [Examples](/Examples) directory)

Just like with `C#`'s version, awaiting other tasks (with `let!` and `do!` keywords) is fully supported.

### Remoting ###
One of the Stacks biggest feature is a merge of its network and actor concepts. Through `ActorClientProxy` and `ActorServerProxy` it is possible to easily create an actor server and a proxy. Server is responsible for handling network from a proxy and execute incoming commands on an actor context, while proxy is an implementation which will redirect all methods to this server. 

As with previous implementations, Stacks leverages code emitting to avoid reflection calls during execution. 

Sample code which implements simple actor with only one method looks like this:

```cs
public interface ICalculatorActor
{
    Task<double> Add(double x, double y);
}

public class CalculatorActor: Actor, ICalculatorActor
{
    public async Task<double> Add(double x, double y)
    {
        await Context;
        
        return x + y;
    }
}

var actorServer = ActorServerProxy.Create<CalculatorActor>("tcp://*:4632");
ICalculatorActor calculator = ActorClientProxy.CreateActor<ICalculatorActor>("tcp://localhost:4632").Result;

var result = calculator.Add(5, 4).Result;
```

Notice few key things:

 * Compiled proxy implements `ICalculatorActor` just like 'normal' implementation, so there is no difference in using remote and local actors. For the user, it is *transparent* if actor is implemented in-proc or out-proc.
 * Local actors use `Task<T>` to reflect, that execution was scheduled on a different thread and the task is signaled when computation has finished. For remote actors, tasks covers whole process of packing parameters, sending them to the server, waiting for response and de-serializing it.
 * Remote actors obey the same rules that local do. Single actor instance is used per server, so there is no difference in keeping state etc., although of course introducing network communication introduces new problems.
 * Remote actors use `ProtoBuf` to serialize parameters and responses. Therefore, all collections and built-in types are supported (like string, int, double, IEnumerable<T>) out of the box, for custom types, they should be decorated with appropriate attributes. Consult [Sample](/Examples/RemoteActorsSample/Program.cs) for more information.
 * Underneath, message types and code for serialization and de-serialization is dynamically emitted, which means that reflection is not used to serialize messages every time.

Copyright and License
---------------------
----------
Copyright 2014 Marcin Deptuła

Licensed under the [MIT License](/LICENSE)
