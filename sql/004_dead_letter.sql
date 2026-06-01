-- ============================================================
-- Dead Letter Events: events that failed after max retries
-- ============================================================
CREATE TABLE IF NOT EXISTS dead_letter_events (
    id                BIGSERIAL    PRIMARY KEY,
    original_event_id BIGINT       NOT NULL,
    topic             TEXT         NOT NULL,
    consumer_group    TEXT         NOT NULL,
    processor_name    TEXT         NOT NULL,
    failed_at         TIMESTAMPTZ  NOT NULL DEFAULT now(),
    attempts          INT          NOT NULL,
    last_error        TEXT         NOT NULL,
    payload           JSONB        NOT NULL,
    headers           JSONB        NULL
);

CREATE INDEX IF NOT EXISTS ix_dead_letter_topic_consumer
    ON dead_letter_events (topic, consumer_group, failed_at DESC);

CREATE INDEX IF NOT EXISTS ix_dead_letter_original_event
    ON dead_letter_events (original_event_id);
