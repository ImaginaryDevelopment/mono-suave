open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful
open System.Net

let (|ParseInt|_|)=
    function
    | null | "" -> None
    | x ->
        match System.Int32.TryParse x with
        | true, x -> Some x
        | _ -> None
        
// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.
let broadcast msg =
    System.Diagnostics.Trace.WriteLine <| sprintf "trace:%s" msg
    printfn "out:%s" msg
    eprintfn "e:%s" msg
    System.Diagnostics.Debug.WriteLine <| sprintf "debug:%s" msg
let logBroadcast msg =
    broadcast msg
    try
        System.IO.File.AppendAllLines("MonoSuave.log",[msg])
    with ex ->
        broadcast ex.Message
let routing =
    // print the incoming raw query, and don't match
    let diagnosticPart :WebPart =
        fun ctx ->
            let message = sprintf "Routing a request %s" ctx.request.rawQuery
            broadcast message
            never ctx
    choose[
        diagnosticPart
        path "/" >=> OK "hello"
    ]
[<EntryPoint>]
let main argv =
    printfn "args:%A" argv
    match argv |> List.ofArray with
    | ParseInt port :: _ -> uint16 port |> Choice1Of2
    | "update" :: filename :: runPath :: [] ->  Choice2Of2 (filename,runPath)
    | "update" :: _ -> failwithf "bad update args"
    | _ -> HttpBinding.defaults.socketBinding.port |> Choice1Of2
    //| _ -> 8080us
    |> function
        |Choice2Of2 (filename,runPath) -> Updater.updateMe filename runPath
        | Choice1Of2 port ->
            printfn "Starting up server"
            printfn "Binding also to port %A" port
            let add = HttpBinding.create HTTP (IPAddress.Parse "0.0.0.0") port

            let config = {defaultConfig with bindings=add::defaultConfig.bindings}
            let t = Updater.launchWatcher()
            startWebServer config routing
            eprintfn "Server finished?"
    0 // return an integer exit code
