module EnvironmentHelpers
open System.IO

open Helpers

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


