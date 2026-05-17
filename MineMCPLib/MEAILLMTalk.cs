using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System.Diagnostics;

namespace MineMCPLib;

/// <summary>
/// MEAI 封装的 LLM 通信类
/// 使用 Microsoft.Extensions.AI 抽象 + OpenAI SDK 实现
/// </summary>
public class MEAILLMTalk
{
    private static IChatClient? _client;
    private static string _currentModel = string.Empty;
    private static string _currentBaseUrl = string.Empty;

    /// <summary>
    /// 创建或获取 IChatClient
    /// </summary>
    public static IChatClient GetClient(string baseUrl, string model)
    {
        if (_client == null || _currentModel != model || _currentBaseUrl != baseUrl)
        {
            // 使用 OpenAI SDK 创建客户端，但返回 IChatClient 抽象
            var credential = new ApiKeyCredential("not-needed");
            var openAiClient = new OpenAIClient(credential, new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });
            _client = openAiClient.GetChatClient(model).AsIChatClient();
            _currentModel = model;
            _currentBaseUrl = baseUrl;
        }
        return _client;
    }

    /// <summary>
    /// 测试 LM Studio 连接
    /// </summary>
    public static async Task TestLinkAsync()
    {
        string testUrl = "http://localhost:1234/v1/models";
        Debug.WriteLine("开始访问地址：" + testUrl);

        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var response = await client.GetAsync(testUrl);
            response.EnsureSuccessStatusCode();
            Debug.WriteLine("访问成功！LM服务能连通");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"访问失败！未知错误：{ex.Message}");
        }
    }

    /// <summary>
    /// 向 LLM 发送一张图片 + 提示词
    /// </summary>
    public static async Task<string> SendImageToLLMAsync(
        string base64,
        string type,
        string prompt,
        string model,
        string baseUrl = "http://localhost:1234/v1")
    {
        var client = GetClient(baseUrl, model);

        // 使用 MEAI 抽象构建消息
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, new List<AIContent>
            {
                new DataContent(Convert.FromBase64String(base64), $"image/{type}"),
                new TextContent(prompt)
            })
        };

        var response = await client.GetResponseAsync(messages);
        return response.Text ?? string.Empty;
    }

    /// <summary>
    /// 向 LLM 发送纯文本消息
    /// </summary>
    public static async Task<string> SendTextToLLMAsync(
        string prompt,
        string model,
        string baseUrl = "http://localhost:1234/v1",
        string? systemPrompt = null)
    {
        var client = GetClient(baseUrl, model);

        var messages = new List<ChatMessage>();

        if (!string.IsNullOrEmpty(systemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, systemPrompt));
        }

        messages.Add(new ChatMessage(ChatRole.User, prompt));

        var response = await client.GetResponseAsync(messages);
        return response.Text ?? string.Empty;
    }

    /// <summary>
    /// 流式发送消息
    /// </summary>
    public static async IAsyncEnumerable<string> StreamTextToLLMAsync(
        string prompt,
        string model,
        string baseUrl = "http://localhost:1234/v1",
        string? systemPrompt = null)
    {
        var client = GetClient(baseUrl, model);

        var messages = new List<ChatMessage>();

        if (!string.IsNullOrEmpty(systemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, systemPrompt));
        }

        messages.Add(new ChatMessage(ChatRole.User, prompt));

        await foreach (var update in client.GetStreamingResponseAsync(messages))
        {
            yield return update.Text ?? string.Empty;
        }
    }

    /// <summary>
    /// 重置客户端
    /// </summary>
    public static void ResetClient()
    {
        _client = null;
        _currentModel = string.Empty;
        _currentBaseUrl = string.Empty;
    }
}
