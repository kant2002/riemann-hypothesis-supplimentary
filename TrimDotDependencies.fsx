open System
open System.IO
open System.Text.RegularExpressions

let args = Environment.GetCommandLineArgs()
let scriptArgs =
    if args.Length >= 2 && String.Equals(Path.GetFileName(args.[1]), "TrimDotDependencies.fsx", StringComparison.OrdinalIgnoreCase) then
        args |> Array.skip 2
    else
        args |> Array.skip 1

if scriptArgs.Length = 0 then
    failwith "Usage: dotnet fsi src/TrimDotDependencies.fsx <lean-import.dot>"

let inputPath = Path.GetFullPath scriptArgs.[0]
if not (File.Exists inputPath) then
    failwithf "DOT file does not exist: %s" inputPath

let edgePattern =
    Regex("^\\s*\"([^\"]+)\"\\s*->\\s*\"([^\"]+)\"\\s*;\\s*$", RegexOptions.Compiled)

let isNested (source: string) (target: string) =
    let normalize (value: string) =
        value.Replace('\\', '/').Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries)

    let sourceParts = normalize source
    let targetParts = normalize target
    let sharedPrefixLength =
        Seq.zip sourceParts targetParts
        |> Seq.takeWhile (fun (a, b) -> a = b)
        |> Seq.length

    sharedPrefixLength > 0 &&
    (sharedPrefixLength = sourceParts.Length || sharedPrefixLength = targetParts.Length)

let mutable removedCount = 0

for line in File.ReadLines inputPath do
    let edge = edgePattern.Match line
    if edge.Success then
        let source = edge.Groups.[1].Value
        let target = edge.Groups.[2].Value
        if isNested source target then
            removedCount <- removedCount + 1
        else
            Console.WriteLine line
    else
        Console.WriteLine line

eprintfn "Removed %d redundant edge(s) from %s" removedCount inputPath
