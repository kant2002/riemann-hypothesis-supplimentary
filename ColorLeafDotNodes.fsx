open System
open System.IO
open System.Text.RegularExpressions

let args = Environment.GetCommandLineArgs()
let scriptArgs =
    if args.Length >= 2 && String.Equals(Path.GetFileName(args.[1]), "ColorLeafDotNodes.fsx", StringComparison.OrdinalIgnoreCase) then
        args |> Array.skip 2
    else
        args |> Array.skip 1

if scriptArgs.Length <> 1 then
    failwith "Usage: dotnet fsi ColorLeafDotNodes.fsx <lean-import.dot>"

let inputPath = Path.GetFullPath scriptArgs.[0]
if not (File.Exists inputPath) then
    failwithf "DOT file does not exist: %s" inputPath

let edgePattern =
    Regex(@"^(?<statement>\s*""(?<source>(?:\\.|[^""\\])+)""\s*->\s*""(?<target>(?:\\.|[^""\\])+)""(?:\s*\[[^\]]*\])*)(?<ending>\s*;\s*)$", RegexOptions.Compiled)

let nodePattern =
    Regex(@"^(?<statement>\s*""(?<node>(?:\\.|[^""\\])+)""(?:\s*\[[^\]]*\])*)(?<ending>\s*;\s*)$", RegexOptions.Compiled)

let oldEdgeColor = Regex(@"\s+\[color=""lightgreen""\]$", RegexOptions.Compiled)
let lines = File.ReadAllLines inputPath
let edges = lines |> Array.map edgePattern.Match
let nodes = lines |> Array.map nodePattern.Match
let sources =
    edges
    |> Array.choose (fun edge -> if edge.Success then Some edge.Groups.["source"].Value else None)
    |> Set.ofArray

let declaredNodes =
    nodes
    |> Array.choose (fun node -> if node.Success then Some node.Groups.["node"].Value else None)
    |> Set.ofArray

let implicitLeaves =
    edges
    |> Array.choose (fun edge ->
        if edge.Success then Some edge.Groups.["target"].Value else None)
    |> Set.ofArray
    |> Set.filter (fun target -> not (sources.Contains target) && not (declaredNodes.Contains target))

let closingLine =
    if Set.isEmpty implicitLeaves then None
    else
        match lines |> Array.tryFindIndexBack (fun line -> line.Trim() = "}") with
        | Some index -> Some index
        | None -> failwith "Cannot add implicit leaf nodes: graph closing brace not found"

let leafAttribute = "[style=\"filled\", fillcolor=\"lightgreen\"]"
let mutable markedCount = implicitLeaves.Count

for i in 0 .. lines.Length - 1 do
    let edge = edges.[i]
    let node = nodes.[i]
    if closingLine = Some i then
        for name in implicitLeaves do
            Console.WriteLine("  \"" + name + "\" " + leafAttribute + ";")

    if edge.Success then
        let statement = oldEdgeColor.Replace(edge.Groups.["statement"].Value, "")
        Console.WriteLine(statement + edge.Groups.["ending"].Value)
    elif node.Success && not (sources.Contains node.Groups.["node"].Value) then
        markedCount <- markedCount + 1
        let statement = node.Groups.["statement"].Value
        if statement.EndsWith(leafAttribute, StringComparison.Ordinal) then
            Console.WriteLine lines.[i]
        else
            Console.WriteLine(statement + " " + leafAttribute + node.Groups.["ending"].Value)
    else
        Console.WriteLine lines.[i]

eprintfn "Marked %d node(s) without outgoing edges in %s" markedCount inputPath
