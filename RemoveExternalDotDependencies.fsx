open System
open System.IO
open System.Text.RegularExpressions

let args = Environment.GetCommandLineArgs()
let scriptArgs =
    if args.Length >= 2 && String.Equals(Path.GetFileName(args.[1]), "RemoveExternalDotDependencies.fsx", StringComparison.OrdinalIgnoreCase) then
        args |> Array.skip 2
    else
        args |> Array.skip 1

if scriptArgs.Length <> 1 then
    failwith "Usage: dotnet fsi RemoveExternalDotDependencies.fsx <lean-import.dot>"

let inputPath = Path.GetFullPath scriptArgs.[0]
if not (File.Exists inputPath) then
    failwithf "DOT file does not exist: %s" inputPath

let nodePattern =
    Regex(@"^\s*""([^""]+)""\s*\[.*\]\s*;\s*$", RegexOptions.Compiled)

let edgePattern =
    Regex(@"^\s*""([^""]+)""\s*->\s*""([^""]+)""\s*(?:\[.*\]\s*)?;\s*$", RegexOptions.Compiled)

let isExcluded (name: string) =
    [| "Lean"; "Mathlib"; "Batteries" |]
    |> Array.exists (fun root ->
        name = root ||
        name.StartsWith(root + ".", StringComparison.Ordinal) ||
        name.StartsWith(root + "/", StringComparison.Ordinal))

let mutable removedNodes = 0
let mutable removedEdges = 0

for line in File.ReadLines inputPath do
    let edge = edgePattern.Match line
    let node = nodePattern.Match line
    if edge.Success && (isExcluded edge.Groups.[1].Value || isExcluded edge.Groups.[2].Value) then
        removedEdges <- removedEdges + 1
    elif node.Success && isExcluded node.Groups.[1].Value then
        removedNodes <- removedNodes + 1
    else
        Console.WriteLine line

eprintfn "Removed %d node(s) and %d edge(s) from %s" removedNodes removedEdges inputPath
