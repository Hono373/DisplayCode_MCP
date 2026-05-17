using MineMCPLib.Extensions;
using ModelContextProtocol.Protocol;

namespace MineMCPLib.Tools;

public class IOTools
{
    public static bool CheckFolder(string folderFullPath)
    {
        if (!Directory.Exists(folderFullPath))
            return false;
        return true;
    }
    public static bool TryGetFirstFileFullPath(string folderFullPath, out string fileFullPath)
    {
        var result = Directory.EnumerateFiles(folderFullPath).FirstOrDefault();
        if (result is null)
        {
            fileFullPath = string.Empty;
            return false;
        }
        fileFullPath = result;
        return true;
    }
    public static bool CheckFileSize(string fileFullPath, out long fileSize, long maxSizeMB = 10)
    {
        var info = new FileInfo(fileFullPath);
        fileSize = info.Length;
        long maxSize = maxSizeMB * 1024 * 1024;
        if (fileSize > maxSize)
            return false;
        return true;
    }
    public static bool CheckIsImage(string fileFullPath)
    {
        using var fileStream = File.OpenRead(fileFullPath);
        if (!fileStream.IsImage())
            return false;
        return true;
    }
    public static bool CheckFileName(string fileFullPath, out string fileFullName)
    {
        fileFullName = Path.GetFileName(fileFullPath);
        if (string.IsNullOrEmpty(fileFullName))
            return false;
        return true;
    }
    public static List<ContentBlock> MoveFile(string fileFullName, string folderPath, string newFolderPath)
    {
        var msg = new List<ContentBlock>();
        if (string.IsNullOrEmpty(fileFullName))
            throw new($"fileFullName:[{fileFullName}]为空，终止并汇报");

        var fileFullPath = Path.Combine(folderPath, fileFullName);
        var fileNewFullPath = Path.Combine(newFolderPath, fileFullName);

        if (!File.Exists(fileFullPath))
            throw new($"[{fileFullPath}]指向的源文件不存在！终止并汇报");

        Directory.CreateDirectory(newFolderPath);
        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileFullName);
        string ext = Path.GetExtension(fileFullName);

        int counter = 0;
        bool isExists = false;
        while (File.Exists(fileNewFullPath))
        {
            counter++;
            isExists = true;
            fileNewFullPath = Path.Combine(newFolderPath, string.Concat(fileNameWithoutExt, $"_{counter}", ext));

            if (counter > 5)
            {
                throw new("文件名后缀的递增次数异常，请自行排查");
            }
        }
        File.Move(fileFullPath, fileNewFullPath);

        return msg.Add($"文件已成功移动到 {newFolderPath}" + (isExists ? $",由于文件重复，添加了后缀[_{counter}]" : string.Empty));
    }
}