using Processors.Interfaces;
using Processors.Models;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

namespace Processors.MessageSources;

public class HttpApiMessageSource : IMessageSource
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger<HttpApiMessageSource> _logger;

    public HttpApiMessageSource(HttpClient httpClient, string baseUrl, ILogger<HttpApiMessageSource> logger)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
        _logger = logger;
    }

    public async Task<IEnumerable<ProcessorMessage<T>>> PollMessagesAsync<T>(string topic, int maxCount = 10)
    {
        try
        {
            var url = $"{_baseUrl}/api/messages/{topic}?maxCount={maxCount}";
            var response = await _httpClient.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var messages = JsonConvert.DeserializeObject<List<ProcessorMessage<T>>>(content) ?? new List<ProcessorMessage<T>>();
                
                foreach (var message in messages)
                {
                    message.MarkReceived();
                }
                
                return messages;
            }
            
            _logger.LogWarning("Failed to poll messages from HTTP API. Status: {StatusCode}", response.StatusCode);
            return new List<ProcessorMessage<T>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to poll messages from HTTP API for topic {Topic}", topic);
            return new List<ProcessorMessage<T>>();
        }
    }

    public async Task AcknowledgeMessageAsync(string messageId)
    {
        try
        {
            var url = $"{_baseUrl}/api/messages/{messageId}/acknowledge";
            var response = await _httpClient.PostAsync(url, null);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to acknowledge message {MessageId}. Status: {StatusCode}", messageId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acknowledge message {MessageId}", messageId);
        }
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var url = $"{_baseUrl}/health";
            var response = await _httpClient.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task PostMessageAsync<T>(ProcessorMessage<T> message, string topic)
    {
        try
        {
            var url = $"{_baseUrl}/api/messages/{topic}";
            var content = new StringContent(
                JsonConvert.SerializeObject(message),
                System.Text.Encoding.UTF8,
                "application/json");
            
            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post message to HTTP API for topic {Topic}", topic);
            throw;
        }
    }
}