-- ============================================================
-- Run all migrations in order
-- Usage: psql -U <user> -d <database> -f run_all.sql
-- ============================================================
\echo 'Running 001_event_log.sql...'
\i 001_event_log.sql

\echo 'Running 002_consumer_offsets.sql...'
\i 002_consumer_offsets.sql

\echo 'Running 003_consumer_locks.sql...'
\i 003_consumer_locks.sql

\echo 'Running 004_dead_letter.sql...'
\i 004_dead_letter.sql

\echo 'Running 005_processor_commands.sql...'
\i 005_processor_commands.sql

\echo 'Running 006_processor_heartbeats.sql...'
\i 006_processor_heartbeats.sql

\echo 'All migrations complete.'
