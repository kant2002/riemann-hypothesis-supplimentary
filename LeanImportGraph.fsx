open System
open System.IO
open System.Text
open System.Text.RegularExpressions

let args = Environment.GetCommandLineArgs()
// dotnet fsi passes Environment.GetCommandLineArgs as [fsi; script; ...].
let scriptArgs =
    if args.Length >= 2 && String.Equals(Path.GetFileName(args.[1]), "LeanImportGraph.fsx", StringComparison.OrdinalIgnoreCase) then
        args |> Array.skip 2
    else
        args |> Array.skip 1

let shortMode =
    scriptArgs
    |> Array.exists (fun arg -> String.Equals(arg, "--folders", StringComparison.OrdinalIgnoreCase))

let depth =
    scriptArgs
    |> Array.tryFindIndex (fun arg -> String.Equals(arg, "--depth", StringComparison.OrdinalIgnoreCase))
    |> Option.bind (fun index ->
        if index + 1 < scriptArgs.Length then
            match Int32.TryParse(scriptArgs.[index + 1]) with
            | true, value when value >= 1 -> Some value
            | _ -> None
        else None)
    |> Option.defaultValue 2

let positionalArgs =
    let items = ResizeArray<string>()
    let mutable i = 0
    while i < scriptArgs.Length do
        if String.Equals(scriptArgs.[i], "--output", StringComparison.OrdinalIgnoreCase) then
            i <- i + 2
        elif String.Equals(scriptArgs.[i], "--depth", StringComparison.OrdinalIgnoreCase) then
            i <- i + 2
        elif String.Equals(scriptArgs.[i], "--folders", StringComparison.OrdinalIgnoreCase) then
            i <- i + 1
        else
            items.Add scriptArgs.[i]
            i <- i + 1
    items.ToArray()

let srcRoot =
    if positionalArgs.Length > 0 then Path.GetFullPath positionalArgs.[0]
    else __SOURCE_DIRECTORY__

let outputPath =
    let explicitOutput =
        scriptArgs
        |> Array.tryFindIndex (fun arg -> String.Equals(arg, "--output", StringComparison.OrdinalIgnoreCase))
        |> Option.bind (fun index ->
            if index + 1 < scriptArgs.Length then Some(scriptArgs.[index + 1])
            else None)
    match explicitOutput with
    | Some path -> Path.GetFullPath path
    | None ->
        let fileName =
            if shortMode then sprintf "lean-impots-depth%d.dot" depth
            else "lean-imports.dot"
        Path.Combine(srcRoot, fileName)

let leanFiles =
    Directory.GetFiles(srcRoot, "*.lean", SearchOption.AllDirectories)
    |> Array.sort

let moduleName (path: string) =
    let relativePath = Path.GetRelativePath(srcRoot, path)
    relativePath.Substring(0, relativePath.Length - ".lean".Length)
        .Replace('\\', '.')
        .Replace('/', '.')

let localModules =
    leanFiles
    |> Array.map moduleName
    |> Set.ofArray

let folderName (path: string) =
    let relativePath = Path.GetRelativePath(srcRoot, path)
    let directory = Path.GetDirectoryName(relativePath)
    if String.IsNullOrEmpty(directory) then "(root)"
    else
        let parts =
            directory.Replace('\\', '/').Split('/')
            |> Array.filter (fun part -> not (String.IsNullOrWhiteSpace(part)))
        if parts.Length = 0 then "(root)"
        else String.Join("/", parts |> Array.truncate depth)

let localModuleFolders =
    leanFiles
    |> Array.map (fun path -> moduleName path, folderName path)
    |> dict

let importPattern =
    Regex(@"^\s*import\s+([A-Za-z0-9_.]+)", RegexOptions.Compiled)

let moduleImports =
    leanFiles
    |> Array.collect (fun path ->
        let source = moduleName path
        File.ReadLines path
        |> Seq.choose (fun line ->
            let matchResult = importPattern.Match line
            if matchResult.Success then Some(matchResult.Groups.[1].Value) else None)
        |> Seq.map (fun target -> source, target)
        |> Seq.distinct
        |> Seq.toArray)
    |> Array.distinct

let imports =
    if shortMode then
        moduleImports
        |> Array.map (fun (source, target) ->
            let sourceFolder =
                match localModuleFolders.TryGetValue(source) with
                | true, folder -> folder
                | false, _ -> "(unknown)"
            let targetFolder =
                match localModuleFolders.TryGetValue(target) with
                | true, folder -> folder
                | false, _ ->
                    target.Split('.')
                    |> Array.truncate depth
                    |> String.concat "/"
            sourceFolder, targetFolder)
        |> Array.distinct
    else
        moduleImports

let quote (value: string) =
    "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""

let stringBuilder = StringBuilder()
stringBuilder.AppendLine("digraph LeanImports {") |> ignore
stringBuilder.AppendLine("  rankdir=LR;") |> ignore
stringBuilder.AppendLine("  graph [fontname=\"Arial\"];") |> ignore
stringBuilder.AppendLine("  node [shape=box, style=filled, fillcolor=\"#f5f5f5\", fontname=\"Arial\"];") |> ignore
stringBuilder.AppendLine("  edge [color=\"#777777\"];") |> ignore

let localNodes =
    if shortMode then
        localModuleFolders.Values |> Seq.distinct |> Seq.sort |> Seq.toArray
    else
        localModules |> Set.toArray

for moduleName in localNodes do
    stringBuilder.Append("  ").Append(quote moduleName).AppendLine(" [label=" + quote moduleName + "];") |> ignore

let externalModules =
    imports
    |> Array.map snd
    |> Array.filter (fun target ->
        if shortMode then not (Seq.contains target (localNodes |> Seq.ofArray))
        else not (localModules.Contains target))
    |> Array.distinct
    |> Array.sort

for moduleName in externalModules do
    stringBuilder.Append("  ").Append(quote moduleName).AppendLine(" [label=" + quote moduleName + ", fillcolor=\"#fff2cc\"];") |> ignore

for source, target in imports do
    stringBuilder.Append("  ").Append(quote source).Append(" -> ").Append(quote target).AppendLine(";") |> ignore

stringBuilder.AppendLine("}") |> ignore
File.WriteAllText(outputPath, stringBuilder.ToString(), UTF8Encoding(false))

printfn "Found %d Lean modules and %d graph edges." leanFiles.Length imports.Length
printfn "Wrote Graphviz graph to %s" outputPath
