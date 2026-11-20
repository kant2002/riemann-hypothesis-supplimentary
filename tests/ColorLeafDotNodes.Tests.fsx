open System
open System.Diagnostics
open System.IO

let inputPath = Path.GetTempFileName()
let scriptPath = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", "ColorLeafDotNodes.fsx"))

let runFilter () =
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
    if childProcess.ExitCode <> 0 then failwithf "Filter failed: %s" error
    output, error

try
    File.WriteAllLines(inputPath, [|
        "digraph G {"
        "  \"A\" -> \"B\";"
        "  \"A\" -> \"C\" [label=\"leaf\", color=\"red\"] [color=\"lightgreen\"];"
        "  \"B\" -> \"D\" [color=\"lightgreen\"];"
        "  \"B\" -> \"B\";"
        "  \"D\" [label=\"D\"];"
        "  \"Isolated\" [label=\"Isolated\", fillcolor=\"white\"];"
        "}"
    |])

    let expected =
        String.Join(Environment.NewLine, [|
            "digraph G {"
            "  \"A\" -> \"B\";"
            "  \"A\" -> \"C\" [label=\"leaf\", color=\"red\"];"
            "  \"B\" -> \"D\";"
            "  \"B\" -> \"B\";"
            "  \"D\" [label=\"D\"] [style=\"filled\", fillcolor=\"lightgreen\"];"
            "  \"Isolated\" [label=\"Isolated\", fillcolor=\"white\"] [style=\"filled\", fillcolor=\"lightgreen\"];"
            "  \"C\" [style=\"filled\", fillcolor=\"lightgreen\"];"
            "}"
            ""
        |])

    let output, error = runFilter ()
    if output <> expected then failwithf "Unexpected DOT output:\n%s" output
    if not (error.Contains("Marked 3 node(s)")) then failwithf "Unexpected summary: %s" error

    File.WriteAllText(inputPath, output)
    let repeatedOutput, _ = runFilter ()
    if repeatedOutput <> expected then failwithf "Filter is not idempotent:\n%s" repeatedOutput
    printfn "Leaf node coloring test passed."
finally
    File.Delete(inputPath)
