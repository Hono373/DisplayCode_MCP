using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;

namespace MineMCPLib;

/// <summary>
/// 图片格式转换工具类
/// </summary>
public static class ImageConverter
{
    /// <summary>
    /// 将任意图片（包括WEBP）转成 JPG 并返回 Base64
    /// </summary>
    public static string ConvertToJpgBase64(string imagePath)
    {
        using var image = Image.Load(imagePath);
        using var ms = new MemoryStream();

        // 强制保存为 JPG
        image.Save(ms, new JpegEncoder { Quality = 90 });
        return Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>
    /// 将任意图片（包括WEBP）转成 PNG 并返回 Base64
    /// </summary>
    public static string ConvertToPngBase64(string imagePath)
    {
        using var image = Image.Load(imagePath);
        using var ms = new MemoryStream();

        // 强制保存为 PNG
        image.Save(ms, new PngEncoder());
        return Convert.ToBase64String(ms.ToArray());
    }
}
