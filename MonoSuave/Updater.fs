module Updater

open System.Diagnostics
open System.IO
open System.Reflection
open System.Resources

open Schema
open Helpers

module Impl =
    // watch for an update to be uploaded, launch update process, and trigger shutdown

    let findAllResources (asm:Assembly) =
        let resourceNames = asm.GetManifestResourceNames()
        broadcast "Getting resource names"
        resourceNames
        |> Seq.iter broadcast
        broadcast "finished resource names"


    let setOffline rootPath =
        // https://stackoverflow.com/questions/179556/will-app-offline-htm-stop-current-requests-or-just-new-requests
        // but the context this method will run under, won't be in the web app's app domain, it will have spawned another process, that should work right?
        try
            logBroadcast "writing app_offline.htm"
            let asm = Assembly.GetExecutingAssembly()
            let rm = ResourceManager("MonoSuave.MonoSuaveResources",asm)
            let html = rm.GetString("App_Offline.htm")
            let targetFullPath=Path.Combine(rootPath,"app_offline.htm")
            logBroadcast <| sprintf "writing offline to %s" targetFullPath
            File.WriteAllText(targetFullPath,html)
        with ex ->
            logBroadcast <| sprintf "appOfflineFailed %s" ex.Message

    let cleanFilesFiltered path fInclude =
        // swallow trying to clean out the old files
        try
            Directory.EnumerateFiles path
            |> Seq.filter fInclude 
            |> Seq.iter(fun x ->
                try
                    File.Delete x
                with ex ->
                    eprintfn "Failed to delete from target directory '%s':%s" x ex.Message
            )
        with ex ->
            logBroadcast <| sprintf "Failed to clear target directory:%s" ex.Message
            ()

    let cleanFiles path =
        cleanFilesFiltered path (fun _ -> true)
open Impl

module Serving =
    open System

    let createWatcher updatePath =
        // clean out all the update files, so the next updater can run
        if updatePath |> endsWithI ".zip" then
            Path.GetDirectoryName updatePath
        else updatePath
        |> fun updateDir ->
            cleanFilesFiltered updateDir (fun fn ->
                fn.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase)
                |> not
            )
        logBroadcast <| sprintf "going to watch %s" updatePath
        async{
            // dispose of the watcher as soon as the first event fires, we don't want our extraction to incur more events
            let! args =
                // https://msdn.microsoft.com/en-us/visualfsharpdocs/conceptual/async.awaitevent%5B%27del,%27t%5D-method-%5Bfsharp%5D?f=255&MSPPError=-2147217396
                use watch = new FileSystemWatcher(updatePath,NotifyFilter=NotifyFilters.LastWrite)
                watch.EnableRaisingEvents <- true
                watch.Changed.Add <| fun _ ->
                    logBroadcast <| sprintf "The file %s is changed" updatePath
                Async.AwaitEvent watch.Changed
            // we don't know if the event is triggered when the file starts writing, or when it finishes
            do! Async.Sleep 2000
            // unzip first to a place the next update can run
            System.IO.Compression.ZipFile.ExtractToDirectory(args.FullPath,Path.GetDirectoryName args.FullPath)
            return args.FullPath
        }
    let launch {AppFileSystemDirectoryPath=rootPath;UpdateFilePath=fn} =
        let cmd = "MonoSuave.exe"
        let args = sprintf "update \"%s\" \"%s\"" fn rootPath
        logBroadcast <| sprintf "Starting update '%s %s'" cmd args
        Process.Start(cmd,args) |> ignore
        logBroadcast "Process Started"


module Updating =
    open System

    // assumption process is running as updater and not in the same app domain as the w3wp invoked process
    let updateMe {AppFileSystemDirectoryPath=runDirectoryPath;UpdateFilePath=updateFilePath} =
        if not <| File.Exists updateFilePath then
            failwithf "Update called but file was not found:%s" updateFilePath
        // does this work in azure web jobs? who knows, let's try
        setOffline runDirectoryPath
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
            // properties of a System.Diagnostics.Process sometimes throw, isn't that lovely?
            try
                printfn "killing %s %i" old.ProcessName old.Id
            with _ -> ()
            try
                old.Kill()
            with ex ->
                eprintfn "Failed to kill:%s" ex.Message
        )

        cleanFiles runDirectoryPath
        // we can't unzip the new updater while the current updater is running

        // unzip to run
        System.IO.Compression.ZipFile.ExtractToDirectory(updateFilePath,runDirectoryPath)
        logBroadcast <| sprintf "done extracting to %s" runDirectoryPath
        // next request should auto-launch the new updated app thanks to w3wp, yes?
        ()

