open System
open System.Net

open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful

open Helpers
open Schema

type ServerCloseType =
    | ForUpdate of updateFilename:string*targetDirectory:string
    | Terminated
[<RequireQualifiedAccess>]
type RunMode =
    |Suave of Sockets.Port
    |Update of updateFilename:string*targetDirectory:string
    |Watch of targetDirectory:string

[<EntryPoint>]
let main argv =
    printfn "args:%A" argv
    let cd = Environment.CurrentDirectory
    printfn "Cd:%s" cd
    match argv |> List.ofArray with
    | ParseInt port :: _ -> uint16 port |> RunMode.Suave
    | "update" :: filename :: runPath :: [] ->  RunMode.Update(filename,runPath)
    | "update" :: _ -> //failwithf "bad update args"
        let basePath = @"D:\home\site\wwwroot"
        let updateLanding = IO.Path.Combine(basePath,"MonoSuave","bin","Release.zip")
        RunMode.Update(updateLanding,basePath)
    | "watch" :: path :: [] ->
        RunMode.Watch path
    | "watch" :: [] ->
        cd
        |> RunMode.Watch
    | _ -> RunMode.Suave HttpBinding.defaults.socketBinding.port
    //| _ -> 8080us
    |> function
        | RunMode.Update(updateFilename,targetDirectory) -> Updater.Updating.updateMe {AppFileSystemDirectoryPath=targetDirectory;UpdateFilePath=updateFilename }
        // only exists for testing/debugging
        | RunMode.Watch path ->
            async{
                let! tp = Updater.Serving.createWatcher path
                return tp
            }
            |> Async.RunSynchronously
            |> fun x ->
                broadcast <| sprintf "time to update %s" x
        | RunMode.Suave port ->
            printfn "Starting up server"
            printfn "Binding also to port %A" port
            let add = HttpBinding.create HTTP (IPAddress.Parse "0.0.0.0") port
            use cts = new System.Threading.CancellationTokenSource()

            let config = {defaultConfig with bindings=add::defaultConfig.bindings;cancellationToken=cts.Token}
            let t = Updater.Serving.createWatcher cd |> Async.map (fun fn -> ServerCloseType.ForUpdate(fn,cd)) |> Async.StartAsTask
            let _,stopped = startWebServerAsync config (cd |> Serving.routing)
            let watch,t =
                Async.AwaitTask t,
                stopped |> Async.map (fun _ -> ServerCloseType.Terminated)
            [ watch;t ]
            |> Async.Choice
            |> Async.RunSynchronously
            |> function
                |ServerCloseType.Terminated ->
                    ()
                |ServerCloseType.ForUpdate (fn,targetPath) ->
                    Updater.Serving.launch {AppFileSystemDirectoryPath=targetPath;UpdateFilePath=fn}
                    ()


            eprintfn "Server finished?"
    0 // return an integer exit code
