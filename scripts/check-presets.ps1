$FdSrc = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $FdSrc
$ErrorActionPreference='Stop'
Set-Location $FdSrc
$fx="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
New-Item -ItemType Directory -Force .dream-loop | Out-Null
& "$fx\csc.exe" /nologo /target:exe /out:.dream-loop\PresetPureTest.exe PresetCore.cs ..\tests\PresetPureTest.cs
if($LASTEXITCODE -ne 0){throw 'Preset pure test compile failed'}
& .dream-loop\PresetPureTest.exe presets
if($LASTEXITCODE -ne 0){throw 'Preset pure test failed'}
# Source guards: the preset core must stay free of process, network and Prism-file access.
$s=Get-Content PresetCore.cs -Raw
foreach($bad in @('Process.Start','HttpClient','WebClient','WebRequest','DllImport','Assembly.Load')){
  if($s.Contains($bad)){throw "PresetCore.cs must not contain $bad"}
}
# Shipped preset files must not contain text copied from other programs' shader or preset formats.
foreach($f in Get-ChildItem presets -Filter *.preset){
  $t=Get-Content $f.FullName -Raw
  foreach($bad in @('shader','.slangp','.glsl','#version')){
    if($t.ToLowerInvariant().Contains($bad)){throw "$($f.Name) looks copied from a shader preset: $bad"}
  }
}
'PASS: preset parser, service and shipped files; source guards.'
