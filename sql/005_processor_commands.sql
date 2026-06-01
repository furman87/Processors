-- ============================================================
-- Processor Commands: dashboard issues commands; workers poll
-- and execute them (start / stop / scale)
-- ============================================================
CREATE TABLE IF NOT EXISTS processor_commands (
    id             BIGSERIAL    PRIMARY KEY,
    processor_name TEXT         NOT NULL,
    consumer_group TEXT         NOT NULL,
    command        TEXT         NOT NULL,   -- 'start' | 'stop' | 'scale'
    parameters     JSONB        NULL,       -- e.g. {"threads": 4}
    issued_by      TEXT         NULL,
    created_at     TIMESTAMPTZ  NOT NULL DEFAULT now(),
    processed_at   TIMESTAMPTZ  NULL,
    processed_by   TEXT         NULL        -- instance_id of worker that ran it
);

-- Fast lookup of unprocessed commands
CREATE INDEX IF NOT EXISTS ix_processor_commands_pending
    ON processor_commands (processor_name, consumer_group, created_at)
    WHERE processed_at IS NULL;
