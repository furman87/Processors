# Message Processing Framework - Quick Start Guide

This guide will help you get the Message Processing Framework up and running quickly with the new JSON-based configuration system.

## Option 1: Run with Docker Compose (Recommended)

### Prerequisites
- Docker and Docker Compose installed

### Steps
1. Clone or download the project
2. Navigate to the project directory
3. Start all services:
   ```bash
   docker-compose up -d
   ```

This will start:
- PostgreSQL database (port 5433)
- Redis cache (port 6379) 
- Message Processor application (port 8080)

### Access the Application
- **Dashboard**: http://localhost:8080/Dashboard
- **Configuration Management**: http://localhost:8080/ConfigurationManagement
- **API**: http://localhost:8080/api/processors/statistics
- **Health Check**: http://localhost:8080/health

## Option 2: Run Locally

### Prerequisites
- .NET 8 SDK
- PostgreSQL database (running on port 5433)
- Redis (optional)

### Steps

1. **Setup Database**
   ```bash
   # Create database in PostgreSQL (if connecting to existing container on port 5433)
   createdb -h localhost -p 5433 -U postgres messageprocessor
   ```

2. **Update Configuration**
   Edit `appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Port=5433;Database=messageprocessor;Username=postgres;Password=your_password"
     },
     "Redis": {
       "ConnectionString": "localhost:6379"
     }
   }
   ```

3. **Run Application**
   ```bash
   dotnet run
   ```

4. **Access Dashboard**
   Open https://localhost:7071/Dashboard

## Managing Processor Configurations

### Using the Web Interface

1. **Access Configuration Management**
   - Visit https://localhost:7071/ConfigurationManagement
   - Or click "Manage Configurations" from the Dashboard

2. **View Existing Configurations**
   - See all processor configurations in card format
   - View processor type, topics, concurrency settings
   - Check auto-start status and custom settings

3. **Create New Processor**
   - Click "Add Configuration"
   - Fill in the processor details:
     - Name: Unique identifier
     - Processor Type: Implementation class name
     - Input Topic: Topic to poll messages from
     - Output Topics: Comma-separated list of output topics
     - Max Concurrency: Number of parallel message processors
     - Polling Interval: Seconds between polling cycles
     - Auto Start: Whether to start automatically
     - Custom Settings: JSON object with processor-specific settings

4. **Edit Existing Configurations**
   - Click "Edit" on any processor card
   - Modify settings as needed
   - Save changes (processor will restart automatically if running)

5. **Validate Configurations**
   - Click "Validate" to check configuration syntax
   - Fix any validation errors before saving

### Using JSON Files Directly

1. **Configuration File Location**
   - File: `Configuration/processors.json`
   - Created automatically with default processors

2. **Sample Configuration**
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

3. **Hot Reload**
   - Save the JSON file
   - Changes are automatically detected and applied
   - Running processors restart with new settings

## Testing the Framework

### 1. Send a Test Message via API

```bash
curl -X POST "http://localhost:8080/api/messages/email_queue" \
  -H "Content-Type: application/json" \
  -d '{
    "to": "test@example.com",
    "from": "system@example.com", 
    "subject": "Test Message",
    "body": "This is a test message"
  }'
```

### 2. Check Processor Statistics

```bash
curl "http://localhost:8080/api/processors/statistics"
```

### 3. Monitor Dashboard

Visit the dashboard at http://localhost:8080/Dashboard to see:
- Real-time processor status
- Message processing statistics
- Start/stop processor controls

### 4. Manage Configurations

Visit http://localhost:8080/ConfigurationManagement to:
- View all processor configurations
- Create, edit, and delete processors
- Validate configuration syntax
- Reload configurations

## Configuration Management via API

### Get All Configurations
```bash
curl "http://localhost:8080/api/configuration"
```

### Get Specific Configuration
```bash
curl "http://localhost:8080/api/configuration/EmailProcessor"
```

### Create New Configuration
```bash
curl -X POST "http://localhost:8080/api/configuration" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "CustomProcessor",
    "inputTopic": "custom_queue",
    "outputTopics": ["result_queue"],
    "processorType": "CustomMessageProcessor",
    "maxConcurrency": 2,
    "pollingIntervalSeconds": 15,
    "autoStart": true,
    "customSettings": {
      "timeout": 60,
      "retries": 3
    }
  }'
```

### Update Configuration
```bash
curl -X PUT "http://localhost:8080/api/configuration/CustomProcessor" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "CustomProcessor",
    "inputTopic": "custom_queue",
    "outputTopics": ["result_queue", "audit_queue"],
    "processorType": "CustomMessageProcessor",
    "maxConcurrency": 5,
    "pollingIntervalSeconds": 10,
    "autoStart": true,
    "customSettings": {
      "timeout": 120,
      "retries": 5
    }
  }'
```

### Delete Configuration
```bash
curl -X DELETE "http://localhost:8080/api/configuration/CustomProcessor"
```

### Validate Configuration
```bash
curl "http://localhost:8080/api/configuration/EmailProcessor/validate"
```

### Reload All Configurations
```bash
curl -X POST "http://localhost:8080/api/configuration/reload"
```

## Sample Processors

The framework includes these sample processors by default:

1. **EmailProcessor**: 
   - Input: `email_queue`
   - Output: `notification_queue`
   - Processes email messages and creates notifications

2. **DataProcessor**: 
   - Input: `data_queue`
   - Output: `analytics_queue`, `reporting_queue`
   - Processes data messages and creates analytics/reporting data

3. **NotificationProcessor**: 
   - Input: `notification_queue`
   - Output: None (end of chain)
   - Sends notifications via various channels

4. **AnalyticsProcessor**: 
   - Input: `analytics_queue`
   - Output: `reporting_queue`
   - Processes analytics data

5. **ReportingProcessor**: 
   - Input: `reporting_queue`
   - Output: None (end of chain)
   - Generates reports

## Troubleshooting

### Configuration Issues
1. **Invalid JSON**: Use the web interface validator or check JSON syntax
2. **Missing Required Fields**: All required properties must be specified
3. **Duplicate Names**: Processor names must be unique
4. **File Permissions**: Ensure the application can read/write configuration files

### Hot Reload Not Working
1. Check file system permissions on Configuration directory
2. Verify FileSystemWatcher is enabled (enabled by default)
3. Check application logs for configuration errors

### Processor Not Starting
1. Check configuration validation
2. Verify ProcessorType class exists and is registered
3. Check application logs for startup errors
4. Ensure input topic exists and is accessible

### Database Connection Issues
1. Ensure PostgreSQL is running on port 5433
2. Check connection string in configuration includes correct port
3. Verify database exists and user has permissions
4. Test connection: `psql -h localhost -p 5433 -U postgres -d messageprocessor`

### Redis Connection Issues
1. Ensure Redis is running (optional)
2. Check Redis connection string
3. Application will work without Redis using database only

### View Logs
- **Docker**: `docker-compose logs message-processor`
- **Local**: Check `logs/` directory

### Health Checks
Visit `/health` endpoint to check component status:
- Database connectivity
- Redis connectivity (if configured)
- Processor status

## Next Steps

1. **Create Custom Processors**: See README.md for detailed instructions
2. **Configure Message Sources**: Set up additional message sources (Redis, File System, HTTP)
3. **Setup Monitoring**: Configure application insights or metrics collection
4. **Production Deployment**: Use Docker containers and environment-specific configurations
5. **Configuration Management**: Implement configuration versioning and backup strategies

## Support

For issues and questions:
1. Check the configuration validation results
2. Review application logs for error messages
3. Verify all dependencies are running correctly (especially PostgreSQL on port 5433)
4. Check the full README.md for detailed documentation
5. Use the web interface for easier configuration management