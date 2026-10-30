$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$taskRoot=Split-Path $PSScriptRoot -Parent
$assetPath=Join-Path $PSScriptRoot 'Lumivolt_v2.2.0/3_소스코드/resources/app.ico'
$sizes=@(16,24,32,48,64,128,256)
$chunks=[System.Collections.Generic.List[byte[]]]::new()
foreach($n in $sizes){
    $bmp=[Drawing.Bitmap]::new($n,$n)
    $g=[Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([Drawing.Color]::Transparent)
    $shape=[Drawing.Drawing2D.GraphicsPath]::new()
    $diam=[float]($n*0.48); $edge=[float]($n-1)
    $shape.AddArc(0,0,$diam,$diam,180,90)
    $shape.AddArc($edge-$diam,0,$diam,$diam,270,90)
    $shape.AddArc($edge-$diam,$edge-$diam,$diam,$diam,0,90)
    $shape.AddArc(0,$edge-$diam,$diam,$diam,90,90)
    $shape.CloseFigure()
    $fill=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(29,87,204))
    $g.FillPath($fill,$shape)
    $pen=[Drawing.Pen]::new([Drawing.Color]::White,[float]($n*.078))
    $pen.StartCap=$pen.EndCap=[Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($pen,[float]($n*.29),[float]($n*.29),[float]($n*.70),[float]($n*.29))
    $g.DrawLine($pen,[float]($n*.29),[float]($n*.50),[float]($n*.60),[float]($n*.50))
    $g.DrawLine($pen,[float]($n*.29),[float]($n*.71),[float]($n*.70),[float]($n*.71))
    $stream=[IO.MemoryStream]::new();$bmp.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
    $chunks.Add($stream.ToArray())
    $pen.Dispose();$fill.Dispose();$shape.Dispose();$g.Dispose();$bmp.Dispose();$stream.Dispose()
}
$out=[IO.File]::Create($assetPath);$writer=[IO.BinaryWriter]::new($out)
$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$sizes.Count)
$offset=6+16*$sizes.Count
for($i=0;$i -lt $sizes.Count;$i++){
    $dim=if($sizes[$i] -eq 256){0}else{$sizes[$i]}
    $writer.Write([byte]$dim);$writer.Write([byte]$dim);$writer.Write([byte]0);$writer.Write([byte]0)
    $writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$chunks[$i].Length);$writer.Write([uint32]$offset)
    $offset+=$chunks[$i].Length
}
foreach($chunk in $chunks){$writer.Write($chunk)}
$writer.Dispose();$out.Dispose()
