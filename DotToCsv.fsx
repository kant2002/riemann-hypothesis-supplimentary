open System
open System.IO
open System.Text
open System.Text.RegularExpressions

let args = Environment.GetCommandLineArgs()
let scriptArgs =
    if args.Length >= 2 && String.Equals(Path.GetFileName(args.[1]), "DotToCsv.fsx", StringComparison.OrdinalIgnoreCase) then
        args |> Array.skip 2
    else
        args |> Array.skip 1

if scriptArgs.Length = 0 then
    failwith "Usage: dotnet fsi src/DotToCsv.fsx <lean-import.dot> [output.csv]"

let inputPath = Path.GetFullPath scriptArgs.[0]
if not (File.Exists inputPath) then
    failwithf "DOT file does not exist: %s" inputPath

let outputPath =
    if scriptArgs.Length > 1 then
        Path.GetFullPath scriptArgs.[1]
    else
        Path.ChangeExtension(inputPath, ".csv")

let edgePattern =
    Regex("^\\s*\"([^\"]+)\"\\s*->\\s*\"([^\"]+)\"\\s*;\\s*$", RegexOptions.Compiled)

let csvEscape (value: string) =
    "\"" + value.Replace("\"", "\"\"") + "\""

let output = StringBuilder()
output.AppendLine("module,used_import") |> ignore

let mutable edgeCount = 0

for line in File.ReadLines inputPath do
    let edge = edgePattern.Match line
    if edge.Success then
        let moduleName = edge.Groups.[1].Value
        let usedImport = edge.Groups.[2].Value
        output.Append(csvEscape moduleName).Append(',').Append(csvEscape usedImport).AppendLine() |> ignore
        edgeCount <- edgeCount + 1

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)) |> ignore
File.WriteAllText(outputPath, output.ToString(), UTF8Encoding(false))

printfn "Converted %d directed import(s)." edgeCount
printfn "Wrote CSV file to %s" outputPath
