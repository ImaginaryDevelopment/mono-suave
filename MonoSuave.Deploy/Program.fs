open System
open System.Diagnostics
open System.IO
open System.Net.Http
open System.Text
open System.IO.Compression

let expand x = Environment.ExpandEnvironmentVariables x
let baseBuildPath = expand "%APPVEYOR_BUILD_FOLDER%" 
let prettifySize (i:int64) =
    let sizes = ["B";"KB";"MB";"GB";"TB"]
    
    (i,0) |> Seq.unfold(fun (l,order) ->
        if l < 1024L && order = 0 then
            let result =(l,sizes.[0])
            Some(result,(0L,sizes.Length))
        elif l >= 1024L && order < sizes.Length then
            let value = l/1024L
            let o = order + 1
            let result =(value,sizes.[o])
            Some (result,(value,o))
        else None
    )
    |> Seq.tryLast
    |> function
        |None -> sprintf "%i%s" i sizes.[0]
        |Some(rem,o) -> sprintf "%i%s" rem o

let rec search basePath =
    printfn "Starting in search %s" basePath
    seq{
        yield! Directory.EnumerateFiles(basePath,"*.zip")
        yield! (
                let items =
                    Directory.GetDirectories basePath
                    |> Seq.collect search
                items
        )
    }


let zipItManually() =
    let binaryPath = Path.Combine(baseBuildPath,@"MonoSuave\bin\Release")
    let target = Path.Combine(baseBuildPath,"Zipped.zip")
    ZipFile.CreateFromDirectory(binaryPath,target)
    FileInfo(target).Length
    |> prettifySize
    |> printfn "Created zip with size %s" 

    Some target
let locateZip () =

    let zipPath = baseBuildPath // @"c:\projects\mono-suave\MonoSuave\bin"
    //printfn "Starting locate with base:%s" zipPath
    let zipFilename = "Release.zip"
    if not <| Directory.Exists zipPath then
        let fullPath = Path.GetFullPath zipPath
        // maybe eprintfn doesn't show up
        eprintfn "zip not found at %s" fullPath
        None
    else
        let zipFilePath = Path.Combine(zipPath,@"MonoSuave\bin",zipFilename)
        if not <| File.Exists zipFilePath then
            eprintfn "File not found at %s" zipFilePath
            search zipPath
            |> Seq.tryFind(fun zip ->
                printfn "Found a zip at %s" zip
                Path.GetFileName zip = zipFilename
            )
        else
            Some zipFilePath
let (|ValueString|NonValueString|) =
    function
    | null | "" -> NonValueString
    | x when String.IsNullOrWhiteSpace x -> NonValueString
    | x -> ValueString x


let appveyorRest() =
    match expand "%restuser%", expand "%restpwd%" with
    | ValueString u, ValueString p ->
        locateZip()
        |> function
            |Some x -> Some x
            | None -> zipItManually()
        |> Option.map(fun zp ->
            printfn "Starting upload"
            async{
                let bytes = File.ReadAllBytes(zp)
                use form = new MultipartFormDataContent()
                use bac = new ByteArrayContent(bytes,0,bytes.Length)
                form.Add(bac,"myUpdatePackage",Path.GetFileName(zp))

                use wc = new HttpClient()
                let encodeAuth u p =
                    sprintf "%s:%s" u p
                    |> Encoding.ASCII.GetBytes
                    |> Convert.ToBase64String
                wc.DefaultRequestHeaders.Authorization <- System.Net.Http.Headers.AuthenticationHeaderValue("Basic", encodeAuth u p)
                let! r = Async.AwaitTask <| wc.PostAsync("https://imaginarysuave.scm.azurewebsites.net/api/zipdeploy",form)
                printfn "Upload completed with %A:%s" r.StatusCode r.ReasonPhrase
                if r.IsSuccessStatusCode then
                    let! x = Async.AwaitTask <| r.Content.ReadAsStringAsync()
                    printfn "----------------------"
                    printfn "Response:%s" x
                    printfn "----------------------"
                    return 0
                else return 1
            }
            |> Async.RunSynchronously
            |> fun result ->
                printfn "Async finished"
                result
        )
        |> Option.defaultValue 2
    | NonValueString, NonValueString ->
        eprintfn "Rest: no username or password found"
        3
    | ValueString _, _ ->
        eprintfn "Rest: Username found, but no password"
        4
    | NonValueString, ValueString _ ->
        eprintfn "Rest: No Username found, but found password"
        5

[<EntryPoint>]
let main argv = 
    printfn "----------------------"
    printfn "----------------------"
    printfn "MonoSuave.Deploy running"
    printfn "args:%A" argv
    appveyorRest()
    |> fun result ->
        printfn "Rest returned %i" result
        printfn "----------------------"
        printfn "----------------------"
    0
    //0 // return an integer exit code
