module EnvironmentHelpers
open System.IO

open Helpers

// tries to open a file even if locked
let getRead fn =
    use fs = new FileStream(fn, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
    use sr = new StreamReader(fs)
    let content = sr.ReadToEnd()
    content

let getLogs rootPath =
    let inline printErr x (ex:exn) = eprintfn "failed to read fileinfo for %A: %s" x ex.Message
    Directory.EnumerateFiles(rootPath,"*.log")
    |> Seq.mapSwallow printErr (fun fp ->
        let fi =FileInfo fp 
        fp,fi.LastWriteTime
    )
    |> Seq.sortBy snd
    |> Seq.mapSwallow printErr (fun (fp,dt) ->
        dt.ToString("yyyy.MM.dd.hh.mm"),getRead fp
    )


