using UnityEngine;

public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance;

    private Checkpoint currentCheckpoint;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // 如果没有手动设置，找到初始检查点
        if (currentCheckpoint == null)
        {
            Checkpoint[] allPoints = FindObjectsOfType<Checkpoint>();
            foreach (var cp in allPoints)
            {
                if (cp.isInitialCheckpoint)
                {
                    currentCheckpoint = cp;
                    break;
                }
            }
        }
    }

    public void SetCurrentCheckpoint(Checkpoint cp)
    {
        currentCheckpoint = cp;
    }

    // 玩家死亡时调用
    public void RespawnPlayer(PlayerControl player)
    {
        if (currentCheckpoint != null && player != null)
        {
            player.transform.position = currentCheckpoint.transform.position;
            player.SetMaxHealth(player.GetMaxHP(), true); // 满血
            Debug.Log("Player respawned at checkpoint: " + currentCheckpoint.name);
        }
        else
        {
            Debug.LogWarning("No checkpoint set or player is null!");
        }
    }
}
