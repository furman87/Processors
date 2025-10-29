<Query Kind="Program">
  <Namespace>System.Net.Http</Namespace>
  <IncludeUncapsulator>false</IncludeUncapsulator>
</Query>

async Task Main()
{
    // Configuration
    var baseUrl = "http://localhost:8080"; // Change this to your application URL
    var httpClient = new HttpClient();
    
    // Available topics and their message types
    var topics = new Dictionary<string, MessageGenerator>
    {
        ["email_queue"] = new EmailMessageGenerator(),
        ["data_queue"] = new DataMessageGenerator(),
        ["notification_queue"] = new NotificationMessageGenerator(),
        ["analytics_queue"] = new AnalyticsMessageGenerator(),
        ["reporting_queue"] = new ReportingMessageGenerator(),
        ["order_queue"] = new OrderMessageGenerator(),
        ["user_activity_queue"] = new UserActivityMessageGenerator()
    };
    
    Console.WriteLine("=== Message Processing Framework - Test Data Generator ===");
    Console.WriteLine($"Target URL: {baseUrl}");
    Console.WriteLine($"Available Topics: {string.Join(", ", topics.Keys)}");
    Console.WriteLine();
    
    // Interactive menu
    while (true)
    {
        Console.WriteLine("Options:");
        Console.WriteLine("1. Send messages to specific topic");
        Console.WriteLine("2. Send bulk messages to multiple topics");
        Console.WriteLine("3. Send continuous stream (stress test)");
        Console.WriteLine("4. List available topics");
        Console.WriteLine("5. Send single custom message");
        Console.WriteLine("6. Exit");
        Console.Write("Choose option (1-6): ");
        
        var choice = Console.ReadLine();
        Console.WriteLine();
        
        try
        {
            switch (choice)
            {
                case "1":
                    await SendToSpecificTopic(httpClient, baseUrl, topics);
                    break;
                case "2":
                    await SendBulkMessages(httpClient, baseUrl, topics);
                    break;
                case "3":
                    await SendContinuousStream(httpClient, baseUrl, topics);
                    break;
                case "4":
                    ListTopics(topics);
                    break;
                case "5":
                    await SendCustomMessage(httpClient, baseUrl);
                    break;
                case "6":
                    Console.WriteLine("Goodbye!");
                    return;
                default:
                    Console.WriteLine("Invalid option. Please try again.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        
        Console.WriteLine();
    }
}

async Task SendToSpecificTopic(HttpClient httpClient, string baseUrl, Dictionary<string, MessageGenerator> topics)
{
    Console.WriteLine("Available topics:");
    var topicList = topics.Keys.ToList();
    for (int i = 0; i < topicList.Count; i++)
    {
        Console.WriteLine($"{i + 1}. {topicList[i]}");
    }
    
    Console.Write("Select topic (1-" + topicList.Count + "): ");
    if (!int.TryParse(Console.ReadLine(), out int topicIndex) || topicIndex < 1 || topicIndex > topicList.Count)
    {
        Console.WriteLine("Invalid topic selection.");
        return;
    }
    
    var selectedTopic = topicList[topicIndex - 1];
    
    Console.Write("Number of messages to send: ");
    if (!int.TryParse(Console.ReadLine(), out int messageCount) || messageCount < 1)
    {
        Console.WriteLine("Invalid message count.");
        return;
    }
    
    Console.WriteLine($"Sending {messageCount} messages to {selectedTopic}...");
    
    var generator = topics[selectedTopic];
    var tasks = new List<Task>();
    
    for (int i = 0; i < messageCount; i++)
    {
        var message = generator.GenerateMessage();
        tasks.Add(SendMessage(httpClient, baseUrl, selectedTopic, message, i + 1));
        
        // Add small delay to avoid overwhelming the server
        if (i % 10 == 0 && i > 0)
        {
            await Task.Delay(100);
        }
    }
    
    await Task.WhenAll(tasks);
    Console.WriteLine($"? Successfully sent {messageCount} messages to {selectedTopic}");
}

async Task SendBulkMessages(HttpClient httpClient, string baseUrl, Dictionary<string, MessageGenerator> topics)
{
    Console.Write("Number of messages per topic: ");
    if (!int.TryParse(Console.ReadLine(), out int messagesPerTopic) || messagesPerTopic < 1)
    {
        Console.WriteLine("Invalid message count.");
        return;
    }
    
    Console.WriteLine($"Sending {messagesPerTopic} messages to each of {topics.Count} topics...");
    
    var allTasks = new List<Task>();
    
    foreach (var kvp in topics)
    {
        var topic = kvp.Key;
        var generator = kvp.Value;
        
        for (int i = 0; i < messagesPerTopic; i++)
        {
            var message = generator.GenerateMessage();
            allTasks.Add(SendMessage(httpClient, baseUrl, topic, message));
        }
    }
    
    await Task.WhenAll(allTasks);
    Console.WriteLine($"? Successfully sent {messagesPerTopic * topics.Count} total messages");
}

async Task SendContinuousStream(HttpClient httpClient, string baseUrl, Dictionary<string, MessageGenerator> topics)
{
    Console.Write("Messages per second: ");
    if (!int.TryParse(Console.ReadLine(), out int messagesPerSecond) || messagesPerSecond < 1)
    {
        Console.WriteLine("Invalid rate.");
        return;
    }
    
    Console.Write("Duration in seconds: ");
    if (!int.TryParse(Console.ReadLine(), out int durationSeconds) || durationSeconds < 1)
    {
        Console.WriteLine("Invalid duration.");
        return;
    }
    
    Console.WriteLine($"Sending {messagesPerSecond} messages/second for {durationSeconds} seconds...");
    Console.WriteLine("Press any key to stop early...");
    
    var random = new Random();
    var topicList = topics.Keys.ToArray();
    var cancellationToken = new CancellationTokenSource();
    
    // Listen for key press to cancel
    var keyTask = Task.Run(() =>
    {
        Console.ReadKey();
        cancellationToken.Cancel();
    });
    
    var startTime = DateTime.UtcNow;
    var messageCount = 0;
    
    try
    {
        while (!cancellationToken.Token.IsCancellationRequested && 
               DateTime.UtcNow - startTime < TimeSpan.FromSeconds(durationSeconds))
        {
            var intervalStart = DateTime.UtcNow;
            var tasks = new List<Task>();
            
            for (int i = 0; i < messagesPerSecond; i++)
            {
                var topic = topicList[random.Next(topicList.Length)];
                var generator = topics[topic];
                var message = generator.GenerateMessage();
                
                tasks.Add(SendMessage(httpClient, baseUrl, topic, message));
                messageCount++;
            }
            
            await Task.WhenAll(tasks);
            
            // Wait for the remainder of the second
            var elapsed = DateTime.UtcNow - intervalStart;
            var remainingTime = TimeSpan.FromSeconds(1) - elapsed;
            if (remainingTime > TimeSpan.Zero)
            {
                await Task.Delay(remainingTime);
            }
            
            Console.Write($"\rSent: {messageCount} messages, Rate: {messagesPerSecond}/sec");
        }
    }
    finally
    {
        cancellationToken.Cancel();
    }
    
    Console.WriteLine();
    Console.WriteLine($"? Stream completed. Total messages sent: {messageCount}");
}

async Task SendCustomMessage(HttpClient httpClient, string baseUrl)
{
    Console.Write("Enter topic name: ");
    var topic = Console.ReadLine();
    
    Console.WriteLine("Enter JSON payload (or press Enter for sample):");
    var jsonInput = Console.ReadLine();
    
    if (string.IsNullOrWhiteSpace(jsonInput))
    {
        jsonInput = """
        {
            "customField": "test value",
            "timestamp": "2024-01-01T10:00:00Z",
            "data": {
                "key": "value",
                "number": 42
            }
        }
        """;
    }
    
    try
    {
        var testParse = JsonSerializer.Deserialize<object>(jsonInput);
        await SendMessage(httpClient, baseUrl, topic, jsonInput);
        Console.WriteLine($"? Custom message sent to {topic}");
    }
    catch (JsonException ex)
    {
        Console.WriteLine($"? Invalid JSON: {ex.Message}");
    }
}

void ListTopics(Dictionary<string, MessageGenerator> topics)
{
    Console.WriteLine("Available Topics and Message Types:");
    foreach (var kvp in topics)
    {
        Console.WriteLine($"  ?? {kvp.Key} - {kvp.Value.GetType().Name.Replace("Generator", "")}");
    }
}

async Task SendMessage(HttpClient httpClient, string baseUrl, string topic, object message, int? messageNumber = null)
{
    try
    {
        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
        
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync($"{baseUrl}/api/messages/{topic}", content);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"HTTP {response.StatusCode}: {error}");
        }
        
        if (messageNumber.HasValue && messageNumber % 50 == 0)
        {
            Console.Write($"\rSent {messageNumber} messages...");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"? Failed to send message to {topic}: {ex.Message}");
    }
}

// Message Generator Classes
public abstract class MessageGenerator
{
    protected Random Random = new Random();
    public abstract object GenerateMessage();
}

public class EmailMessageGenerator : MessageGenerator
{
    private readonly string[] domains = { "example.com", "test.org", "demo.net", "sample.co" };
    private readonly string[] subjects = { 
        "Welcome to our platform!", "Your order confirmation", "Password reset request",
        "Newsletter subscription", "Account verification", "Special offer inside",
        "Meeting reminder", "System maintenance notice", "Your receipt", "Thank you!"
    };
    
    public override object GenerateMessage()
    {
        return new
        {
            To = $"user{Random.Next(1, 1000)}@{domains[Random.Next(domains.Length)]}",
            From = "system@messageprocessor.com",
            Subject = subjects[Random.Next(subjects.Length)],
            Body = $"This is a test email message generated at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}",
            ScheduledAt = DateTime.UtcNow.AddMinutes(Random.Next(0, 60))
        };
    }
}

public class DataMessageGenerator : MessageGenerator
{
    private readonly string[] dataTypes = { "UserActivity", "SalesData", "ProductViews", "SearchQueries", "Downloads" };
    private readonly string[] sources = { "WebAPI", "MobileApp", "Desktop", "IoT", "Batch" };
    
    public override object GenerateMessage()
    {
        var recordCount = Random.Next(1, 20);
        var records = new List<object>();
        
        for (int i = 0; i < recordCount; i++)
        {
            records.Add(new
            {
                Id = Random.Next(1000, 9999),
                Timestamp = DateTime.UtcNow.AddMinutes(-Random.Next(0, 1440)),
                Value = Math.Round(Random.NextDouble() * 1000, 2),
                Category = $"Category{Random.Next(1, 5)}"
            });
        }
        
        return new
        {
            DataType = dataTypes[Random.Next(dataTypes.Length)],
            Source = sources[Random.Next(sources.Length)],
            ReceivedAt = DateTime.UtcNow,
            Records = records
        };
    }
}

public class NotificationMessageGenerator : MessageGenerator
{
    private readonly string[] types = { "SystemAlert", "UserNotification", "SecurityWarning", "InfoMessage", "ErrorAlert" };
    private readonly string[] messages = {
        "System maintenance scheduled", "New user registration", "Login attempt detected",
        "Data backup completed", "High CPU usage detected", "Storage space low",
        "Update available", "License expiring soon"
    };
    
    public override object GenerateMessage()
    {
        return new
        {
            Type = types[Random.Next(types.Length)],
            Message = messages[Random.Next(messages.Length)],
            Timestamp = DateTime.UtcNow,
            Metadata = new
            {
                Priority = Random.Next(1, 6),
                Source = "TestGenerator",
                Environment = "Development"
            }
        };
    }
}

public class AnalyticsMessageGenerator : MessageGenerator
{
    private readonly string[] sources = { "WebTraffic", "MobileApp", "API", "Dashboard", "Reports" };
    
    public override object GenerateMessage()
    {
        return new
        {
            Source = sources[Random.Next(sources.Length)],
            RecordCount = Random.Next(1, 1000),
            ProcessedAt = DateTime.UtcNow,
            Metrics = new
            {
                ProcessingTimeMs = Random.Next(100, 5000),
                SuccessRate = Math.Round(Random.NextDouble(), 2),
                ErrorCount = Random.Next(0, 10),
                ThroughputPerSecond = Random.Next(10, 500)
            }
        };
    }
}

public class ReportingMessageGenerator : MessageGenerator
{
    private readonly string[] reportTypes = { "DailySummary", "WeeklyReport", "MonthlyAnalytics", "RealTimeMetrics", "ErrorReport" };
    
    public override object GenerateMessage()
    {
        return new
        {
            ReportType = reportTypes[Random.Next(reportTypes.Length)],
            GeneratedAt = DateTime.UtcNow,
            Data = new
            {
                Period = DateTime.UtcNow.AddDays(-Random.Next(1, 30)).ToString("yyyy-MM-dd"),
                TotalRecords = Random.Next(1000, 100000),
                ProcessedRecords = Random.Next(800, 99000),
                ErrorRate = Math.Round(Random.NextDouble() * 0.1, 3)
            }
        };
    }
}

public class OrderMessageGenerator : MessageGenerator
{
    private readonly string[] customerEmails = { 
        "customer1@email.com", "buyer@test.com", "user@example.org", 
        "shopper@demo.net", "client@sample.co" 
    };
    
    public override object GenerateMessage()
    {
        var itemCount = Random.Next(1, 5);
        var items = new List<object>();
        
        for (int i = 0; i < itemCount; i++)
        {
            items.Add(new
            {
                ProductId = Random.Next(100, 999),
                Quantity = Random.Next(1, 10),
                Price = Math.Round(Random.NextDouble() * 100, 2)
            });
        }
        
        return new
        {
            OrderId = Random.Next(10000, 99999),
            CustomerEmail = customerEmails[Random.Next(customerEmails.Length)],
            Total = Math.Round(items.Sum(i => ((dynamic)i).Price * ((dynamic)i).Quantity), 2),
            Items = items,
            OrderDate = DateTime.UtcNow
        };
    }
}

public class UserActivityMessageGenerator : MessageGenerator
{
    private readonly string[] actions = { "login", "logout", "view_product", "add_to_cart", "purchase", "search", "download", "upload" };
    private readonly string[] userAgents = { "Chrome", "Firefox", "Safari", "Edge", "Mobile" };
    
    public override object GenerateMessage()
    {
        return new
        {
            UserId = Random.Next(1, 10000),
            Action = actions[Random.Next(actions.Length)],
            Timestamp = DateTime.UtcNow,
            UserAgent = userAgents[Random.Next(userAgents.Length)],
            IpAddress = $"{Random.Next(1, 255)}.{Random.Next(1, 255)}.{Random.Next(1, 255)}.{Random.Next(1, 255)}",
            SessionId = Guid.NewGuid().ToString(),
            Metadata = new
            {
                PageUrl = $"/page{Random.Next(1, 100)}",
                Referrer = Random.Next(0, 2) == 0 ? null : "https://google.com",
                Duration = Random.Next(1, 300)
            }
        };
    }
}

// Required using statements (add these at the top of your LINQPad script)
#r "System.Net.Http"
#r "System.Text.Json"

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;