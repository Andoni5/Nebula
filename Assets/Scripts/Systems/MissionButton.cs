using UnityEngine;
using UnityEngine.UI;

public class MissionButton : MonoBehaviour
{
    // === SERIALIZED FIELDS ===
    [HideInInspector] public int ChallengeId;   // Filled when instantiated
    [SerializeField] Image icon;
    [SerializeField] Text  description;

    // === INITIALIZATION METHODS ===
    // Populates the button visuals and metadata with the provided challenge data
    public void Init(DailyChallengeDTO dto, Sprite coins, Sprite walk)
    {
        ChallengeId      = dto.id;
        description.text = dto.description;
        icon.sprite      = dto.challenge_type == "COINS" ? coins : walk;
    }
}