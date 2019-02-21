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
            broadcast message
            never ctx
open Impl

let routing rootPath =
    choose [
        diagnosticPart
        // force deferral of method run, instead of closing over the result at the time of routing init
        path "/logs" >=> (fun ctx -> EnvironmentHelpers.getLogs rootPath |> Json.toJson |> ok |> fun f -> f ctx)
        path "/" >=> OK "hello"
    ]
