using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace MineMCPLib;

public class OpenAITalk
{
    /// <summary>
    /// 向 LM Studio 发送一张 Base64 编码的图片和提示词，并返回模型的文本回复。
    /// </summary>
    /// <param name="base64">图片的 Base64 编码字符串（例如 JPEG 或 PNG 格式）</param>
    /// <param name="prompt">与图片相关的提问或指令</param>
    /// <param name="model">LM Studio 中已加载的视觉模型名称</param>
    /// <param name="baseUrl">LM Studio 的 /v1 兼容 API 基础地址，默认 http://localhost:1234/v1</param>
    /// <returns>模型生成的文本回答内容</returns>
    /// <remarks>
    /// 该方法内部会将 Base64 字符串解码为字节数组，并构造 <see cref="ChatMessageContentPart"/> 图片部分，
    /// 然后与文本提示组合成多模态用户消息，发送给 LM Studio 的聊天补全接口。
    /// </remarks>
    public static async Task<string> SendJpegToLLMAsync(
      string base64,
      string prompt)
    {
        var client = new ChatClient(
            model: "model",
            credential: new ApiKeyCredential("lm-studio"),
            new OpenAIClientOptions { Endpoint = new Uri("http://localhost:1234/v1") }
        );

        // 1. 将 Base64 字符串解码为字节数组并包装为 BinaryData
        var imageBytes = Convert.FromBase64String(base64);
        var binaryData = BinaryData.FromBytes(imageBytes);

        // 2. 【关键】必须使用 CreateImagePart 方法将 BinaryData 转换为图片类型的消息内容部分
        var imagePart = ChatMessageContentPart.CreateImagePart(
            binaryData,
            "image/jpeg"
        );

        // 3. 构造包含文本提示和图片内容的多模态用户消息
        var userMessage = new UserChatMessage(
            ChatMessageContentPart.CreateTextPart(prompt),
            imagePart  // 👈 这里必须传入 imagePart，不可直接传递 BinaryData
        );

        var prefillMessage = new AssistantChatMessage("思考流程");

        ChatCompletion result = await client.CompleteChatAsync(userMessage, prefillMessage);

        return result.Content[0].Text;
    }
}
public static class PrefillChat
{
    /// <summary>
    /// 使用预填充强制 LLM 从指定前缀开始输出
    /// </summary>
    /// <param name="prefix">强制的前缀内容，LLM 会从这里继续</param>
    /// <param name="model">模型名</param>
    /// <param name="baseUrl">API 地址</param>
    /// <param name="apiKey">API Key（可为空）</param>
    public static async Task<string> CompleteAsync(string prefix)
    {
        var client = new ChatClient(
            model: "model",
            credential: new ApiKeyCredential("lm-studio"),
            new OpenAIClientOptions { Endpoint = new Uri("http://localhost:1234/v1") }
        );

        // 关键：插入不完整的 Assistant 消息，强制模型从这里继续
        var prefillMessage = new AssistantChatMessage(prefix);

        ChatCompletion result = await client.CompleteChatAsync(prefillMessage);

        return result.Content[0].Text;
    }

    /// <summary>
    /// 带系统消息的预填充
    /// </summary>
    public static async Task<string> CompleteWithSystemAsync(string systemPrompt)
    {
        var client = new ChatClient(
            model: "model",
            credential: new ApiKeyCredential("lm-studio"),
            new OpenAIClientOptions { Endpoint = new Uri("http://localhost:1234/v1") }
        );

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new AssistantChatMessage("思考流程")  // 强制从这里继续
        };

        ChatCompletion result = await client.CompleteChatAsync(messages);

        return result.Content[0].Text;
    }
}