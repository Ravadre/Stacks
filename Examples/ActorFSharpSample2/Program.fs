
open System.Net.Http
open Stacks.Actors
open System.Xml.Linq
open Stacks


type Weather() = 
    let actor = ActorContext("Weather")
    let httpClient = new HttpClient()

    let xn s = XName.Get(s)

    member __.GetTemperature(city: string) =
        actor.MakeAsync(async {
            let! response = sprintf "http://api.openweathermap.org/data/2.5/\
                                     weather?q=%s&mode=xml&units=metric" city
                            |> httpClient.GetStringAsync
                            |> Async.AwaitTask

            return double (XDocument.Parse(response)
                                    .Element(xn"current")
                                    .Element(xn"temperature")
                                    .Attribute(xn"value"))
        })


[<EntryPoint>]
let main _ = 
    
    let weather = Weather()

    try
        let temp = weather.GetTemperature("Warsaw") |> Async.RunSynchronously
        printfn "Temperature in Warsaw, Poland: %.2f\u00B0C" temp
    with
    | _ -> printfn "Could not get temperature for Warsaw, Poland"

    0
