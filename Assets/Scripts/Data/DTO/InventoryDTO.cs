using System;

// === SERIALIZED FIELDS ===
[Serializable]
public class InventoryDTO
{
    public string  user_id;              // user identifier (UUID)
    public string  item_name;            // references CosmeticItemDTO.name
    public DateTime acquired_at;         // timestamp of acquisition
}