using System.Diagnostics;
using System.Text.Json;

namespace PicAnalysis.Serialize;

public class McpSerialize
{
    static readonly string SavePath = Path.Combine(Directory.GetCurrentDirectory(), "SaveData.json");
    public static readonly string ImgFolderPath = "D:\\PicAnalysis";
    public static readonly string pureImgFolderPath = "D:\\PicAnalysis\\pureImage";
    public static readonly string usefulImgFolderPath = "D:\\PicAnalysis\\usefulImage";
    public static readonly string notImgFolderPath = "D:\\PicAnalysis\\notImage";
    public static Dictionary<string, int> invokeCounter = new();
    public static void Serialize(McpSerialize data)
    {
        string jsonString = JsonSerializer.Serialize(data);
        File.WriteAllText(SavePath, jsonString);
    }
    public static McpSerialize Deserialize()
    {
        try
        {
            Debug.WriteLine(SavePath);
            string jsonFromFile = File.ReadAllText(SavePath);
            var result = JsonSerializer.Deserialize<McpSerialize>(jsonFromFile);
            return result ?? new McpSerialize();
        }
        catch
        {
            var data = new McpSerialize();
            Serialize(data);
            return data;
        }
    }
}