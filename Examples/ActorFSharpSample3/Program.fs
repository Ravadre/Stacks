open Stacks
open Stacks.Actors
open Stacks.FSharp

[<EntryPoint>]
let main argv = 

    let exec1 = ActionBlockExecutor("exec1")
    let exec2 = BusyWaitExecutor("exec2")
    
    
    let a = exec1.RunAsync(async {
        printfn "I am running on %s" (Executor.GetCurrentName())
    }) 

    let b = exec2.RunAsync(async {
        printfn "I am running on %s" (Executor.GetCurrentName())
    })

    Async.Parallel [a; b] 
    |> Async.RunSynchronously
    |> ignore

    System.Console.ReadKey() |> ignore
    
    0 
