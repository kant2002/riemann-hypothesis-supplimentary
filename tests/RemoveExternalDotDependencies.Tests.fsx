open System
open System.Diagnostics
open System.IO

let inputPath = Path.GetTempFileName()
let scriptPath = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", "RemoveExternalDotDependencies.fsx"))

try
    File.WriteAllLines(inputPath, [|
        "digraph G {"
        "  \"Lean\" [label=\"Lean\"];"
        "  \"Mathlib/Data\" [label=\"Mathlib/Data\"];"
        "  \"Batteries.Tactic\" [label=\"Batteries.Tactic\"];"
        "  \"LeanExtra\" [label=\"LeanExtra\"];"
        "  \"Local\" -> \"Lean\";"
        "  \"Local\" -> \"Mathlib.Data\" [color=\"red\"];"
        "  \"Batteries/Tactic\" -> \"Local\";"
        "  \"Local\" -> \"LeanExtra\";"
        "  \"MathlibExtra\" -> \"BatteriesExtra\";"
        "}"
    |])

    let startInfo = ProcessStartInfo("dotnet")
    startInfo.ArgumentList.Add("fsi")
    startInfo.ArgumentList.Add(scriptPath)
    startInfo.ArgumentList.Add(inputPath)
    startInfo.RedirectStandardOutput <- true
    startInfo.RedirectStandardError <- true
    use childProcess = Process.Start(startInfo)
    let output = childProcess.StandardOutput.ReadToEnd()
    let error = childProcess.StandardError.ReadToEnd()
    childProcess.WaitForExit()

    let expected =
        String.Join(Environment.NewLine, [|
            "digraph G {"
            "  \"LeanExtra\" [label=\"LeanExtra\"];"
            "  \"Local\" -> \"LeanExtra\";"
            "  \"MathlibExtra\" -> \"BatteriesExtra\";"
            "}"
            ""
        |])

    if childProcess.ExitCode <> 0 then failwithf "Filter failed: %s" error
    if output <> expected then failwithf "Unexpected DOT output:\n%s" output
    if not (error.Contains("Removed 3 node(s) and 3 edge(s)")) then
        failwithf "Unexpected filter summary: %s" error

    printfn "External dependency filter test passed."
finally
    File.Delete(inputPath)
