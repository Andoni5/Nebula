using System;

public enum RarityType
{
    common,
    rare,
    epic,
    legendary
}

// === SERIALIZED FIELDS ===
[Serializable]
public class CosmeticItemDTO
{
    public string     name;          // unique item name
    public string     description;   // item description shown in shop
    public int        price_coins;   // purchase cost in coins
    public RarityType rarity;        // item rarity
    public DateTime   created_at;    // creation timestamp
    public DateTime   updated_at;    // last update timestamp
}