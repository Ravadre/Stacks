open Stacks
open Stacks.Actors

type IFormatter = 
    abstract member SayHello: string -> Async<string>

type Formatter() =
    inherit Actor()

    member this.GetContext() = 
        base.GetActorSynchronizationContext()

    interface IFormatter with
        member this.SayHello(name: string) = 
            async {
                do! Async.SwitchToContext(this.GetContext())
                return sprintf "Hello %s!" name
            }
        
type Hello() = 

    let formatter = ActorSystem.Default.CreateActor<Formatter>() :?> IFormatter

    member this.SayHelloToFriends(names: seq<string>) = 
        names
        |> Seq.iter (fun t -> printfn "%s" (Async.RunSynchronously <| formatter.SayHello(t)))


[<EntryPoint>]
let main argv = 
    
    let helloPrinter = Hello()

    helloPrinter.SayHelloToFriends( [ "Stan"; "Scott"; "John" ])
    
    0 
