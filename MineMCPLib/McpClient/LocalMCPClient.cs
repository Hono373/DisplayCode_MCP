using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace MineMCPLib.McpClient;

/// <summary>
/// 本地 MCP Client，用于连接 MCP 服务器
/// 基于 HTTP 传输的 MCP 客户端实现
/// </summary>
public class LocalMCPClient : IAsyncDisposable
{
    private HttpClient? _httpClient;
    private readonly string _serverUrl;
    private readonly ILogger<LocalMCPClient>? _logger;
    private bool _isConnected;
    private string? _lastError;

    /// <summary>
    /// 是否已连接
    /// </summary>
    public bool IsConnected => _isConnected;

    /// <summary>
    /// 服务器地址
    /// </summary>
    public string ServerUrl => _serverUrl;

    /// <summary>
    /// 最后一个错误信息
    /// </summary>
    public string? LastError => _lastError;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serverUrl">MCP 服务器地址，如 http://127.0.0.1:5100</param>
    /// <param name="logger">日志记录器（可选）</param>
    public LocalMCPClient(string serverUrl, ILogger<LocalMCPClient>? logger = null)
    {
        _serverUrl = serverUrl.TrimEnd('/');
        _logger = logger;
    }

    /// <summary>
    /// 连接到 MCP 服务器
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_isConnected && _httpClient != null)
        {
            _logger?.LogWarning("已连接到服务器，请先断开连接");
            return;
        }

        try
        {
            _logger?.LogInformation("正在连接到 MCP 服务器: {ServerUrl}", _serverUrl);

            // 创建 HTTP 客户端
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_serverUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };

            // 发送 ping 请求测试连接
            var pingResult = await SendPingAsync(cancellationToken);
            if (pingResult)
            {
                _isConnected = true;
                _logger?.LogInformation("成功连接到 MCP 服务器");
            }
            else
            {
                _lastError = "服务器未响应 ping 请求";
                _logger?.LogWarning(_lastError);
            }
        }
        catch (Exception ex)
        {
            _lastError = $"连接失败: {ex.Message}";
            _logger?.LogError(ex, "连接 MCP 服务器失败");
            throw;
        }
    }

    /// <summary>
    /// 断开与 MCP 服务器的连接
    /// </summary>
    public Task DisconnectAsync()
    {
        if (_httpClient != null)
        {
            _logger?.LogInformation("正在断开与 MCP 服务器的连接");
            _httpClient.Dispose();
            _httpClient = null;
            _isConnected = false;
            _logger?.LogInformation("已断开连接");
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 发送聊天消息并获取响应
    /// </summary>
    /// <param name="message">用户消息</param>
    /// <param name="systemPrompt">系统提示（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型响应文本</returns>
    public async Task<string> SendMessageAsync(
        string message,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var messages = new List<ChatMessage>();

        // 添加系统提示
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, systemPrompt));
        }

        // 添加用户消息
        messages.Add(new ChatMessage(ChatRole.User, message));

        _logger?.LogDebug("发送消息: {Message}", message);

        // 构建 MCP JSON-RPC 请求
        var request = new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid().ToString(),
            method = "tools/call",
            @params = new
            {
                name = "chat",
                arguments = new
                {
                    messages = messages
                }
            }
        };

        var jsonResponse = await SendJsonRpcRequestAsync(request, cancellationToken);
        return ParseJsonRpcResponse(jsonResponse);
    }

    /// <summary>
    /// 发送聊天消息并获取流式响应
    /// </summary>
    public async IAsyncEnumerable<string> SendMessageStreamAsync(
        string message,
        string? systemPrompt = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var messages = new List<ChatMessage>();

        if (!string.IsNullOrEmpty(systemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, systemPrompt));
        }

        messages.Add(new ChatMessage(ChatRole.User, message));

        _logger?.LogDebug("发送流式消息: {Message}", message);

        // 构建 MCP JSON-RPC 请求
        var request = new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid().ToString(),
            method = "tools/call",
            @params = new
            {
                name = "chat_stream",
                arguments = new
                {
                    messages = messages
                }
            }
        };

        var jsonResponse = await SendJsonRpcRequestAsync(request, cancellationToken);

        // 解析并逐行返回
        var lines = jsonResponse.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                yield return line;
            }
        }
    }

    /// <summary>
    /// 发送带图片的消息
    /// </summary>
    public async Task<string> SendImageMessageAsync(
        string message,
        string imageBase64,
        string imageType = "image/jpeg",
        string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var messages = new List<ChatMessage>();

        if (!string.IsNullOrEmpty(systemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, systemPrompt));
        }

        // 将 Base64 转换为字节数组
        var imageBytes = Convert.FromBase64String(imageBase64);

        // 创建带图片的消息
        var userMessage = new ChatMessage(ChatRole.User, new List<AIContent>
        {
            new TextContent(message),
            new DataContent(imageBytes, imageType)
        });

        messages.Add(userMessage);

        _logger?.LogDebug("发送图片消息: {Message}, 图片类型: {ImageType}", message, imageType);

        // 构建 MCP JSON-RPC 请求
        var request = new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid().ToString(),
            method = "tools/call",
            @params = new
            {
                name = "chat",
                arguments = new
                {
                    messages = messages
                }
            }
        };

        var jsonResponse = await SendJsonRpcRequestAsync(request, cancellationToken);
        return ParseJsonRpcResponse(jsonResponse);
    }

    /// <summary>
    /// 获取可用的 MCP 工具列表
    /// </summary>
    public async Task<IReadOnlyList<McpTool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var tools = new List<McpTool>();

        // 构建 MCP JSON-RPC 请求
        var request = new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid().ToString(),
            method = "tools/list"
        };

        try
        {
            var jsonResponse = await SendJsonRpcRequestAsync(request, cancellationToken);

            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            if (root.TryGetProperty("result", out var result) &&
                result.TryGetProperty("tools", out var toolsArray))
            {
                foreach (var tool in toolsArray.EnumerateArray())
                {
                    tools.Add(new McpTool
                    {
                        Name = tool.GetProperty("name").GetString() ?? string.Empty,
                        Description = tool.TryGetProperty("description", out var desc) ? desc.GetString() ?? string.Empty : string.Empty,
                        InputSchema = tool.TryGetProperty("inputSchema", out var schema) ? schema.GetRawText() : "{}"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "获取工具列表失败");
        }

        return tools;
    }

    /// <summary>
    /// 调用 MCP 工具
    /// </summary>
    public async Task<string> CallToolAsync(
        string toolName,
        Dictionary<string, object?>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        _logger?.LogInformation("调用工具: {ToolName}, 参数: {Arguments}", toolName, arguments);

        // 构建 MCP JSON-RPC 请求
        var request = new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid().ToString(),
            method = "tools/call",
            @params = new
            {
                name = toolName,
                arguments = arguments ?? new Dictionary<string, object?>()
            }
        };

        var jsonResponse = await SendJsonRpcRequestAsync(request, cancellationToken);
        return ParseJsonRpcResponse(jsonResponse);
    }

    /// <summary>
    /// 测试服务器连接
    /// </summary>
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await SendPingAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "连接测试失败");
            return false;
        }
    }

    /// <summary>
    /// 发送 ping 请求测试连接
    /// </summary>
    private async Task<bool> SendPingAsync(CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = Guid.NewGuid().ToString(),
                method = "ping"
            };

            var response = await SendJsonRpcRequestAsync(request, cancellationToken);
            return !string.IsNullOrEmpty(response) && response.Contains("result");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 发送 JSON-RPC 请求
    /// </summary>
    private async Task<string> SendJsonRpcRequestAsync(object request, CancellationToken cancellationToken)
    {
        EnsureConnected();

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient!.PostAsync("/mcp", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    /// 解析 JSON-RPC 响应
    /// </summary>
    private string ParseJsonRpcResponse(string jsonResponse)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // 检查是否有错误
            if (root.TryGetProperty("error", out var error))
            {
                var errorMessage = error.GetProperty("message").GetString() ?? "Unknown error";
                _lastError = errorMessage;
                _logger?.LogError("MCP 错误: {Error}", errorMessage);
                return $"Error: {errorMessage}";
            }

            // 提取结果
            if (root.TryGetProperty("result", out var result))
            {
                // 如果结果是字符串，直接返回
                if (result.ValueKind == JsonValueKind.String)
                {
                    return result.GetString() ?? string.Empty;
                }

                // 如果是对象，尝试提取 content
                if (result.TryGetProperty("content", out var content))
                {
                    foreach (var item in content.EnumerateArray())
                    {
                        if (item.TryGetProperty("text", out var text))
                        {
                            return text.GetString() ?? string.Empty;
                        }
                    }
                }

                // 否则返回原始 JSON
                return result.GetRawText();
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "解析响应失败");
            return jsonResponse;
        }
    }

    private void EnsureConnected()
    {
        if (!_isConnected || _httpClient == null)
        {
            throw new InvalidOperationException("未连接到 MCP 服务器，请先调用 ConnectAsync");
        }
    }

    /// <summary>
    /// 异步释放资源
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}

/// <summary>
/// MCP 工具信息
/// </summary>
public class McpTool
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InputSchema { get; set; } = "{}";
}

/// <summary>
/// MCP Client 静态工厂类，便于快速创建客户端
/// </summary>
public static class McpClientFactory
{
    /// <summary>
    /// 创建并连接到 MCP 服务器
    /// </summary>
    public static async Task<LocalMCPClient> CreateAndConnectAsync(
        string serverUrl,
        ILogger<LocalMCPClient>? logger = null,
        CancellationToken cancellationToken = default)
    {
        var client = new LocalMCPClient(serverUrl, logger);
        await client.ConnectAsync(cancellationToken);
        return client;
    }
}
