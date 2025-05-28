using System;

// === SERIALIZED FIELDS ===
[Serializable]
public class CompletedChallengeDTO
{
    public string user_id;        // user identifier (UUID)
    public int    challenge_id;   // associated challenge ID
    public string completed_at;   // completion timestamp (ISO-8601)
    public bool   reward_claimed; // whether reward has been claimed
}