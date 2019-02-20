﻿open System
open System.Diagnostics
open System.IO
open System.Net.Http
open System.Text

// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.
let rec search basePath =
    printfn "Starting search"
    seq{
        yield! Directory.EnumerateFiles(basePath,"*.zip")
        yield! (
                let items =
                    Directory.GetDirectories basePath
                    |> Seq.collect search
                items
        )
    }


let expand x = Environment.ExpandEnvironmentVariables x
//System.IO.Compression.ZipFile.ExtractToDirectory(zipFilePath,"unzipped")
let locateZip () =

    let zipPath = expand "%APPVEYOR_BUILD_FOLDER%" // @"c:\projects\mono-suave\MonoSuave\bin"
    printfn "Starting locate with base:%s" zipPath
    let zipFilename = "Release.zip"
    if not <| Directory.Exists zipPath then
        let fullPath = Path.GetFullPath zipPath
        // maybe eprintfn doesn't show up
        printfn "zip not found at %s" fullPath
        eprintfn "zip not found at %s" fullPath
        None
    else
        let zipFilePath = Path.Combine(zipPath,zipFilename)
        if not <| File.Exists zipFilePath then
            printfn "File not found at %s" zipFilePath
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
        |> Option.map(fun zp ->
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
                return if r.IsSuccessStatusCode then 0 else 1
            }
            |> Async.RunSynchronously
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
    printfn "MonoSuave.Deploy running"
    printfn "%A" argv
    appveyorRest()
    //0 // return an integer exit code
