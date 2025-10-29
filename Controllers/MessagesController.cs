using Microsoft.AspNetCore.Mvc;
using Processors.Interfaces;
using Processors.Models;
using Newtonsoft.Json;

namespace Processors.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly IMessageSource _messageSource;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(IMessageSource messageSource, IMessagePublisher messagePublisher, ILogger<MessagesController> logger)
    {
        _messageSource = messageSource;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    [HttpGet("{topic}")]
    public async Task<ActionResult<IEnumerable<ProcessorMessage<object>>>> GetMessages(string topic, [FromQuery] int maxCount = 10)
    {
        try
        {
            var messages = await _messageSource.PollMessagesAsync<object>(topic, maxCount);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get messages for topic {Topic}", topic);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("{topic}")]
    public async Task<ActionResult> PostMessage(string topic, [FromBody] object payload)
    {
        try
        {
            var message = new ProcessorMessage<object>
            {
                Topic = topic,
                Payload = payload
            };

            await _messagePublisher.PublishAsync(message, topic);
            return Ok(new { messageId = message.Id, message = "Message published successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to topic {Topic}", topic);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("{messageId}/acknowledge")]
    public async Task<ActionResult> AcknowledgeMessage(string messageId)
    {
        try
        {
            await _messageSource.AcknowledgeMessageAsync(messageId);
            return Ok(new { message = "Message acknowledged successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acknowledge message {MessageId}", messageId);
            return StatusCode(500, "Internal server error");
        }
    }
}