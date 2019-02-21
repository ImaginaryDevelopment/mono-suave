module Serving
open System.IO

open Helpers
open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful

module Impl =

    let getLogs rootPath =
        let inline printErr x (ex:exn) = eprintfn "failed to read fileinfo for %A: %s" x ex.Message
        Directory.EnumerateFiles(rootPath,"*.log")
        |> Seq.mapSwallow printErr (fun fp ->
            let fi =FileInfo fp 
            fp,fi.LastWriteTime
        )
        |> Seq.sortBy snd
        |> Seq.mapSwallow printErr (fun (fp,dt) ->
            dt.ToString("yyyy.MM.dd.hh.mm"),File.ReadAllText fp
        )

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
        path "/logs" >=> (fun ctx -> getLogs rootPath |> Json.toJson |> ok |> fun f -> f ctx)
        path "/" >=> OK "hello"
    ]
