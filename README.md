# Riemann Hypothesis supplementary scripts

F# scripts for analyzing Lean module imports.

## LeanImportGraph.fsx

Generate the complete module graph or a folder-level graph:

```powershell
dotnet fsi LeanImportGraph.fsx ..\riemann-hypothesis\src
dotnet fsi LeanImportGraph.fsx ..\riemann-hypothesis\src --folders --depth 2
```

The first command writes `lean-imports.dot`. Folder mode writes
`lean-impots-depth2.dot` by default. Use `--depth N` to change the folder
depth and `--output FILE` to set an explicit output path.

## TrimDotDependencies.fsx

Remove self and nested-module dependencies and print the cleaned DOT to the
console:

```powershell
dotnet fsi TrimDotDependencies.fsx lean-impots-depth2.dot
```

To save the result:

```powershell
dotnet fsi TrimDotDependencies.fsx lean-impots-depth2.dot > lean-impots-depth2-clean.dot
```

## RemoveExternalDotDependencies.fsx

Remove `Lean`, `Mathlib`, and `Batteries` nodes and their nested modules, along
with all edges to or from them. Both dot-separated module names and
slash-separated folder names are supported. This filter expects the
line-oriented DOT format produced by `LeanImportGraph.fsx`.

```powershell
dotnet fsi RemoveExternalDotDependencies.fsx lean-impots-depth2-clean.dot > lean-local.dot
```

## ColorLeafDotNodes.fsx

Fill nodes with no outgoing edges light green, including isolated nodes.
The script expects the line-oriented DOT format produced by
`LeanImportGraph.fsx` and writes the resulting DOT to standard output.
It also removes the light-green edge attribute added by the earlier
`ColorLeafDotEdges.fsx` script.

```powershell
dotnet fsi ColorLeafDotNodes.fsx lean-local.dot > lean-local-nodes.dot
```

## DotToCsv.fsx

Convert directed DOT edges to a two-column CSV named `module,used_import`:

```powershell
dotnet fsi DotToCsv.fsx lean-imports.dot
dotnet fsi DotToCsv.fsx lean-imports.dot lean-imports.csv
```
