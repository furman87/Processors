using Processors.Models;
using Processors.MessageProcessors;
using Processors.Services;
using Processors.MessageSources;
using Microsoft.Extensions.Logging;

namespace Processors.Services;

public class SampleDataSeeder
{
    private readonly DatabaseMessageSource _messageSource;
    private readonly ILogger<SampleDataSeeder> _logger;

    public SampleDataSeeder(DatabaseMessageSource messageSource, ILogger<SampleDataSeeder> logger)
    {
        _messageSource = messageSource;
        _logger = logger;
    }

    public async Task SeedSampleMessagesAsync()
    {
        try
        {
            // Seed email messages
            var emailMessages = new[]
            {
                new ProcessorMessage<EmailMessage>
                {
                    Topic = "email_queue",
                    Payload = new EmailMessage
                    {
                        To = "user1@example.com",
                        From = "system@example.com",
                        Subject = "Welcome to our platform!",
                        Body = "Thank you for joining us.",
                        ScheduledAt = DateTime.UtcNow.AddMinutes(5)
                    }
                },
                new ProcessorMessage<EmailMessage>
                {
                    Topic = "email_queue",
                    Payload = new EmailMessage
                    {
                        To = "user2@example.com",
                        From = "system@example.com",
                        Subject = "Your order confirmation",
                        Body = "Your order has been processed.",
                        ScheduledAt = DateTime.UtcNow.AddMinutes(10)
                    }
                }
            };

            foreach (var message in emailMessages)
            {
                await _messageSource.InsertMessageAsync(message);
            }

            // Seed data messages
            var dataMessages = new[]
            {
                new ProcessorMessage<DataMessage>
                {
                    Topic = "data_queue",
                    Payload = new DataMessage
                    {
                        DataType = "UserActivity",
                        Source = "WebAPI",
                        ReceivedAt = DateTime.UtcNow,
                        Records = new List<Dictionary<string, object>>
                        {
                            new() { ["userId"] = 1, ["action"] = "login", ["timestamp"] = DateTime.UtcNow },
                            new() { ["userId"] = 2, ["action"] = "purchase", ["timestamp"] = DateTime.UtcNow },
                            new() { ["userId"] = 3, ["action"] = "logout", ["timestamp"] = DateTime.UtcNow }
                        }
                    }
                },
                new ProcessorMessage<DataMessage>
                {
                    Topic = "data_queue",
                    Payload = new DataMessage
                    {
                        DataType = "SalesData",
                        Source = "POS",
                        ReceivedAt = DateTime.UtcNow,
                        Records = new List<Dictionary<string, object>>
                        {
                            new() { ["productId"] = 101, ["quantity"] = 2, ["amount"] = 29.99 },
                            new() { ["productId"] = 102, ["quantity"] = 1, ["amount"] = 15.99 }
                        }
                    }
                }
            };

            foreach (var message in dataMessages)
            {
                await _messageSource.InsertMessageAsync(message);
            }

            // Seed notification messages
            var notificationMessages = new[]
            {
                new ProcessorMessage<NotificationMessage>
                {
                    Topic = "notification_queue",
                    Payload = new NotificationMessage
                    {
                        Type = "SystemAlert",
                        Message = "System maintenance scheduled for tonight",
                        Timestamp = DateTime.UtcNow,
                        Metadata = new Dictionary<string, object>
                        {
                            ["priority"] = "high",
                            ["category"] = "maintenance"
                        }
                    }
                }
            };

            foreach (var message in notificationMessages)
            {
                await _messageSource.InsertMessageAsync(message);
            }

            _logger.LogInformation("Sample messages seeded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed sample messages");
            throw;
        }
    }
}