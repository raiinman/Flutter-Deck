$FdSrc = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $FdSrc
$ErrorActionPreference='Stop'
Set-Location $FdSrc
New-Item -ItemType Directory -Force (Join-Path $FdSrc '.dream-loop') | Out-Null
$fx="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
$out=Join-Path $FdSrc '.dream-loop\ScalingPureTest.exe'
& "$fx\csc.exe" /nologo /target:exe /out:$out /reference:System.Drawing.dll /reference:System.Windows.Forms.dll scaling\DsiNative.cs scaling\Profile.cs scaling\PureTest.cs
if($LASTEXITCODE -ne 0){throw 'Scaler pure test compile failed'}
& $out
if($LASTEXITCODE -ne 0){throw 'Scaler pure test failed'}
$s=Get-Content (Join-Path $FdSrc 'Program.cs') -Raw
foreach($g in @('RenderScaling()','DsiScaling.Native.Windows()','StopScaling()','Fullscreen=false','selected.Handle')){
 if(-not $s.Contains($g)){throw "Integration guard missing: $g"}
}

$n=Get-Content (Join-Path $FdSrc 'scaling\DsiNative.cs') -Raw
foreach($g in @('result.Add(Pin(hwnd','RequireIdentity(pinned);IntPtr window','RequireIdentity(pinned);return bitmap','Native.RequireIdentity(sourceIdentity);if(worker==null)','Native.RequireIdentity(sourceIdentity);','new PixelCapture(sourceIdentity,profile,')){
 if(-not $n.Contains($g)){throw "Missing pinned identity guard: $g"}
}
if(-not $s.Contains('Native.RequireIdentity(selected)') -or -not $s.Contains('new DsiScaling.ScaleSurface(selected,')){throw 'Start not using pinned window'}
'PASS: enumeration/start/capture/presentation identity source guards. No actual HWND reuse or GUI lifecycle test.'
