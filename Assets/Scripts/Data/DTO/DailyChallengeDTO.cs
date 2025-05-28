using System;

// === SERIALIZED FIELDS ===
[Serializable]
public class DailyChallengeDTO
{
    public int      id;                    // unique challenge identifier
    public DateTime challenge_date;        // challenge date in yyyy-MM-dd format
    public string   description;           // description shown to the player
    public int      reward_coins;          // coins awarded when completed
    public int      amount_needed;         // target amount (steps or coins)
    public string   challenge_type;        // "COINS" or "WALK"
}