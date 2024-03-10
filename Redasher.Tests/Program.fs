module Program =
    open Expecto
    open Redasher.Tests

    [<EntryPoint>]
    let main args =
         [datasourceLoadTests; jobLoadTests; queryResultLoadTests]
         |> List.map ( runTestsWithCLIArgs [] args)
         |> ignore
         0