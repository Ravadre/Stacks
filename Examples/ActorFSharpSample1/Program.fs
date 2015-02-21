open Stacks
open Stacks.Actors

type Formatter() =
    inherit Actor()

    member this.GetContext() = 
        base.GetActorSynchronizationContext()

    member this.SayHello(name: string) = 
        async {
            do! Async.SwitchToContext(this.GetContext())
            return sprintf "Hello %s!" name
        }
        
type Hello() = 

    let formatter = Formatter()

    member this.SayHelloToFriends(names: seq<string>) = 
        names
        |> Seq.iter (fun t -> printfn "%s" (Async.RunSynchronously <| formatter.SayHello(t)))


[<EntryPoint>]
let main argv = 
    
    let helloPrinter = Hello()

    helloPrinter.SayHelloToFriends( [ "Stan"; "Scott"; "John" ])
    
    0 
