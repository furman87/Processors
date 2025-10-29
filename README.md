# Message Processing Framework

A comprehensive .NET 8 message processing framework with built-in monitoring, multiple message sources, JSON-based processor configuration, and light/dark theme support.

## Features

- ? **Multiple Message Sources**: Database (PostgreSQL), Redis, File System, HTTP API
- ? **JSON Configuration**: File-based processor configuration with hot reload
- ? **Configuration Management**: Web-based configuration editor with validation
- ? **Light/Dark Theme**: Toggle between light and dark modes with persistent preferences
- ? **Configurable Logging**: Built-in logging with Serilog to console, file, and database
- ? **Generic Message Processing**: Process any message type with `IMessageProcessor<T>`
- ? **JSON Message Format**: Structured messages with metadata (creation time, processing times, etc.)
- ? **Topic-based Routing**: One input topic, multiple output topics per processor
- ? **Real-time Dashboard**: Web-based dashboard with processor tiles and statistics
- ? **RESTful API**: Complete API for processor management and statistics
- ? **Health Checks**: Built-in health monitoring for all components
- ? **Auto-scaling**: Configurable concurrency per processor
- ? **Hot Reload**: Configuration changes are automatically detected and applied

## Architecture

### Core Interfaces

- `IMessageSource`: Polls messages from various sources
- `IMessageProcessor<T>`: Processes messages of type T
- `IMessagePublisher`: Publishes messages to output topics
- `IMessageLogger`: Logs message processing events
- `IProcessorEngine`: Manages processor lifecycle and statistics
- `IProcessorConfigurationService`: Manages processor configurations

### Theme System

The framework includes a built-in light/dark theme system:

**Light Theme (Default)**:
- Clean white background with subtle borders
- Bootstrap default color scheme
- Optimized for daytime use

**Dark Theme**:
- Dark background (#1a1a1a) with darker cards (#2d2d2d)
- Green accent colors for running processors
- Blue accent colors for metrics and primary actions
- Optimized for low-light environments

**Theme Features**:
- **Persistent Preferences**: Theme choice saved in localStorage
- **Smooth Transitions**: CSS transitions for theme switching
- **Universal Support**: Works across all pages and components
- **Toggle Button**: Easy access theme switcher in navigation

### Configuration System

Processor configurations are stored in `Configuration/processors.json`:

```json
{
  "Processors": [
    {
      "Name": "EmailProcessor",
      "InputTopic": "email_queue",
      "OutputTopics": ["notification_queue"],
      "ProcessorType": "EmailMessageProcessor",
      "MaxConcurrency": 3,
      "PollingIntervalSeconds": 10,
      "AutoStart": true,
      "CustomSettings": {
        "EmailProvider": "SMTP",
        "RetryAttempts": 3,
        "TimeoutSeconds": 30
      }
    }
  ]
}
```

### Message Structure

```json
{
  "id": "guid",
  "topic": "email_queue",
  "dateTimeCreated": "2024-01-01T10:00:00Z",
  "dateTimeReceived": "2024-01-01T10:00:01Z",
  "dateTimeProcessingComplete": "2024-01-01T10:00:02Z",
  "dateTimeSentToNext": "2024-01-01T10:00:03Z",
  "payload": {
    // Your message data here
  }
}
```

## Quick Start

### 1. Prerequisites

- .NET 8 SDK
- PostgreSQL database
- Redis (optional)

### 2. Database Setup

1. Create a PostgreSQL database
2. Update the connection string in `appsettings.json`
3. Run the application - the schema will be created automatically

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5433;Database=messageprocessor;Username=postgres;Password=your_password"
  }
}
```

### 3. Configuration

Update `appsettings.json` with your settings:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5433;Database=messageprocessor;Username=postgres;Password=password"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "FileSystem": {
    "BasePath": "./MessageData"
  },
  "ProcessorSettings": {
    "AutoStartProcessors": true,
    "MaxConcurrency": 5
  }
}
```

### 4. Run the Application

```bash
dotnet run
```

Visit:
- **Dashboard**: `https://localhost:7071/Dashboard`
- **Configuration Management**: `https://localhost:7071/ConfigurationManagement`
- **API Documentation**: `https://localhost:7071/api/processors/statistics`
- **Health Check**: `https://localhost:7071/health`

## User Interface Features

### Theme Switching

The framework includes a sophisticated light/dark theme system:

**Accessing Theme Settings**:
- Click the moon/sun icon in the navigation bar
- Theme preference is automatically saved
- Changes apply immediately without page refresh

**Light Theme**:
- Clean, professional appearance
- High contrast for readability
- Optimized for bright environments

**Dark Theme**:
- Modern dark interface
- Reduced eye strain in low-light conditions
- Green status indicators for running processors
- Blue accent colors for metrics and actions

### Dashboard Features

The built-in dashboard provides:

- **Real-time Statistics**: Messages per minute, pending count, error count
- **Processor Status**: Running/stopped indicators with start/stop controls
- **Performance Metrics**: Total processed, uptime, success rates
- **Auto-refresh**: Optional auto-refresh every 5 seconds
- **Configuration Link**: Direct access to configuration management
- **Theme Toggle**: Switch between light and dark modes
- **Responsive Design**: Works on desktop and mobile devices
- **All Processors Visible**: Shows all configured processors regardless of AutoStart setting or current state

#### Dashboard Filtering

The dashboard includes powerful filtering capabilities to help you focus on specific processor states:

**Filter Options**:
- **All Processors**: Shows all configured processors (default view)
- **Running**: Shows only processors that are currently running
- **Stopped**: Shows only processors that have been started but are now stopped
- **Not Started**: Shows only processors that have never been started

**Status Summary**:
- **Running Count**: Number of processors currently running (green badge)
- **Stopped Count**: Number of processors that have been started but are now stopped (red badge)
- **Not Started Count**: Number of processors that have never been started (gray badge)
- **With Errors Count**: Number of processors that have recorded errors (yellow badge)
- **Total Count**: Total number of configured processors (blue badge)

**Processor States**:
- **Running**: Processor is actively processing messages (green status indicator)
- **Stopped**: Processor was started but is now stopped (red status indicator)
- **Not Started**: Processor is configured but has never been started (gray status indicator)
- **Error**: Processor has recorded errors during processing (yellow status indicator)

**Always Visible Processors**:
The dashboard now shows all configured processors, regardless of:
- AutoStart setting (`true` or `false`)
- Current running state
- Whether they've ever been started

This ensures complete visibility into your entire processor configuration, making it easy to:
- See which processors are configured but not yet started
- Identify processors with AutoStart: false that might need manual attention
- Get a complete overview of your processing pipeline

**Filter Features**:
- **Real-time Filtering**: Instantly filter processors without page reload
- **Status Indicators**: Visual badges show running (green), stopped (red), not started (gray), error (yellow) states
- **Filter Result Text**: Shows how many processors match the current filter
- **No Results Handling**: Displays helpful message when no processors match filter
- **Filter Persistence**: Filter selection is maintained during auto-refresh

**Usage Examples**:
```
# View all configured processors (including those never started)
Click "All" filter (default)

# View only running processors to monitor active workload
Click "Running" filter

# Check processors that were started but are now stopped
Click "Stopped" filter

# Find processors that have never been started (including AutoStart: false)
Click "Not Started" filter
```

The enhanced filtering system makes it easy to manage large numbers of processors by providing complete visibility into all configured processors while allowing you to focus on specific states as needed.

### Configuration Management Features

The configuration management interface provides:

- **Visual Configuration Editor**: Card-based layout with all processor details
- **Form-based Editing**: User-friendly forms for creating/editing configurations
- **Real-time Validation**: Client and server-side configuration validation
- **JSON Preview**: View raw JSON configuration
- **Hot Reload**: Apply changes without application restart
- **Theme Support**: Full dark/light theme compatibility

## Configuration Management

### Web Interface

Access the configuration management interface at `/ConfigurationManagement` to:

- **View All Configurations**: See all processor configurations in a card layout
- **Create New Configurations**: Add new processors with validation
- **Edit Existing Configurations**: Modify processor settings
- **Delete Configurations**: Remove unused processors
- **Validate Configurations**: Check configuration syntax and requirements
- **Hot Reload**: Apply configuration changes without restarting

### Configuration Properties

| Property | Description | Required |
|----------|-------------|----------|
| `Name` | Unique processor identifier | Yes |
| `InputTopic` | Topic to poll messages from | Yes |
| `OutputTopics` | Array of topics to send results to | No |
| `ProcessorType` | Class name of the processor implementation | Yes |
| `MaxConcurrency` | Maximum concurrent message processing | Yes |
| `PollingIntervalSeconds` | Seconds between polling cycles | Yes |
| `AutoStart` | Whether to start automatically on application startup | No |
| `CustomSettings` | Processor-specific configuration object | No |

### Hot Reload

The framework automatically monitors the `Configuration/processors.json` file for changes:

- **File Watcher**: Detects configuration file modifications
- **Automatic Reload**: Reloads configurations when file changes
- **Processor Restart**: Restarts running processors with new settings
- **Auto-Start**: Starts new processors if `AutoStart` is enabled

## API Endpoints

### Processor Management

- `GET /api/processors/statistics` - Get all processor statistics
- `GET /api/processors/{name}/statistics` - Get specific processor statistics
- `POST /api/processors/{name}/start` - Start a processor
- `POST /api/processors/{name}/stop` - Stop a processor
- `GET /api/processors/{name}/status` - Get processor status

### Configuration Management

- `GET /api/configuration` - Get all configurations
- `GET /api/configuration/{name}` - Get specific configuration
- `POST /api/configuration` - Create new configuration
- `PUT /api/configuration/{name}` - Update configuration
- `DELETE /api/configuration/{name}` - Delete configuration
- `POST /api/configuration/reload` - Reload all configurations
- `GET /api/configuration/{name}/validate` - Validate configuration

### Message Management

- `GET /api/messages/{topic}` - Poll messages from a topic
- `POST /api/messages/{topic}` - Publish a message to a topic
- `POST /api/messages/{messageId}/acknowledge` - Acknowledge message processing

## Creating Custom Processors

### 1. Define Your Message Type

```csharp
public class OrderMessage
{
    public int OrderId { get; set; }
    public string CustomerEmail { get; set; }
    public decimal Total { get; set; }
    public List<OrderItem> Items { get; set; } = new();
}
```

### 2. Implement the Processor

```csharp
public class OrderProcessor : IMessageProcessor<OrderMessage>
{
    private readonly ILogger<OrderProcessor> _logger;

    public OrderProcessor(ILogger<OrderProcessor> logger)
    {
        _logger = logger;
    }

    public async Task<ProcessorResult<OrderMessage>> ProcessAsync(ProcessorMessage<OrderMessage> message)
    {
        try
        {
            // Your processing logic here
            var order = message.Payload;
            
            // Process the order
            await ProcessOrderAsync(order);
            
            // Create output messages for next processors
            var emailMessage = new ProcessorMessage<EmailMessage>
            {
                Payload = new EmailMessage
                {
                    To = order.CustomerEmail,
                    Subject = $"Order {order.OrderId} Confirmation",
                    Body = $"Your order total is ${order.Total}"
                }
            };
            
            var outputMessages = new List<ProcessorMessage<object>> { emailMessage };
            
            return ProcessorResult<OrderMessage>.SuccessResult(message, outputMessages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process order {OrderId}", message.Payload?.OrderId);
            return ProcessorResult<OrderMessage>.ErrorResult($"Processing failed: {ex.Message}");
        }
    }
    
    private async Task ProcessOrderAsync(OrderMessage order)
    {
        // Your business logic here
        await Task.Delay(100); // Simulate processing
    }
}
```

### 3. Register the Processor

In `Program.cs`:

```csharp
builder.Services.AddScoped<OrderProcessor>();
```

### 4. Add Configuration

Create configuration in `Configuration/processors.json` or use the web interface:

```json
{
  "Name": "OrderProcessor",
  "InputTopic": "order_queue",
  "OutputTopics": ["email_queue", "inventory_queue"],
  "ProcessorType": "OrderProcessor",
  "MaxConcurrency": 5,
  "PollingIntervalSeconds": 10,
  "AutoStart": true,
  "CustomSettings": {
    "OrderTimeout": 300,
    "ValidateInventory": true
  }
}
```

## Production Deployment

### Docker Support
The project includes Docker support. Build and run:

```bash
docker build -t message-processor .
docker run -p 8080:80 message-processor
```

### Environment Variables
Override configuration with environment variables:

```bash
ConnectionStrings__DefaultConnection="Host=prod-db;Port=5433;Database=messageprocessor;..."
Redis__ConnectionString="prod-redis:6379"
ProcessorSettings__AutoStartProcessors=true
```

### Configuration Management in Production

- **Volume Mapping**: Mount configuration directory as volume
- **Git Integration**: Store configurations in version control
- **Configuration Backup**: Implement configuration backup strategies
- **Environment-specific Configs**: Use different configurations per environment

## Contributing

1. Fork the repository
2. Create a feature branch
3. Implement your changes
4. Add tests
5. Submit a pull request

## License

This project is licensed under the MIT License.

## Processor Management

### Manual Start/Stop Control

The framework provides comprehensive control over processor lifecycle:

**Dashboard Controls**:
- **Start/Stop Buttons**: Each processor tile shows start/stop buttons based on current state
- **Auto-Start Indicators**: Visual indicators show whether processors are set to auto-start or manual start
- **Real-time Status**: Status indicators show running (green), stopped (red), or error (yellow) states

**Configuration Management**:
- **AutoStart Setting**: Configure processors to start automatically on application startup
- **Quick Actions**: Start/stop processors directly from the configuration management interface
- **Bulk Operations**: 
  - Start all auto-start processors
  - Start all manual processors  
  - Stop all processors
- **Manual Override**: Start any processor regardless of AutoStart setting

### AutoStart vs Manual Start

**AutoStart Processors** (`AutoStart: true`):
- Start automatically when the application begins
- Indicated with green "Auto Start" badge
- Ideal for critical, always-on processors

**Manual Start Processors** (`AutoStart: false`):
- Require manual intervention to start
- Indicated with yellow "Manual Start" badge  
- Perfect for:
  - Development/testing processors
  - Batch processors that run on-demand
  - Processors that should only run under specific conditions

### Starting Processors with AutoStart: false

You can start processors with `AutoStart: false` using several methods:

**1. Dashboard Interface**:
- Navigate to `/Dashboard`
- Click the green "Play" button on any stopped processor
- Works regardless of AutoStart setting

**2. Configuration Management**:
- Navigate to `/ConfigurationManagement`
- Use quick action buttons (play/stop icons) in processor cards
- Use bulk actions dropdown:
  - "Start All Manual" - starts all processors with AutoStart: false
  - "Start All Auto-Start" - starts all processors with AutoStart: true
  - "Stop All Processors" - stops all running processors

**3. API Endpoints**:
```sh
# Start any processor
POST /api/processors/{processorName}/start

# Stop any processor  
POST /api/processors/{processorName}/stop

# Start all auto-start processors
POST /api/processors/start-all-auto-start

# Check processor status
GET /api/processors/{processorName}/status
```

**4. Programmatic Control**:
```csharp
// Inject IProcessorEngine in your service
await _processorEngine.StartAsync("NotificationProcessor");
await _processorEngine.StopAsync("NotificationProcessor");