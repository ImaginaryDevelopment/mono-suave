open System
open System.Diagnostics
open System.IO

// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.
let appveyorRest() =
    let zipPath = "MonoSuave\bin\Release"
    if not <| Directory.Exists zipPath then
        let fullPath = Path.GetFullPath zipPath
        eprintfn "zip not found at %s" fullPath
        1
    else
        let zipFilePath = Path.Combine(zipPath,"Release.zip")
        System.IO.Compression.ZipFile.ExtractToDirectory(zipFilePath,"unzipped")
        0
[<EntryPoint>]
let main argv = 
    printfn "MonoSuave.Deploy running"
    printfn "%A" argv
    appveyorRest()
    //0 // return an integer exit code
