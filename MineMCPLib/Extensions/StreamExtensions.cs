namespace MineMCPLib.Extensions;

public static class StreamExtensions
{
    /// <summary>
    /// 传入 Stream? 判断是否为有效本地图片 → 返回Base64 或 false
    /// </summary>
    public static bool IsImage(this Stream? stream)
    {
        if (stream == null || !stream.CanRead)
            return false;
        try
        {
            byte[] header = new byte[12];
            int bytesRead = stream.Read(header, 0, header.Length);
            if (bytesRead < 2) return false; // 至少需要2个字节

            // JPEG: FF D8
            if (header[0] == 0xFF && header[1] == 0xD8) return true;
            // PNG: 89 50 4E 47
            if (bytesRead >= 4 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47) return true;
            // BMP: 42 4D
            if (header[0] == 0x42 && header[1] == 0x4D) return true;
            // GIF: 47 49 46 38 (GIF8)
            if (bytesRead >= 4 && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38) return true;
            // ICO: 00 00 01 00
            if (bytesRead >= 4 && header[0] == 0x00 && header[1] == 0x00 && header[2] == 0x01 && header[3] == 0x00) return true;
            // TIFF: 49 49 2A 00 或 4D 4D 00 2A
            if (bytesRead >= 4 &&
                ((header[0] == 0x49 && header[1] == 0x49 && header[2] == 0x2A && header[3] == 0x00) ||
                 (header[0] == 0x4D && header[1] == 0x4D && header[2] == 0x00 && header[3] == 0x2A))) return true;
            // WebP: RIFF....WEBP
            if (bytesRead >= 12 &&
                header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 && // "RIFF"
                header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50) return true;
            // AVIF: ftyp box 后跟 avif 或 avis
            if (bytesRead >= 12 &&
                header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70 && // "ftyp"
                ((header[8] == 0x61 && header[9] == 0x76 && header[10] == 0x69 && header[11] == 0x66) ||  // "avif"
                 (header[8] == 0x61 && header[9] == 0x76 && header[10] == 0x69 && header[11] == 0x73)))   // "avis"
                return true;

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"图片判断失败：{ex.Message}");
            return false;
        }
    }
    public static string? ToBase64(this Stream? stream)
    {
        if (stream == null || !stream.CanRead)
            return null;

        try
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return Convert.ToBase64String(ms.ToArray());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"流转Base64失败：{ex.Message}");
            return null;
        }
    }
}