using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [Header("可选：设置为场景初始检查点")]
    public bool isInitialCheckpoint = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            CheckpointManager.Instance.SetCurrentCheckpoint(this);
            Debug.Log("Checkpoint activated: " + gameObject.name);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = isInitialCheckpoint ? Color.green : Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(1, 2, 1));
    }
}
