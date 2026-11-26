param([Parameter(Mandatory=$true)][string]$Sheet)
$FdSrc = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $FdSrc
# Cuts the owner's Flutter Ship 20-state toolbar sheet into assets\toolbar\<symbol>-<state>.png and derives the play icons
# the sheet lacks (see assets\TOOLBAR-PROVENANCE.md). The sheet itself is not stored in the repository.
$ErrorActionPreference='Stop'
Set-Location $FdSrc
Add-Type -AssemblyName System.Drawing
$expected='3EAA22E17A163C63C3F8B442C2029906F94398949773257AC517CC1C12BB20FD'
if((Get-FileHash -LiteralPath $Sheet).Hash -ne $expected){throw "Unexpected sheet (hash). This script cuts the owner's Flutter-Ship-20-State-Sheet.png, SHA-256 $expected"}
$map=Get-Content assets\toolbar\sheet-mapping.json -Raw | ConvertFrom-Json
Add-Type -TypeDefinition @"
using System;using System.Drawing;using System.Drawing.Drawing2D;using System.Drawing.Imaging;using System.Collections.Generic;
public static class ToolbarPlay {
  static int Lum(Color c){return (c.R*30+c.G*59+c.B*11)/100;}
  // Erases the stop square from a cut stop icon (row by row: a straight blend between the face on either side) and draws a play triangle in the square's colour.
  public static Bitmap FromStop(Bitmap stop){
    int w=stop.Width,h=stop.Height;var o=new Bitmap(w,h,PixelFormat.Format32bppArgb);
    using(var g=Graphics.FromImage(o)){g.DrawImage(stop,0,0,w,h);}
    int x0=w,y0=h,x1=-1,y1=-1;var cols=new List<Color>();
    for(int y=h/2-45;y<h/2+45;y++)for(int x=w/2-45;x<w/2+45;x++){var c=stop.GetPixel(x,y);if(c.A>200&&Lum(c)>110){if(x<x0)x0=x;if(x>x1)x1=x;if(y<y0)y0=y;if(y>y1)y1=y;cols.Add(c);}}
    if(x1<0)throw new Exception("no glyph found in the stop icon");
    cols.Sort(delegate(Color a,Color b){return Lum(a).CompareTo(Lum(b));});
    Color ink=cols[cols.Count*3/4];
    int pad=4;int xa=Math.Max(1,x0-pad),xb=Math.Min(w-2,x1+pad),ya=Math.Max(0,y0-pad),yb=Math.Min(h-1,y1+pad);
    for(int y=ya;y<=yb;y++){Color l=stop.GetPixel(xa-1,y),r=stop.GetPixel(xb+1,y);for(int x=xa;x<=xb;x++){double t=(x-xa+1.0)/(xb-xa+2.0);o.SetPixel(x,y,Color.FromArgb(255,(int)(l.R+(r.R-l.R)*t),(int)(l.G+(r.G-l.G)*t),(int)(l.B+(r.B-l.B)*t)));}}
    double cx=(x0+x1)/2.0+(x1-x0)*0.05,cy=(y0+y1)/2.0,tw=(x1-x0)*0.92,th=(y1-y0)*1.0;
    var pts=new PointF[]{new PointF((float)(cx-tw/2),(float)(cy-th/2)),new PointF((float)(cx+tw/2),(float)cy),new PointF((float)(cx-tw/2),(float)(cy+th/2))};
    using(var g=Graphics.FromImage(o)){g.SmoothingMode=SmoothingMode.AntiAlias;
      using(var b=new SolidBrush(ink))g.FillPolygon(b,pts);
      using(var p=new Pen(ink,7f)){p.LineJoin=LineJoin.Round;g.DrawPolygon(p,pts);}}
    return o;
  }
}
"@ -ReferencedAssemblies System.Drawing
$src=[System.Drawing.Bitmap]::FromFile((Resolve-Path -LiteralPath $Sheet))
$W=200
New-Item -ItemType Directory -Force assets\toolbar | Out-Null
$count=0
foreach($sym in 'pause','restart','stop','fullscreen','menu'){
  foreach($st in 'normal','hover','pressed','disabled'){
    $b=$map.sprites."$sym-$st".visible_alpha_bounds
    # centred on this icon's own visible frame, so every icon and state registers the same way
    $x=[int][math]::Round(($b[0]+$b[2])/2-$W/2); $y=[int][math]::Round(($b[1]+$b[3])/2-$W/2)
    $rect=New-Object System.Drawing.Rectangle $x,$y,$W,$W
    $cut=$src.Clone($rect,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $cut.Save((Join-Path $FdSrc "assets\toolbar\$sym-$st.png"),[System.Drawing.Imaging.ImageFormat]::Png)
    if($sym -eq 'stop'){ $play=[ToolbarPlay]::FromStop($cut); $play.Save((Join-Path $FdSrc "assets\toolbar\play-$st.png"),[System.Drawing.Imaging.ImageFormat]::Png); $play.Dispose(); $count++ }
    $cut.Dispose(); $count++
  }
}
$src.Dispose()
"Cut $count icons (20 from the sheet, 4 derived play icons are included in that count) into assets\toolbar."
