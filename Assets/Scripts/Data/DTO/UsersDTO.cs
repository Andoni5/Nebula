using System;

// === SERIALIZED FIELDS ===
[Serializable]
public class UsersDTO
{
    public string   id;             // user identifier (UUID)
    public string   username;       // unique user name
    public string   email;          // email address
    public string   password_hash;  // nullable if external authentication is used
    public DateTime created_at;     // creation timestamp
    public DateTime updated_at;     // last update timestamp
}