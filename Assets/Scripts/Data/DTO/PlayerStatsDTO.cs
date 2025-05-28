using System;

// === SERIALIZED FIELDS ===
[Serializable]
public class PlayerStatsDTO
{
    public string user_id;                 // user identifier (UUID)
    public int    best_distance;           // longest distance in a single session
    public int    best_coins_earned;       // most coins earned in a single session
    public int    total_sessions;          // total number of sessions played
    public long   total_distance;          // cumulative distance across sessions
    public long   total_coins_collected;   // cumulative coins collected
    public long   total_coins_spent;       // cumulative coins spent
    public int    challenges_completed;    // total challenges completed
    public string actual_skin;             // currently equipped skin
    public DateTime updated_at;            // last update timestamp
}