using MineMCPLib;
using MineMCPLib.Extensions;
using MineMCPLib.Tools;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using PicAnalysis.Serialize;
using System.ComponentModel;
using System.Diagnostics;

namespace PicAnalysis.Tools;

[McpServerToolType]
public class MyMcp
{
    [McpServerTool(Name = "Debug")]
    [Description("特殊函数，用来返回预定义内容")]
    public static List<ContentBlock> Debug()
    {
        var result = new List<ContentBlock>();
        result.Add(GeneralTools.Time());
        return result;
    }

    [McpServerTool(Name = "get_Plan")]
    [Description("获取任务内容")]
    public static List<ContentBlock> GetPlan()
    {
        var result = new List<ContentBlock>();
        return result.Add("get_first_image_analysis，根据提示执行任务，如果出现了提示之外的情况，就停止任务");
    }

    [DebuggerStepThrough]
    [McpServerTool(Name = "get_first_image_analysis")]
    [Description("获取监控文件夹中的第一张图片，使用AI模型分析图片内容并返回分析结果")]
    public static async Task<List<ContentBlock>> GetFirstImgBase64FromFolder([Description("JSON字符串形式的参数（预留）")] string argumentsJson = "{}")
    {
        var list = new List<ContentBlock>();
        var methodName = nameof(GetFirstImgBase64FromFolder);

        // 调用次数计数（同步逻辑，直接执行）
        if (McpSerialize.invokeCounter.TryGetValue(methodName, out var count))
        {
            if (count > 1)
                list.Add($"[{methodName}]调用失败了了{count}次");
            McpSerialize.invokeCounter[methodName] = count + 1;
        }
        else
        {
            McpSerialize.invokeCounter[methodName] = 1;
        }

        var currentCount = McpSerialize.invokeCounter[methodName];
        if (currentCount > 3)
        {
            return list.Add($"失败次数已达{currentCount}次，暂时阻断调用");
        }

        try
        {
            var folderFullPath = McpSerialize.ImgFolderPath;
            if (!IOTools.CheckFolder(folderFullPath))
                return list.Add($"[{folderFullPath}]指向的文件夹不可访问,可能是没有权限或不存在");
            if (!IOTools.TryGetFirstFileFullPath(folderFullPath, out var fileFullPath))
                return list.Add($"[{folderFullPath}]文件夹内没有文件");
            if (!IOTools.CheckFileSize(fileFullPath, out var fileSize))
                return list.Add($"[{folderFullPath}]的大小{fileSize / 1024 / 1024}超过了10MB");
            if (!IOTools.CheckIsImage(fileFullPath))
                return list.Add($"[{fileFullPath}]文件不是可用图片，直接调用MoveNotImg归档，然后重新执行Plan");

            var base64 = ImageConverter.ConvertToJpgBase64(fileFullPath);
            var mimeType = "image/jpeg";
            byte[] imageBytes = Convert.FromBase64String(base64);

            string describes = await OpenAITalk.SendJpegToLLMAsync(base64, "请简单，结构化地描述这张图片");

            list.Add(ImageContentBlock.FromBytes(imageBytes, mimeType));
            list.Add(describes);

            if (!IOTools.CheckFileName(fileFullPath, out var fileFullName))
                return list.Add($"[{fileFullPath}]文件名不合法，直接调用MoveNotImg归档，然后重新执行Plan");

            list.Add($"源文件名：{fileFullName}");
            list.Add("如果是纯图片，直接调用MovePureImg归档，不用询问");
            list.Add("如果图片有成段有意义文案或是含文字的漫画分镜，并且不像是表情包，就算有用图片，你可以调用MoveUsefulImg进行归档");
            list.Add("完整本轮流程后，你需要继续执行Plan，直到没有图片为止");

            McpSerialize.invokeCounter[methodName] = 0;

            return list;
        }
        catch (Exception ex)
        {
            list.Add($"处理图片时出错：{ex.Message}");
            return list;
        }
    }
    [McpServerTool(Name = "move_to_pure_images")]
    [Description("将图片移动到\"纯图片\"分类文件夹")]
    public static List<ContentBlock> MovePureImg([Description("源文件名")] string fileFullName)
    {
        var list = new List<ContentBlock>();
        try
        {
            return IOTools.MoveFile(fileFullName, McpSerialize.ImgFolderPath, McpSerialize.pureImgFolderPath);
        }
        catch (Exception ex)
        {
            return list.Add($"处理图片时出错：{ex.Message}");
        }
    }
    [McpServerTool(Name = "move_to_useful_images")]
    [Description("将图片移动到\"有用图片\"分类文件夹")]
    public static List<ContentBlock> MoveUsefulImg([Description("源文件名")] string fileFullName)
    {
        var list = new List<ContentBlock>();
        try
        {
            return IOTools.MoveFile(fileFullName, McpSerialize.ImgFolderPath, McpSerialize.usefulImgFolderPath);
        }
        catch (Exception ex)
        {
            return list.Add($"处理图片时出错：{ex.Message}");
        }
    }
    [McpServerTool(Name = "move_to_not_images")]
    [Description("将文件移动到\"非图片\"分类文件夹（用于误识别的文件）")]
    public static List<ContentBlock> MoveNotImg([Description("源文件名")] string fileFullName)
    {
        var list = new List<ContentBlock>();
        try
        {
            return IOTools.MoveFile(fileFullName, McpSerialize.ImgFolderPath, McpSerialize.notImgFolderPath);
        }
        catch (Exception ex)
        {
            return list.Add($"处理图片时出错：{ex.Message}");
        }
    }
}
