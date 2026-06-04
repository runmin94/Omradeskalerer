param(
    [string]$Source = (Join-Path $PSScriptRoot "..\Assets\AppIconSource.png"),
    [string]$TransparentPng = (Join-Path $PSScriptRoot "..\Assets\AppIcon.png"),
    [string]$Destination = (Join-Path $PSScriptRoot "..\Assets\AppIcon.ico")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

public static class ConnectedBackgroundRemover
{
    public static void Remove(string sourcePath, string destinationPath, int threshold)
    {
        using (Bitmap source = new Bitmap(sourcePath))
        using (Bitmap output = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
        {
            using (Graphics graphics = Graphics.FromImage(output))
            {
                graphics.DrawImageUnscaled(source, 0, 0);
            }

            int width = output.Width;
            int height = output.Height;
            bool[] visited = new bool[width * height];
            Queue<int> queue = new Queue<int>();

            for (int x = 0; x < width; x++)
            {
                EnqueueIfBackground(output, x, 0, threshold, visited, queue);
                EnqueueIfBackground(output, x, height - 1, threshold, visited, queue);
            }

            for (int y = 0; y < height; y++)
            {
                EnqueueIfBackground(output, 0, y, threshold, visited, queue);
                EnqueueIfBackground(output, width - 1, y, threshold, visited, queue);
            }

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int x = index % width;
                int y = index / width;
                output.SetPixel(x, y, Color.Transparent);

                EnqueueIfBackground(output, x - 1, y, threshold, visited, queue);
                EnqueueIfBackground(output, x + 1, y, threshold, visited, queue);
                EnqueueIfBackground(output, x, y - 1, threshold, visited, queue);
                EnqueueIfBackground(output, x, y + 1, threshold, visited, queue);
            }

            output.Save(destinationPath, ImageFormat.Png);
        }
    }

    private static void EnqueueIfBackground(
        Bitmap bitmap,
        int x,
        int y,
        int threshold,
        bool[] visited,
        Queue<int> queue)
    {
        if (x < 0 || y < 0 || x >= bitmap.Width || y >= bitmap.Height)
        {
            return;
        }

        int index = y * bitmap.Width + x;
        if (visited[index])
        {
            return;
        }

        visited[index] = true;
        Color color = bitmap.GetPixel(x, y);
        int strongestChannel = Math.Max(color.R, Math.Max(color.G, color.B));
        if (strongestChannel <= threshold)
        {
            queue.Enqueue(index);
        }
    }
}
"@

$sourcePath = (Resolve-Path $Source).Path
$transparentPath = [System.IO.Path]::GetFullPath($TransparentPng)
$destinationPath = [System.IO.Path]::GetFullPath($Destination)

[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($transparentPath)) | Out-Null
[ConnectedBackgroundRemover]::Remove($sourcePath, $transparentPath, 55)

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$sourceImage = [System.Drawing.Image]::FromFile($transparentPath)
$frames = @()

try {
    foreach ($size in $sizes) {
        $bitmap = [System.Drawing.Bitmap]::new(
            $size,
            $size,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
        )
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $stream = [System.IO.MemoryStream]::new()

        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.DrawImage($sourceImage, 0, 0, $size, $size)
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $frames += ,$stream.ToArray()
        }
        finally {
            $stream.Dispose()
            $graphics.Dispose()
            $bitmap.Dispose()
        }
    }
}
finally {
    $sourceImage.Dispose()
}

$fileStream = [System.IO.File]::Open(
    $destinationPath,
    [System.IO.FileMode]::Create,
    [System.IO.FileAccess]::Write
)
$writer = [System.IO.BinaryWriter]::new($fileStream)

try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$sizes.Count)

    $offset = 6 + (16 * $sizes.Count)
    for ($index = 0; $index -lt $sizes.Count; $index++) {
        $size = $sizes[$index]
        $frame = $frames[$index]

        $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
        $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Length)
        $writer.Write([uint32]$offset)
        $offset += $frame.Length
    }

    foreach ($frame in $frames) {
        $writer.Write($frame)
    }
}
finally {
    $writer.Dispose()
    $fileStream.Dispose()
}

Write-Output "Created $destinationPath"
