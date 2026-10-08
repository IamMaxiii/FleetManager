# Dibuja el logo de FleetManager y crea FleetManager.ico (varios tamaños) y una vista previa en PNG.
# Uso: powershell -ExecutionPolicy Bypass -File herramientas\crear-logo.ps1
# Logo: esfera de tacógrafo con los cuatro colores de las actividades y una aguja blanca (sin texto).
param(
    [string]$Icono = "$PSScriptRoot\..\FleetManager\Recursos\FleetManager.ico",
    [string]$Vista = "$PSScriptRoot\..\FleetManager\Recursos\FleetManager-256.png"
)

Add-Type -AssemblyName System.Drawing
$Icono = [System.IO.Path]::GetFullPath($Icono)
$Vista = [System.IO.Path]::GetFullPath($Vista)
New-Item -ItemType Directory -Force (Split-Path $Icono) | Out-Null

function Color([string]$hex) { [System.Drawing.ColorTranslator]::FromHtml($hex) }

function Dibujar([int]$tam) {
    $imagen = New-Object System.Drawing.Bitmap $tam, $tam
    $g = [System.Drawing.Graphics]::FromImage($imagen)
    $g.SmoothingMode = 'AntiAlias'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.ScaleTransform($tam / 256.0, $tam / 256.0)

    # Fondo: cuadrado oscuro con esquinas redondeadas.
    $r = 52
    $fondo = New-Object System.Drawing.Drawing2D.GraphicsPath
    $fondo.AddArc(4, 4, $r, $r, 180, 90)
    $fondo.AddArc(252 - $r, 4, $r, $r, 270, 90)
    $fondo.AddArc(252 - $r, 252 - $r, $r, $r, 0, 90)
    $fondo.AddArc(4, 252 - $r, $r, $r, 90, 90)
    $fondo.CloseFigure()
    $degradado = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Point 0, 0), (New-Object System.Drawing.Point 0, 256), (Color '#33393F'), (Color '#1A1D21')
    $g.FillPath($degradado, $fondo)

    # Esfera: arco de 270° (abierto por abajo) en cuatro tramos, de descanso a conducción.
    $grosor = if ($tam -le 32) { 34 } else { 26 }
    $caja = New-Object System.Drawing.RectangleF 44, 58, 168, 168
    $colores = '#2E7D32', '#1E88E5', '#EF6C00', '#C62828'
    $hueco = if ($tam -le 32) { 0 } else { 4 }
    for ($i = 0; $i -lt 4; $i++) {
        $lapiz = New-Object System.Drawing.Pen (Color $colores[$i]), $grosor
        $g.DrawArc($lapiz, $caja, 135 + $i * 67.5 + $hueco / 2, 67.5 - $hueco)
        $lapiz.Dispose()
    }

    # Aguja apuntando a la zona de conducción.
    $centro = New-Object System.Drawing.PointF 128, 142
    $angulo = (35 - 90) * [Math]::PI / 180   # 35° a la derecha de la vertical
    $largo = 70
    $punta = New-Object System.Drawing.PointF ($centro.X + $largo * [Math]::Cos($angulo)), ($centro.Y + $largo * [Math]::Sin($angulo))
    $lado = $angulo + [Math]::PI / 2
    $ancho = 9
    $base1 = New-Object System.Drawing.PointF ($centro.X + $ancho * [Math]::Cos($lado)), ($centro.Y + $ancho * [Math]::Sin($lado))
    $base2 = New-Object System.Drawing.PointF ($centro.X - $ancho * [Math]::Cos($lado)), ($centro.Y - $ancho * [Math]::Sin($lado))
    $blanco = New-Object System.Drawing.SolidBrush (Color '#FFFFFF')
    $g.FillPolygon($blanco, [System.Drawing.PointF[]]@($punta, $base1, $base2))
    $g.FillEllipse($blanco, 128 - 16, 142 - 16, 32, 32)
    $g.FillEllipse((New-Object System.Drawing.SolidBrush (Color '#1A1D21')), 128 - 6, 142 - 6, 12, 12)

    $g.Dispose()
    return $imagen
}

# 256 px va como PNG; los demás como mapa de bits clásico (lo entiende cualquier programa).
function MapaDeBits([System.Drawing.Bitmap]$img) {
    $t = $img.Width
    $ms = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter $ms
    $filaMascara = [int]([Math]::Ceiling($t / 32.0) * 4)
    $w.Write([UInt32]40); $w.Write([Int32]$t); $w.Write([Int32]($t * 2))
    $w.Write([UInt16]1); $w.Write([UInt16]32); $w.Write([UInt32]0)
    $w.Write([UInt32]($t * $t * 4 + $filaMascara * $t))
    $w.Write([Int32]0); $w.Write([Int32]0); $w.Write([UInt32]0); $w.Write([UInt32]0)
    $datos = $img.LockBits((New-Object System.Drawing.Rectangle 0, 0, $t, $t), 'ReadOnly', 'Format32bppArgb')
    $bytes = New-Object byte[] ($datos.Stride * $t)
    [System.Runtime.InteropServices.Marshal]::Copy($datos.Scan0, $bytes, 0, $bytes.Length)
    $img.UnlockBits($datos)
    for ($y = $t - 1; $y -ge 0; $y--) { $w.Write($bytes, $y * $datos.Stride, $t * 4) }   # de abajo arriba
    $w.Write((New-Object byte[] ($filaMascara * $t)))                                     # máscara vacía: manda el alfa
    $w.Flush()
    return , $ms.ToArray()
}

$tamanos = 16, 20, 24, 32, 40, 48, 64, 128, 256
$pngs = foreach ($t in $tamanos) {
    $img = Dibujar $t
    if ($t -eq 256) {
        $ms = New-Object System.IO.MemoryStream
        $img.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $img.Save($Vista, [System.Drawing.Imaging.ImageFormat]::Png)
        $datosImagen = $ms.ToArray()
    }
    else {
        $datosImagen = MapaDeBits $img
    }
    $img.Dispose()
    , $datosImagen
}

$salida = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $salida
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$tamanos.Count)
$desplazamiento = 6 + 16 * $tamanos.Count
for ($i = 0; $i -lt $tamanos.Count; $i++) {
    $t = $tamanos[$i]; $lado = if ($t -ge 256) { 0 } else { $t }
    $w.Write([byte]$lado); $w.Write([byte]$lado); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$pngs[$i].Length); $w.Write([UInt32]$desplazamiento)
    $desplazamiento += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Flush()
[System.IO.File]::WriteAllBytes($Icono, $salida.ToArray())
"Icono creado: $Icono"
