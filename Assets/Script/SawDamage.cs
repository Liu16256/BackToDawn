using UnityEngine;

public class SawDamage : MonoBehaviour
{
    [SerializeField]
    private int damageAmount = 20; // 可在Inspector中配置的伤害值

    // 设置伤害值的方法（供外部调用）
    public void SetDamage(int amount)
    {
        damageAmount = amount;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 检查是否碰撞到玩家
        if (other.CompareTag("Player"))
        {
            PlayerControl player = other.GetComponent<PlayerControl>();
            if (player != null && player.IsAlive())
            {
                player.TakeDamage(damageAmount);
                Debug.Log($"锯片造成 {damageAmount} 点伤害", this);
            }
        }
    }

    // 可选：添加与墙壁的碰撞处理（如果需要）
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 例如：碰到墙壁后销毁
        if (collision.gameObject.CompareTag("Wall"))
        {
            // 如果使用对象池，应该返回池中而不是直接销毁
            if (ObjectPooling.instance != null)
            {
                gameObject.SetActive(false);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
