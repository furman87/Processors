-- Message Processing Framework Database Schema for PostgreSQL

-- Table for storing processor messages
CREATE TABLE IF NOT EXISTS processor_messages (
    id VARCHAR(255) PRIMARY KEY,
    topic VARCHAR(255) NOT NULL,
    datetime_created TIMESTAMP WITH TIME ZONE NOT NULL,
    datetime_received TIMESTAMP WITH TIME ZONE,
    datetime_processing_complete TIMESTAMP WITH TIME ZONE,
    datetime_sent_to_next TIMESTAMP WITH TIME ZONE,
    payload_type VARCHAR(255) NOT NULL,
    payload_json TEXT NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    error_message TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Table for message processing logs
CREATE TABLE IF NOT EXISTS message_logs (
    id SERIAL PRIMARY KEY,
    message_id VARCHAR(255) NOT NULL,
    topic VARCHAR(255) NOT NULL,
    status VARCHAR(50) NOT NULL,
    error_message TEXT,
    payload_type VARCHAR(255) NOT NULL,
    payload_json TEXT NOT NULL,
    logged_at TIMESTAMP WITH TIME ZONE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Table for processor statistics
CREATE TABLE IF NOT EXISTS processor_statistics (
    id SERIAL PRIMARY KEY,
    processor_name VARCHAR(255) NOT NULL,
    is_running BOOLEAN NOT NULL,
    messages_per_minute DECIMAL(10,2) NOT NULL DEFAULT 0,
    pending_messages INTEGER NOT NULL DEFAULT 0,
    error_count INTEGER NOT NULL DEFAULT 0,
    last_updated TIMESTAMP WITH TIME ZONE NOT NULL,
    uptime_seconds INTEGER NOT NULL DEFAULT 0,
    total_processed BIGINT NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT 'Stopped',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Indexes for performance
CREATE INDEX IF NOT EXISTS idx_processor_messages_topic_status ON processor_messages(topic, status);
CREATE INDEX IF NOT EXISTS idx_processor_messages_datetime_created ON processor_messages(datetime_created);
CREATE INDEX IF NOT EXISTS idx_message_logs_message_id ON message_logs(message_id);
CREATE INDEX IF NOT EXISTS idx_message_logs_topic ON message_logs(topic);
CREATE INDEX IF NOT EXISTS idx_message_logs_logged_at ON message_logs(logged_at);
CREATE INDEX IF NOT EXISTS idx_processor_statistics_processor_name ON processor_statistics(processor_name);
CREATE INDEX IF NOT EXISTS idx_processor_statistics_last_updated ON processor_statistics(last_updated);

-- Note: Processor configurations are now stored in JSON files instead of database
-- The processor_configs table has been removed in favor of file-based configuration