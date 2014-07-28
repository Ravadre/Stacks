open RestSharp
open Stacks
open Stacks.Actors
open Stacks.FSharp
open Newtonsoft.Json
open Newtonsoft.Json.Linq

let GetRepositories(user: string) = async {
        let c = new RestClient("https://api.github.com/")

        let! resp = c.ExecuteGetTaskAsync(RestRequest("users/" + user + "/repos"))
                    |> Async.AwaitTask
        
        return JsonConvert.DeserializeObject(resp.Content) :?> JArray
               |> Seq.map(fun r -> r.["name"].Value<string>())
               |> Seq.toArray
    }

let GetStargazers user repo = async {
        let c = new RestClient("https://api.github.com/")

        let! resp = c.ExecuteGetTaskAsync(RestRequest(sprintf "repos/%s/%s/stargazers" user repo))
                    |> Async.AwaitTask

        return JsonConvert.DeserializeObject(resp.Content) :?> JArray
               |> Seq.map(fun r -> r.["login"].Value<string>())
               |> Seq.toArray
    }

// This sample shows how to use executor to run an async block
// All features, like Async.Parallel can be used, and user code 
// will run on executor's context, which can be in this context
// assumed to be an actor. This also means, that external tasks
// can be awaited inside async block on exector.
//
// Sample rules apply to actor contexts and actors, although 
// using Actor class is discouraged in F#, because of caveeats
// with inheritance. 
// Instead, composition with ActorContext is a way to go
    
[<EntryPoint>]
let main argv = 

    let exec = ActionBlockExecutor("main")
   
    //RunAsync creates new async block, which will run given one
    //on executor's context. Whol block can be awaited synchronously
    //with Async.RunSynchronously or asynchronously, with Async.StartImmediate.
    exec.RunAsync(async {
        printfn "Running on executor: %s" (Executor.GetCurrentName())
    
        let! repos = GetRepositories "Ravadre"

        printfn "Still running on executor: %s" (Executor.GetCurrentName())
        printfn ""            
        printfn "Repositories:"
        printfn "%s" (System.String.Join(", ", repos))

        let! stargazers = repos
                            |> Seq.map(fun r -> GetStargazers "Ravadre" r)
                            |> Async.Parallel

        let stargazers = stargazers
                            |> Array.collect id
                            |> Set.ofArray
        
        printfn ""            
        printfn "Still running on executor: %s" (Executor.GetCurrentName())
        printfn ""            
        printfn "Stargazers:"
        printfn "%s" (System.String.Join(", ", stargazers))
    })
    |> Async.RunSynchronously

    0 
