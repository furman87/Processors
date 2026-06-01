-- ============================================================
-- Consumer Locks: lease-based distributed locking prevents
-- multiple worker instances from processing the same topic
-- ============================================================
CREATE TABLE IF NOT EXISTS consumer_locks (
    consumer_group TEXT        NOT NULL,
    topic          TEXT        NOT NULL,
    lock_id        TEXT        NOT NULL,   -- instance-unique ID (e.g. hostname-guid)
    locked_until   TIMESTAMPTZ NOT NULL,   -- lock expiry; expired = available to steal
    PRIMARY KEY (consumer_group, topic)
);

CREATE INDEX IF NOT EXISTS ix_consumer_locks_expiry
    ON consumer_locks (locked_until);
