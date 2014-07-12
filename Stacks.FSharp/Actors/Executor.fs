namespace Stacks.FSharp

open Stacks

[<AutoOpen>]
module Executors =

    type IExecutor with
        member this.RunAsync<'a>(wf: Async<'a>) = async {
            do! Async.SwitchToContext(this.Context)
            return! wf    
        }

        member this.PostAsync<'a>(code: unit -> 'a) = 
            this.PostTask(code)
            |> Async.AwaitTask

        member this.PostAsync(code: unit -> unit) = 
            this.PostTask(code)
            |> Async.AwaitTask
