using UnityEngine;

public class TrapDamage : MonoBehaviour
{
    [Header("伤害设置")]
    [SerializeField] private int damageAmount = 10;  // 陷阱造成的伤害
    [SerializeField] private float damageCooldown = 1f;  // 伤害冷却时间（防止连续扣血）

    [Header("视觉效果")]
    [SerializeField] private GameObject hitEffect;  // 碰撞时触发的特效
    [SerializeField] private AudioClip hitSound;  // 碰撞音效

    [Header("陷阱类型")]
    [SerializeField] private bool isOneTimeTrap = true;  // 是否是一次性陷阱
    [SerializeField] private bool destroyAfterHit = true;  // 碰撞后是否销毁

    private float lastDamageTime = 0f;
    private bool hasTriggered = false;
    private AudioSource audioSource;

    void Start()
    {
        // 尝试获取或添加AudioSource组件
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && hitSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        HandleTrapCollision(other);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        HandleTrapCollision(collision.collider);
    }

    void HandleTrapCollision(Collider2D other)
    {
        // 检查是否碰撞到玩家
        if (!other.CompareTag("Player")) return;

        // 检查冷却时间
        if (Time.time - lastDamageTime < damageCooldown) return;

        // 如果是一次性陷阱且已经触发过，则不再触发
        if (isOneTimeTrap && hasTriggered) return;

        PlayerControl player = other.GetComponent<PlayerControl>();
        if (player == null) return;

        // 对玩家造成伤害
        player.TakeDamage(damageAmount);

        // 更新最后伤害时间
        lastDamageTime = Time.time;
        hasTriggered = true;

        // 播放特效
        PlayHitEffect();

        // 播放音效
        PlayHitSound();

        // 如果需要，销毁陷阱
        if (destroyAfterHit)
        {
            Destroy(gameObject, 0.1f);
        }
    }

    void PlayHitEffect()
    {
        if (hitEffect != null)
        {
            Instantiate(hitEffect, transform.position, Quaternion.identity);
        }
    }

    void PlayHitSound()
    {
        if (hitSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hitSound);
        }
    }

    // 重置陷阱状态
    public void ResetTrap()
    {
        hasTriggered = false;
    }

    // 设置陷阱伤害
    public void SetDamage(int newDamage)
    {
        damageAmount = newDamage;
    }
}