# Cuts the individual control and lamp sprites out of the owner's "Flutter Deck Visual Controls Pack v1" sheets.
# The sheets are only read. Each sprite is cropped to its opaque button frame (not to the equal grid cell, and
# without the neighbouring glow), then scaled to the size the launcher draws it at twice over, for sharp results.
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File ..\scripts\make-sprites.ps1 -Pack <folder with the four sheets> [-ContactSheet <png>]
param([Parameter(Mandatory=$true)][string]$Pack,[string]$ContactSheet='')
$Here = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $Here
$ErrorActionPreference='Stop'
Set-Location $Here
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing @'
using System;using System.Drawing;using System.Drawing.Imaging;using System.Runtime.InteropServices;
public static class SpriteCut{
  public static byte[] Pixels(Bitmap b){var d=b.LockBits(new Rectangle(0,0,b.Width,b.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);var p=new byte[d.Stride*b.Height];Marshal.Copy(d.Scan0,p,0,p.Length);b.UnlockBits(d);return p;}
  // Bounding box, inside one grid cell, of the pixels that pass the test: fully opaque (the frame), optionally dark only (a lamp's cap and cage).
  public static Rectangle Box(byte[] p,int width,Rectangle cell,bool darkOnly){
    int l=int.MaxValue,t=int.MaxValue,r=-1,bm=-1;
    for(int y=cell.Top;y<cell.Bottom;y++)for(int x=cell.Left;x<cell.Right;x++){int i=(y*width+x)*4;if(p[i+3]<250)continue;if(darkOnly&&(p[i]+p[i+1]+p[i+2])>210)continue;if(x<l)l=x;if(x>r)r=x;if(y<t)t=y;if(y>bm)bm=y;}
    if(r<0)throw new Exception("no opaque pixels in cell");return Rectangle.FromLTRB(l,t,r+1,bm+1);
  }
}
'@
$expected=@{'transport-states.png'='dda95919363f8626333703cd7077fab43c3866feed1260f815c2c76e40ee1803';'lamp-states.png'='d73d8472c4f3990bc7979c1ae8293d2028fcdae1094387afeff64b1a720775b7';'utility-controls.png'='40185198f5b55db466b082039d4a0999bfc9487c75a7d1d3b185030800c1298a';'supplementary-controls.png'='b3086a21afb7e896163234fabb0eeed357a49b4f5040fcf07379c5afe8125f29'}
foreach($name in $expected.Keys){$f=Join-Path $Pack $name;if(-not (Test-Path -LiteralPath $f)){throw "Missing $name"};if((Get-FileHash -LiteralPath $f).Hash -ne $expected[$name]){throw "$name is not the version 1 sheet this script was written for"}}
$out=Join-Path $Here 'assets\controls'
New-Item -ItemType Directory -Force $out | Out-Null
$made=New-Object System.Collections.Generic.List[string]
function Save([System.Drawing.Bitmap]$sheet,[System.Drawing.Rectangle]$from,[int]$w,[int]$h,[string]$name){
  $b=New-Object System.Drawing.Bitmap $w,$h,([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g=[System.Drawing.Graphics]::FromImage($b);$g.InterpolationMode='HighQualityBicubic';$g.PixelOffsetMode='HighQuality';$g.CompositingQuality='HighQuality';$g.Clear([System.Drawing.Color]::Transparent)
  $attr=New-Object System.Drawing.Imaging.ImageAttributes;$attr.SetWrapMode('TileFlipXY')
  $g.DrawImage($sheet,(New-Object System.Drawing.Rectangle 0,0,$w,$h),$from.X,$from.Y,$from.Width,$from.Height,[System.Drawing.GraphicsUnit]::Pixel,$attr)
  $g.Dispose();$attr.Dispose();$b.Save((Join-Path $out "$name.png"),[System.Drawing.Imaging.ImageFormat]::Png);$b.Dispose();$made.Add($name)
}
function Sheet([string]$file,[string[]]$names,[int]$cols,[int]$rows,[int]$height){
  $sheet=[System.Drawing.Bitmap]::FromFile((Join-Path $Pack $file));$p=[SpriteCut]::Pixels($sheet);$cw=$sheet.Width/$cols;$ch=$sheet.Height/$rows
  for($i=0;$i -lt $names.Count;$i++){
    $cell=New-Object System.Drawing.Rectangle ([int](($i%$cols)*$cw)),([int]([Math]::Floor($i/$cols)*$ch)),([int]$cw),([int]$ch)
    $box=[SpriteCut]::Box($p,$sheet.Width,$cell,$false)
    Save $sheet $box ([int][Math]::Round($height*$box.Width/$box.Height)) $height $names[$i]
  }
  $sheet.Dispose()
}
$states='normal','hover','pressed','disabled'
Sheet 'transport-states.png' (@('play','pause','stop') | ForEach-Object { $k=$_; $states | ForEach-Object { "$k-$_" } }) 4 3 100
Sheet 'utility-controls.png' @('previous','next','shuffle','repeat','speaker','mute','settings','save','folder','gamepad','grid','download') 4 3 144
Sheet 'supplementary-controls.png' @('images','music','help','setup','minimize','maximize','close','reset','scale-down','scale-up','features','menu') 4 3 144
# Lamps: every frame is cut to the same window around its own cap and cage, so the four frames line up when swapped.
$lamps=[System.Drawing.Bitmap]::FromFile((Join-Path $Pack 'lamp-states.png'));$lp=[SpriteCut]::Pixels($lamps);$names='off','dim','on','bright'
for($i=0;$i -lt 4;$i++){
  $cell=New-Object System.Drawing.Rectangle ($i*543),0,543,724
  $fixture=[SpriteCut]::Box($lp,$lamps.Width,$cell,$true)
  $cx=$fixture.Left+$fixture.Width/2;$half=260
  $from=New-Object System.Drawing.Rectangle ([int]($cx-$half)),([int]($fixture.Top-120)),($half*2),($half*2)
  Save $lamps $from 208 208 "lamp-$($names[$i])"
}
$lamps.Dispose()
"Wrote $($made.Count) sprites to assets\controls"
if($ContactSheet){
  $cols=8;$cell=160;$rows=[Math]::Ceiling($made.Count/$cols);$sheet=New-Object System.Drawing.Bitmap ($cols*$cell),([int]($rows*$cell))
  $g=[System.Drawing.Graphics]::FromImage($sheet);$g.Clear([System.Drawing.Color]::FromArgb(120,20,90));$font=New-Object System.Drawing.Font 'Segoe UI',8
  for($i=0;$i -lt $made.Count;$i++){$img=[System.Drawing.Bitmap]::FromFile((Join-Path $out "$($made[$i]).png"));$s=[Math]::Min(1.0,[Math]::Min(150/$img.Width,130/$img.Height));$x=($i%$cols)*$cell+4;$y=[Math]::Floor($i/$cols)*$cell+2
    $g.DrawImage($img,[int]$x,[int]$y,[int]($img.Width*$s),[int]($img.Height*$s));$g.DrawString("$($made[$i]) $($img.Width)x$($img.Height)",$font,[System.Drawing.Brushes]::White,[single]$x,[single]($y+138));$img.Dispose()}
  $g.Dispose();$sheet.Save($ContactSheet,[System.Drawing.Imaging.ImageFormat]::Png);$sheet.Dispose();"Contact sheet: $ContactSheet"
}
