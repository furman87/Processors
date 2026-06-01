-- ============================================================
-- Event Log: append-only event store (primary write/read table)
-- ============================================================
CREATE TABLE IF NOT EXISTS event_log (
    id            BIGSERIAL    PRIMARY KEY,
    topic         TEXT         NOT NULL,
    partition_key TEXT         NULL,
    event_time    TIMESTAMPTZ  NOT NULL DEFAULT now(),
    created_at    TIMESTAMPTZ  NOT NULL DEFAULT now(),
    payload       JSONB        NOT NULL,
    headers       JSONB        NULL
);

-- Primary read path: fetch events for a topic starting from an offset
CREATE INDEX IF NOT EXISTS ix_event_log_topic_id
    ON event_log (topic, id);

-- Time-based range queries
CREATE INDEX IF NOT EXISTS ix_event_log_topic_event_time
    ON event_log (topic, event_time);

-- Partition key ordering (useful for per-key queries)
CREATE INDEX IF NOT EXISTS ix_event_log_topic_partition
    ON event_log (topic, partition_key, id)
    WHERE partition_key IS NOT NULL;

-- Uncomment to enable full JSONB payload search (adds write overhead)
-- CREATE INDEX IF NOT EXISTS ix_event_log_payload_gin
--     ON event_log USING GIN (payload);
