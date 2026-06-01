-- ============================================================
-- Processor Heartbeats: workers write status every few seconds;
-- dashboard reads this to display real-time processor state
-- ============================================================
CREATE TABLE IF NOT EXISTS processor_heartbeats (
    processor_name   TEXT        NOT NULL,
    consumer_group   TEXT        NOT NULL,
    instance_id      TEXT        NOT NULL,
    topic            TEXT        NOT NULL,
    state            TEXT        NOT NULL,   -- Running | Stopped | Faulted | Starting | Stopping
    events_processed BIGINT      NOT NULL DEFAULT 0,
    error_count      BIGINT      NOT NULL DEFAULT 0,
    active_threads   INT         NOT NULL DEFAULT 0,
    max_threads      INT         NOT NULL DEFAULT 1,
    last_error       TEXT        NULL,
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (processor_name, consumer_group, instance_id)
);

CREATE INDEX IF NOT EXISTS ix_processor_heartbeats_updated
    ON processor_heartbeats (updated_at DESC);
