namespace Stacks.Actors


[<AutoOpen>]
module Actors = 
    type Stacks.Actors.IActorContext with
    
        /// <summary>
        /// Creates a wrapper async expression which will execute
        /// given one on executor's context. Returned expression
        /// is not started automatically, however, it will be rescheduled
        /// almost immediately after start, so Async.RunSynchronously or 
        /// Async.StartImmediate are recommended to start it.
        /// </summary>
        member this.MakeAsync<'T>(wf: Async<'T>) = async {
            do! Async.SwitchToContext(this.SynchronizationContext)

            return! wf
        }
