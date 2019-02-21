module Serving
open System.IO

open Helpers
open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful

module Impl =

    // print the incoming raw query, and don't match
    let diagnosticPart :WebPart =
        fun ctx ->
            let message = sprintf "Routing a request %s" ctx.request.rawQuery
            logBroadcast message
            never ctx
open Impl
open System.Threading.Tasks

let routing rootPath =
    choose [
        diagnosticPart
        // force deferral of method run, instead of closing over the result at the time of routing init
        path "/logs" >=> (fun ctx ->
                async{
                    let t = Task.Run(System.Func<_>(fun () ->  EnvironmentHelpers.getLogs rootPath |> Json.toJson |> ok |> fun f -> f ctx))
                    let! value = Async.AwaitTask(t)
                    return! value
                }
        )

        path "/" >=> OK "hello"
    ]
