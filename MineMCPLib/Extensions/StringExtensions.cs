using ModelContextProtocol.Protocol;

namespace MineMCPLib.Extensions;

public static class StringExtensions
{
    /// <summary>
    /// 获取文件夹内第一个文件
    /// </summary>
    public static Stream? GetFirstFileFromFolder(this string folderPath)
    {
        try
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException("文件夹不存在");

            var filePath = Directory.EnumerateFiles(folderPath).FirstOrDefault();
            if (string.IsNullOrEmpty(filePath))
                throw new FileNotFoundException("文件夹内没有文件");

            FileInfo info = new(filePath);
            long fileSize = info.Length;

            if (fileSize > 1024 * 1024 * 15)
                throw new IOException($"文件大小[{fileSize / 1024 / 1024}]超过限制");

            return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"未知错误：{ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 将字符串转换为 ContentBlock
    /// </summary>
    public static ContentBlock Block(this string text)
    {
        return new TextContentBlock { Text = text };
    }


}
