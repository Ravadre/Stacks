namespace Stacks.FSharp

open Stacks

[<AutoOpen>]
module Executors =

    type IExecutor with
        /// <summary>
        /// Creates a wrapper async expression which will execute
        /// given one on executor's context. Returned expression
        /// is not started automatically, however, it will be rescheduled
        /// almost immediately after start, so Async.RunSynchronously or 
        /// Async.StartImmediate are recommended to start it.
        /// </summary>
        member this.RunAsync<'a>(wf: Async<'a>) = async {
            do! Async.SwitchToContext(this.Context)
            return! wf    
        }

        /// <summary>
        /// Runs given code on executor's context immediately. Result can be awaited with 
        /// Async.RunSynchronously
        /// </summary>
        member this.PostAsync<'a>(code: unit -> 'a) = 
            this.PostTask(code)
            |> Async.AwaitTask

        /// <summary>
        /// Runs given code on executor's context immediately. 
        /// Async.RunSynchronously can be used to await completion of code.
        /// </summary>
        member this.PostAsync(code: unit -> unit) = 
            this.PostTask(code)
            |> Async.AwaitTask
