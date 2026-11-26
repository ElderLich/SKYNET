CREATE INDEX idx_run_activity_recent ON run_events(run_id, id DESC)
    WHERE event_type <> 'match-launched';
CREATE INDEX idx_match_launches_recent ON run_events(run_id, id DESC)
    WHERE event_type = 'match-launched';
