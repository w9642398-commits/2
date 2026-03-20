using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Civil3DAIAddon.Interfaces;
using Civil3DAIAddon.Models.AI;

namespace Civil3DAIAddon.Services.AI;

public sealed class OpenAIClient : IOpenAIClient, IDisposable
{
    private readonly IConfigurationService _configService;
    private readonly IActionLogger _logger;
    private readonly HttpClient _httpClient;

    private const string ApiBaseUrl = "https://api.openai.com/v1";

    public OpenAIClient(IConfigurationService configService, IActionLogger logger)
    {
        _configService = configService;
        _logger = logger;
        _httpClient = new HttpClient();
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_configService.GetApiKey());

    public async Task<AIResponse> SendRequestAsync(AIRequest request, CancellationToken ct = default)
    {
        var config = _configService.Load();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        if (!IsConfigured)
            return new AIResponse { ErrorMessage = "OpenAI API key is not configured. Go to Settings to set it." };

        try
        {
            var toolDefs = request.AvailableTools;
            var systemPrompt = SystemPromptBuilder.BuildSystemPrompt(request.DrawingSnapshot, toolDefs);

            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt }
            };

            // Add conversation history
            foreach (var msg in request.ConversationHistory.TakeLast(10))
            {
                messages.Add(new { role = msg.Role, content = msg.Content });
            }

            messages.Add(new { role = "user", content = request.UserPrompt });

            var requestBody = new
            {
                model = config.PrimaryModel,
                messages,
                max_tokens = config.MaxTokens,
                temperature = 0.1,
                response_format = new
                {
                    type = "json_schema",
                    json_schema = new
                    {
                        name = "ExecutionPlan",
                        strict = false,
                        schema = AIPlanJsonSchema.GetSchema()
                    }
                }
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _configService.GetApiKey());
            _httpClient.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);

            var response = await _httpClient.PostAsync($"{ApiBaseUrl}/chat/completions", httpContent, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return new AIResponse
                {
                    ErrorMessage = $"OpenAI API error {response.StatusCode}: {responseBody}",
                    RawResponse = responseBody
                };
            }

            var responseObj = JObject.Parse(responseBody);
            var content = responseObj["choices"]?[0]?["message"]?["content"]?.ToString();
            var tokensUsed = responseObj["usage"]?["total_tokens"]?.Value<int>() ?? 0;

            stopwatch.Stop();

            if (string.IsNullOrEmpty(content))
            {
                return new AIResponse { ErrorMessage = "Empty response from model.", RawResponse = responseBody };
            }

            var plan = JsonConvert.DeserializeObject<AIPlan>(content);

            return new AIResponse
            {
                Plan = plan,
                RawResponse = content,
                TokensUsed = tokensUsed,
                ResponseTime = stopwatch.Elapsed,
                ModelUsed = config.PrimaryModel
            };
        }
        catch (TaskCanceledException)
        {
            return new AIResponse { ErrorMessage = "Request cancelled." };
        }
        catch (HttpRequestException ex)
        {
            return new AIResponse { ErrorMessage = $"Network error: {ex.Message}" };
        }
        catch (JsonException ex)
        {
            return new AIResponse { ErrorMessage = $"Failed to parse model response as plan: {ex.Message}" };
        }
    }

    public async IAsyncEnumerable<string> StreamResponseAsync(
        AIRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var config = _configService.Load();

        if (!IsConfigured)
        {
            yield return "[Error: API key not configured]";
            yield break;
        }

        var systemPrompt = SystemPromptBuilder.BuildSystemPrompt(request.DrawingSnapshot, request.AvailableTools);

        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };

        // Include conversation history for multi-turn context
        foreach (var msg in request.ConversationHistory.TakeLast(10))
        {
            messages.Add(new { role = msg.Role, content = msg.Content });
        }

        messages.Add(new { role = "user", content = request.UserPrompt });

        var requestBody = new
        {
            model = config.PrimaryModel,
            messages,
            max_tokens = config.MaxTokens,
            temperature = 0.1,
            stream = true,
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "ExecutionPlan",
                    strict = false,
                    schema = AIPlanJsonSchema.GetSchema()
                }
            }
        };

        var json = JsonConvert.SerializeObject(requestBody);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _configService.GetApiKey());

        var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            yield return $"[Error: {response.StatusCode} - {err}]";
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];
            if (data == "[DONE]") break;

            JObject? chunk;
            try
            {
                chunk = JObject.Parse(data);
            }
            catch (JsonException)
            {
                _logger.LogError("STREAM", "StreamResponseAsync", new InvalidDataException($"Malformed SSE chunk: {data}"));
                continue;
            }

            var delta = chunk["choices"]?[0]?["delta"]?["content"]?.ToString();
            if (!string.IsNullOrEmpty(delta))
                yield return delta;
        }
    }

    public async Task<string> ClassifyIntentAsync(string userPrompt, CancellationToken ct = default)
    {
        var config = _configService.Load();

        if (!IsConfigured) return "AMBIGUOUS";

        try
        {
            var requestBody = new
            {
                model = config.ClassificationModel,
                messages = new object[]
                {
                    new { role = "system", content = SystemPromptBuilder.BuildClassificationPrompt() },
                    new { role = "user", content = userPrompt }
                },
                max_tokens = 20,
                temperature = 0
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _configService.GetApiKey());

            var response = await _httpClient.PostAsync($"{ApiBaseUrl}/chat/completions", httpContent, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            var result = JObject.Parse(body);
            var category = result["choices"]?[0]?["message"]?["content"]?.ToString()?.Trim().ToUpperInvariant() ?? "AMBIGUOUS";

            // Validate that response is one of the expected categories
            var validCategories = new HashSet<string> { "DRAW", "CIVIL", "MODIFY", "QUERY", "WORKFLOW", "AMBIGUOUS" };
            return validCategories.Contains(category) ? category : "AMBIGUOUS";
        }
        catch
        {
            return "AMBIGUOUS";
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
