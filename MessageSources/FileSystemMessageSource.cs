using Processors.Interfaces;
using Processors.Models;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Processors.MessageSources;

public class FileSystemMessageSource : IMessageSource
{
    private readonly string _basePath;
    private readonly ILogger<FileSystemMessageSource> _logger;

    public FileSystemMessageSource(string basePath, ILogger<FileSystemMessageSource> logger)
    {
        _basePath = basePath;
        _logger = logger;
        Directory.CreateDirectory(_basePath);
    }

    public async Task<IEnumerable<ProcessorMessage<T>>> PollMessagesAsync<T>(string topic, int maxCount = 10)
    {
        try
        {
            var topicPath = Path.Combine(_basePath, topic, "pending");
            var processingPath = Path.Combine(_basePath, topic, "processing");
            
            Directory.CreateDirectory(topicPath);
            Directory.CreateDirectory(processingPath);

            var files = Directory.GetFiles(topicPath, "*.json")
                .OrderBy(f => File.GetCreationTime(f))
                .Take(maxCount);

            var messages = new List<ProcessorMessage<T>>();

            foreach (var file in files)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(file);
                    var message = JsonConvert.DeserializeObject<ProcessorMessage<T>>(content);
                    
                    if (message != null)
                    {
                        message.MarkReceived();
                        messages.Add(message);

                        // Move to processing folder
                        var processingFile = Path.Combine(processingPath, Path.GetFileName(file));
                        File.Move(file, processingFile);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process file {File}", file);
                }
            }

            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to poll messages from file system for topic {Topic}", topic);
            return new List<ProcessorMessage<T>>();
        }
    }

    public async Task AcknowledgeMessageAsync(string messageId)
    {
        try
        {
            // Find and move the file to completed folder
            var topicFolders = Directory.GetDirectories(_basePath);
            
            foreach (var topicFolder in topicFolders)
            {
                var processingPath = Path.Combine(topicFolder, "processing");
                var completedPath = Path.Combine(topicFolder, "completed");
                
                Directory.CreateDirectory(completedPath);
                
                var files = Directory.GetFiles(processingPath, "*.json");
                foreach (var file in files)
                {
                    var content = await File.ReadAllTextAsync(file);
                    if (content.Contains($"\"Id\":\"{messageId}\""))
                    {
                        var completedFile = Path.Combine(completedPath, Path.GetFileName(file));
                        File.Move(file, completedFile);
                        return;
                    }
                }
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
            return Directory.Exists(_basePath) && await Task.FromResult(true);
        }
        catch
        {
            return false;
        }
    }

    public async Task WriteMessageAsync<T>(ProcessorMessage<T> message, string topic)
    {
        try
        {
            var topicPath = Path.Combine(_basePath, topic, "pending");
            Directory.CreateDirectory(topicPath);

            var fileName = $"{message.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}.json";
            var filePath = Path.Combine(topicPath, fileName);
            
            var content = JsonConvert.SerializeObject(message, Formatting.Indented);
            await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write message to file system for topic {Topic}", topic);
            throw;
        }
    }
}