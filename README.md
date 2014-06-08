Stacks - Actor based networking library
===================================
--------------------------------------
[![Build status](https://ci.appveyor.com/api/projects/status/uxi69l39gcl63tsn)](https://ci.appveyor.com/project/Ravadre/stacks) [![NuGet](http://img.shields.io/nuget/v/Stacks.svg?style=flat)](http://www.nuget.org/packages/Stacks/)

Stacks is a small networking library. With Stacks it is very easy to create a high performance socket client or server without worrying about synchronization or threading. Stacks is highly configurable, features like tls/ssl, framing or serialization can be composed together easily.

Build
-----

Stacks can be build with Visual Studio 2013. Earlier versions should work as well.
Core functionality (actors and sockets) are contained in a single assembly. Serialization functionalities which require third party libraries are compiled into separate assemblies.

### Building on Mono (and Linux) ###

Building requires at least Mono 3.2, previous versions will crash during compilation.
```
wget https://nuget.org/nuget.exe
fsharpi Create-Mono.fsx
mono nuget.exe restore Stacks-Mono.sln
xbuild Stacks-Mono.sln
```


Concepts
--------
--------------------------

### Actors ###

Everything on Stacks uses lightweight implementation of actor model underneath. Actors in Stacks, instead of passing arbitrary messages, pass only one type of message -- `Action`. Passing `Action` to an actor means that it should be executed in it's context and that is should be synchronized with other messages.

Sample actor, which implements behavior for single message:

```cs
//One of the ways of defining an actor is to 
//inherit from Actor class
class Formatter : Actor
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

Copyright and License
---------------------
----------
Copyright 2014 Marcin Deptuła

Licensed under the [MIT License](/LICENSE)
