open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful


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
    printfn "%A" argv
    printfn "Starting up server"
    startWebServer defaultConfig routing
    eprintfn "Server finished?"
    0 // return an integer exit code
