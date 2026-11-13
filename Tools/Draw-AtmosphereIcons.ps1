# Draws the Atmosphere Overhaul HUD strip icons from Material Design Icons path data (Pictogrammers Free License) into Textures\UI\Icons\Hud.
param([string]$Out = (Join-Path $PSScriptRoot '..\Textures\UI\Icons\Hud'))
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text.RegularExpressions;

public static class SvgPath
{
    // Absolute M, L, H, V, C, A and Z commands, as Material Design Icons uses.
    public static GraphicsPath Parse(string d, float scale, float dx, float dy)
    {
        GraphicsPath path = new GraphicsPath(FillMode.Winding);
        List<string> tokens = new List<string>();
        foreach (Match m in Regex.Matches(d, @"[MLHVCAZ]|-?\d*\.?\d+(?:e-?\d+)?")) tokens.Add(m.Value);
        int i = 0;
        char cmd = 'M';
        PointF cur = PointF.Empty, start = PointF.Empty;
        Func<float> num = () => float.Parse(tokens[i++], CultureInfo.InvariantCulture);
        Func<float, float, PointF> P = (x, y) => new PointF(x * scale + dx, y * scale + dy);
        bool open = false;
        while (i < tokens.Count)
        {
            if (char.IsLetter(tokens[i][0])) cmd = tokens[i++][0];
            switch (cmd)
            {
                case 'M':
                    if (open) path.StartFigure();
                    cur = start = new PointF(num(), num()); open = true; cmd = 'L'; break;
                case 'L': { PointF n = new PointF(num(), num()); path.AddLine(P(cur.X, cur.Y), P(n.X, n.Y)); cur = n; break; }
                case 'H': { PointF n = new PointF(num(), cur.Y); path.AddLine(P(cur.X, cur.Y), P(n.X, n.Y)); cur = n; break; }
                case 'V': { PointF n = new PointF(cur.X, num()); path.AddLine(P(cur.X, cur.Y), P(n.X, n.Y)); cur = n; break; }
                case 'C':
                {
                    PointF a = new PointF(num(), num()), b = new PointF(num(), num()), n = new PointF(num(), num());
                    path.AddBezier(P(cur.X, cur.Y), P(a.X, a.Y), P(b.X, b.Y), P(n.X, n.Y)); cur = n; break;
                }
                case 'A':
                {
                    float rx = num(), ry = num(), rot = num(), large = num(), sweep = num();
                    PointF n = new PointF(num(), num());
                    Arc(path, cur, n, rx, ry, rot, large != 0, sweep != 0, P);
                    cur = n; break;
                }
                case 'Z': path.CloseFigure(); cur = start; open = false; break;
                default: throw new InvalidOperationException("Unsupported command " + cmd);
            }
        }
        return path;
    }

    // Endpoint arc to cubic Beziers (SVG implementation notes, F.6).
    private static void Arc(GraphicsPath path, PointF p0, PointF p1, float rx, float ry, float rotDeg, bool large, bool sweep, Func<float, float, PointF> P)
    {
        if (rx == 0 || ry == 0) { path.AddLine(P(p0.X, p0.Y), P(p1.X, p1.Y)); return; }
        double phi = rotDeg * Math.PI / 180, cos = Math.Cos(phi), sin = Math.Sin(phi);
        double x1p = cos * (p0.X - p1.X) / 2 + sin * (p0.Y - p1.Y) / 2, y1p = -sin * (p0.X - p1.X) / 2 + cos * (p0.Y - p1.Y) / 2;
        double rxs = Math.Abs(rx), rys = Math.Abs(ry);
        double lambda = x1p * x1p / (rxs * rxs) + y1p * y1p / (rys * rys);
        if (lambda > 1) { rxs *= Math.Sqrt(lambda); rys *= Math.Sqrt(lambda); }
        double num = rxs * rxs * rys * rys - rxs * rxs * y1p * y1p - rys * rys * x1p * x1p;
        double den = rxs * rxs * y1p * y1p + rys * rys * x1p * x1p;
        double coef = Math.Sqrt(Math.Max(0, num / den)) * (large == sweep ? -1 : 1);
        double cxp = coef * rxs * y1p / rys, cyp = -coef * rys * x1p / rxs;
        double cx = cos * cxp - sin * cyp + (p0.X + p1.X) / 2, cy = sin * cxp + cos * cyp + (p0.Y + p1.Y) / 2;
        Func<double, double, double, double, double> ang = (ux, uy, vx, vy) =>
        {
            double a = Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
            return a;
        };
        double t1 = ang(1, 0, (x1p - cxp) / rxs, (y1p - cyp) / rys);
        double dt = ang((x1p - cxp) / rxs, (y1p - cyp) / rys, (-x1p - cxp) / rxs, (-y1p - cyp) / rys);
        if (!sweep && dt > 0) dt -= 2 * Math.PI; else if (sweep && dt < 0) dt += 2 * Math.PI;
        int segs = (int)Math.Ceiling(Math.Abs(dt) / (Math.PI / 2));
        double step = dt / segs, k = 4.0 / 3 * Math.Tan(step / 4);
        Func<double, PointF> E = t => new PointF((float)(cx + rxs * Math.Cos(t) * cos - rys * Math.Sin(t) * sin), (float)(cy + rxs * Math.Cos(t) * sin + rys * Math.Sin(t) * cos));
        Func<double, PointF> D = t => new PointF((float)(-rxs * Math.Sin(t) * cos - rys * Math.Cos(t) * sin), (float)(-rxs * Math.Sin(t) * sin + rys * Math.Cos(t) * cos));
        for (int s = 0; s < segs; s++)
        {
            double a = t1 + s * step, b = a + step;
            PointF ea = E(a), eb = E(b), da = D(a), db = D(b);
            path.AddBezier(P(ea.X, ea.Y), P((float)(ea.X + k * da.X), (float)(ea.Y + k * da.Y)), P((float)(eb.X - k * db.X), (float)(eb.Y - k * db.Y)), P(eb.X, eb.Y));
        }
    }
}
'@

$icons = [ordered]@{
    'atmosphere-pressure' = @(@{ D = 'M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12C20,14.4 19,16.5 17.3,18C15.9,16.7 14,16 12,16C10,16 8.2,16.7 6.7,18C5,16.5 4,14.4 4,12A8,8 0 0,1 12,4M14,5.89C13.62,5.9 13.26,6.15 13.1,6.54L11.81,9.77L11.71,10C11,10.13 10.41,10.6 10.14,11.26C9.73,12.29 10.23,13.45 11.26,13.86C12.29,14.27 13.45,13.77 13.86,12.74C14.12,12.08 14,11.32 13.57,10.76L13.67,10.5L14.96,7.29L14.97,7.26C15.17,6.75 14.92,6.17 14.41,5.96C14.28,5.91 14.15,5.89 14,5.89M10,6A1,1 0 0,0 9,7A1,1 0 0,0 10,8A1,1 0 0,0 11,7A1,1 0 0,0 10,6M7,9A1,1 0 0,0 6,10A1,1 0 0,0 7,11A1,1 0 0,0 8,10A1,1 0 0,0 7,9M17,9A1,1 0 0,0 16,10A1,1 0 0,0 17,11A1,1 0 0,0 18,10A1,1 0 0,0 17,9Z'; S = 1.0; X = 0; Y = 0 })
    # Points up; drawn rotated to the wind's direction.
    'atmosphere-wind' = @(@{ D = 'M12,2L4.5,20.29L5.21,21L12,18L18.79,21L19.5,20.29L12,2Z'; S = 1.0; X = 0; Y = 0 })
    'atmosphere-humidity' = @(@{ D = 'M12,3.25C12,3.25 6,10 6,14C6,17.32 8.69,20 12,20A6,6 0 0,0 18,14C18,10 12,3.25 12,3.25M14.47,9.97L15.53,11.03L9.53,17.03L8.47,15.97M9.75,10A1.25,1.25 0 0,1 11,11.25A1.25,1.25 0 0,1 9.75,12.5A1.25,1.25 0 0,1 8.5,11.25A1.25,1.25 0 0,1 9.75,10M14.25,14.5A1.25,1.25 0 0,1 15.5,15.75A1.25,1.25 0 0,1 14.25,17A1.25,1.25 0 0,1 13,15.75A1.25,1.25 0 0,1 14.25,14.5Z'; S = 1.0; X = 0; Y = 0 })
    # A water drop over a band of ground.
    'atmosphere-soil' = @(
        @{ D = 'M12,20A6,6 0 0,1 6,14C6,10 12,3.25 12,3.25C12,3.25 18,10 18,14A6,6 0 0,1 12,20Z'; S = 0.8; X = 2.4; Y = -1.6 },
        @{ D = 'M3,18H21V22H3Z'; S = 1.0; X = 0; Y = 0 })
}

New-Item -ItemType Directory -Force $Out | Out-Null
$size = 64
$brush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 187, 187, 187))
foreach ($name in $icons.Keys) {
    $bmp = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $unit = $size / 24.0
    foreach ($part in $icons[$name]) {
        $path = [SvgPath]::Parse($part.D, [float]($part.S * $unit), [float]($part.X * $unit), [float]($part.Y * $unit))
        $g.FillPath($brush, $path)
    }
    $g.Dispose()
    $file = Join-Path $Out "$name.png"
    $bmp.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    "wrote $file"
}
