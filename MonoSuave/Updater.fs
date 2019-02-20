module Updater

open System.Diagnostics
open System.IO

// watch for an update to be uploaded, launch update process, and trigger shutdown
let launchWatcher() =
    ()
// process is running as updater
let updateMe updatePath runPath=
    let me = Process.GetCurrentProcess()
    let others =
        Process.GetProcessesByName("MonoSuave.exe")
        |> List.ofArray
    let w3wps =
        Process.GetProcessesByName("w3wp.exe")
        |> List.ofArray
    others
    |> List.filter(fun x -> x.Id <> me.Id)
    |> List.append w3wps
    |> List.iter(fun old ->
        try
            printfn "killing %s %i" old.ProcessName old.Id
        with _ -> ()
        try
            old.Kill()
        with ex ->
            eprintfn "Failed to kill:%s" ex.Message
    )
    // swallow trying to clean out the old files
    try
        Directory.EnumerateFiles(runPath)
        |> Seq.iter(fun x ->
            try
                File.Delete x
            with ex ->
                eprintfn "Failed to delete from target directory '%s':%s" x ex.Message
        )
    with ex ->
        eprintfn "Failed to clear target directory:%s" ex.Message
        ()
    // unzip first to a place the update can run
    System.IO.Compression.ZipFile.ExtractToDirectory(updatePath,".")
    // unzip to run
    System.IO.Compression.ZipFile.ExtractToDirectory(updatePath,runPath)
    ()

