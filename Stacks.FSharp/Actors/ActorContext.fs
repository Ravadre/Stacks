namespace Stacks.FSharp


[<AutoOpen>]
module Actors = 
    type Stacks.Actors.ActorContext with
    
        member this.RunAsync<'T>(wf: Async<'T>) = async {
            do! Async.SwitchToContext(this.Context)

            return! wf
        }
