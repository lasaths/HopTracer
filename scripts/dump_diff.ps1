$ErrorActionPreference = "Stop"

$baseDir = Join-Path $PSScriptRoot "..\\src_csharp\\TestDiff\\bin\\Debug\\net9.0"
$oldPath = Join-Path $PSScriptRoot "..\\tests\\data\\DataDrop16.ghx"
$newPath = Join-Path $PSScriptRoot "..\\tests\\data\\DataDrop17.ghx"
$outputPath = Join-Path $PSScriptRoot "..\\tests\\data\\diff_sample.json"

Add-Type -Path (Join-Path $baseDir "Microsoft.Extensions.Logging.Abstractions.dll")
Add-Type -Path (Join-Path $baseDir "GH_IO.dll")
Add-Type -Path (Join-Path $baseDir "HopTracer.Core.dll")

$parserLogger = [Microsoft.Extensions.Logging.Abstractions.NullLogger[HopTracer.Core.Services.GhxParser]]::Instance
$differLogger = [Microsoft.Extensions.Logging.Abstractions.NullLogger[HopTracer.Core.Services.Differ]]::Instance

$parser = [HopTracer.Core.Services.GhxParser]::new($parserLogger)
$differ = [HopTracer.Core.Services.Differ]::new($differLogger)

$graphOld = $parser.Parse($oldPath)
$graphNew = $parser.Parse($newPath)

$diff = $differ.Diff($graphOld, $graphNew)
$nodes = $diff.Item1
$edges = $diff.Item2

$meta = [ordered]@{
    generatedAt = (Get-Date).ToString("o")
    nodeCount   = $nodes.Count
    edgeCount   = $edges.Count
}

$result = [ordered]@{
    nodes = $nodes
    edges = $edges
    meta  = $meta
}

$result | ConvertTo-Json -Depth 8 | Set-Content -Path $outputPath -Encoding UTF8
Write-Host "Diff JSON written to $outputPath"
