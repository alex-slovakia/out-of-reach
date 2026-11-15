param(
    [Parameter(Mandatory = $true)][string]$Pptx,
    [Parameter(Mandatory = $true)][string]$Pdf,
    [string]$PngDir = ""
)
# Export a PPTX to PDF (and optional PNG previews) through PowerPoint, without showing a window.
$ErrorActionPreference = "Stop"
$app = New-Object -ComObject PowerPoint.Application
try {
    # Open(FileName, ReadOnly, Untitled, WithWindow) : msoTrue = -1, msoFalse = 0
    $pres = $app.Presentations.Open($Pptx, -1, 0, 0)
    try {
        Write-Output ("slides=" + $pres.Slides.Count)
        $pres.SaveAs($Pdf, 32)   # ppSaveAsPDF
        if ($PngDir -ne "") {
            New-Item -ItemType Directory -Force $PngDir | Out-Null
            foreach ($slide in $pres.Slides) {
                $out = Join-Path $PngDir ("slide-{0:00}.png" -f $slide.SlideIndex)
                $slide.Export($out, "PNG", 1600, 900)
            }
        }
    }
    finally {
        $pres.Close()
    }
}
finally {
    if ($app.Presentations.Count -eq 0) { $app.Quit() }
    [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($app)
}
Write-Output ("pdf=" + (Get-Item $Pdf).Length)
