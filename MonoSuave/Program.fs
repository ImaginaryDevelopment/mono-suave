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
    |Fetch of uri:string
    |Type of fnOpt:string

[<EntryPoint>]
let main argv =
    printfn "args:%A" argv
    let cd = Environment.CurrentDirectory
    printfn "Cd:%s" cd
    match argv |> List.ofArray with
    | ParseInt port :: _ -> uint16 port |> RunMode.Suave
    | "Type" :: x :: [] ->
        RunMode.Type x
    | "Type" :: [] ->
        RunMode.Type null
    | "update" :: filename :: runPath :: [] ->  RunMode.Update(filename,runPath)
    | "update" :: _ -> //failwithf "bad update args"
        let basePath = @"D:\home\site\wwwroot"
        let updateLanding = IO.Path.Combine(basePath,"MonoSuave","bin","Release.zip")
        RunMode.Update(updateLanding,basePath)
    | "watch" :: path :: [] ->
        RunMode.Watch path
    | "fetch" :: path :: [] ->
        RunMode.Fetch path
    | "watch" :: [] ->
        cd
        |> RunMode.Watch
    | _ -> RunMode.Suave HttpBinding.defaults.socketBinding.port
    //| _ -> 8080us
    |> function
        | RunMode.Type (ValueString fn) ->
            IO.File.ReadAllText fn
            |> printfn "%s"
        | RunMode.Type _ ->
            printfn "Trying to print all logs"
            EnvironmentHelpers.getLogs cd
            |> Seq.iter(fun (dt,text) ->
                printfn ""
                printfn "LogDt:%A" dt
                printfn ""
                printfn "----------------------"
                printfn "%s" text
                printfn "----------------------"
            )

        | RunMode.Update(updateFilename,targetDirectory) -> Updater.Updating.updateMe {AppFileSystemDirectoryPath=targetDirectory;UpdateFilePath=updateFilename }
        | RunMode.Fetch path ->
            let path = path.Trim('"').Trim('\'')
            async {
                use wc = new System.Net.Http.HttpClient()
                let! result = wc.GetStringAsync(path)
                return result
            }
            |> Async.RunSynchronously
            |> printfn "FetchResults:\r\n%s"
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
            tryLog <| sprintf "Started on port:%i" port
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
                |ServerCloseType.Terminated -> ()
                |ServerCloseType.ForUpdate (fn,targetPath) ->
                    Updater.Serving.launch {AppFileSystemDirectoryPath=targetPath;UpdateFilePath=fn}
                    ()


            eprintfn "Server finished?"
    0 // return an integer exit code
