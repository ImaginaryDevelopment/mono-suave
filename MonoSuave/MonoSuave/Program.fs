open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful


// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.

let routing =
    choose[
        path "/" >=> OK "hello"
    ]
[<EntryPoint>]
let main argv =
    printfn "%A" argv
    0 // return an integer exit code
