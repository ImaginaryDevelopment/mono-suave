module Helpers
open System
open System.Threading

let (|ValueString|NonValueString|) =
    function
    | "" | null -> NonValueString
    | x when String.IsNullOrWhiteSpace x -> NonValueString
    | x -> ValueString x

let inline endsWithI d =
    function
    | "" | null -> false
    | x -> x.EndsWith(d, StringComparison.InvariantCultureIgnoreCase)
let (|ParseInt|_|)=
    function
    | null | "" -> None
    | x ->
        match System.Int32.TryParse x with
        | true, x -> Some x
        | _ -> None

let inline close1 f x = fun () -> f x

let broadcast msg =
    System.Diagnostics.Trace.WriteLine <| sprintf "trace:%s" msg
    printfn "out:%s" msg
    eprintfn "e:%s" msg
    System.Diagnostics.Debug.WriteLine <| sprintf "debug:%s" msg
let tryLog msg =
    try
        let target =
            let fn ="MonoSuave.log"
            let dn = "_diag"
            if IO.Directory.Exists dn  then
                IO.Path.Combine(dn,fn)
            else fn
        System.IO.File.AppendAllLines(target,[msg])
    with ex ->
        broadcast ex.Message
let logBroadcast msg =
    broadcast msg
    tryLog msg

module Seq =
    let mapSwallow fUnhappy f =
        Seq.choose(fun x ->
            try
                f x
                |> Some
            with ex ->
                fUnhappy x ex
                None
        )

module Choice =
    let inline ofCatch1 f x =
        try
            f x
            |> Choice1Of2
        with ex ->
            Choice2Of2 ex
    let inline ofCatch f = ofCatch1 f ()


type Microsoft.FSharp.Control.Async with
    // http://www.fssnip.net/6D/title/Return-the-first-result-using-AsyncChoice
    /// Takes several asynchronous workflows and returns 
    /// the result of the first workflow that successfuly completes
    static member Choice(workflows) = 
        Async.FromContinuations(fun (cont, _, _) ->
          let cts = new CancellationTokenSource()
          let completed = ref false
          let lockObj = new obj()
          let synchronized f = lock lockObj f

          /// Called when a result is available - the function uses locks
          /// to make sure that it calls the continuation only once
          let completeOnce res =
            let run =
              synchronized(fun () ->
                if completed.Value then false
                else completed := true; true)
            if run then cont res

          /// Workflow that will be started for each argument - run the 
          /// operation, cancel pending workflows and then return result
          let runWorkflow workflow = async {
            let! res = workflow
            cts.Cancel()
            completeOnce res }

          // Start all workflows using cancellation token
          for work in workflows do
            Async.Start(runWorkflow work, cts.Token) )