-- ============================================================
-- Consumer Offsets: tracks the last processed event ID per
-- (consumer_group, topic) pair
-- ============================================================
CREATE TABLE IF NOT EXISTS consumer_offsets (
    consumer_group   TEXT        NOT NULL,
    topic            TEXT        NOT NULL,
    last_processed_id BIGINT     NOT NULL DEFAULT 0,
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (consumer_group, topic)
);
