using UnityEngine;

public class EnemyFireball : MonoBehaviour
{
    [Header("基础配置")]
    public float moveSpeed = 8f;          // 飞行速度
    public int damage = 15;               // 伤害值
    public float lifetime = 3f;           // 生命周期
    public LayerMask playerLayer;         // 玩家图层
    public LayerMask obstacleLayer;       // 障碍物图层（墙壁/地面）

    [Header("动画配置")]
    public Animator fireballAnimator;     // 法球动画组件
    public string animStateMove = "Move"; // 移动动画状态名
    public string animStateExplode = "Explode"; // 爆炸动画状态名
    public float explodeAnimDuration = 0.5f; // 爆炸动画时长（需与动画帧匹配）

    private Rigidbody2D rb;
    private Vector2 moveDirection;
    private bool isExploding = false;     // 是否正在爆炸

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        fireballAnimator = GetComponent<Animator>();

        // 初始化播放移动动画
        PlayMoveAnim();
        // 自动销毁（防止爆炸动画未播放完就销毁）
        Destroy(gameObject, lifetime + explodeAnimDuration);
    }

    void FixedUpdate()
    {
        // 爆炸时停止移动
        if (isExploding)
        {
            rb.velocity = Vector2.zero;
            return;
        }
        // 正常飞行
        rb.velocity = moveDirection * moveSpeed;
    }

    // 设置飞行方向（外部调用）
    public void SetDirection(Vector2 direction)
    {
        moveDirection = direction.normalized;
        // 翻转法球朝向（适配左右发射）
        if (direction.x < 0)
            GetComponent<SpriteRenderer>().flipX = true;
    }

    // 播放移动动画
    private void PlayMoveAnim()
    {
        fireballAnimator.Play(animStateMove);
    }

    // 播放爆炸动画
    private void PlayExplodeAnim()
    {
        isExploding = true;
        fireballAnimator.Play(animStateExplode);
        // 禁用碰撞体（防止重复触发）
        GetComponent<Collider2D>().enabled = false;
        // 爆炸动画播放完后销毁
        Invoke(nameof(DestroySelf), explodeAnimDuration);
    }

    // 碰撞检测
    void OnTriggerEnter2D(Collider2D other)
    {
        if (isExploding) return; // 已爆炸则忽略

        // 命中玩家
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            PlayerControl player = other.GetComponent<PlayerControl>();
            if (player != null && player.IsAlive())
            {
                player.TakeDamage(damage);
                Debug.Log($"🔥 法球命中玩家，造成{damage}点伤害");
            }
            PlayExplodeAnim(); // 播放爆炸动画
        }
        // 命中障碍物（墙壁/地面）
        else if (((1 << other.gameObject.layer) & obstacleLayer) != 0)
        {
            PlayExplodeAnim(); // 播放爆炸动画
        }
    }

    // 销毁自身（爆炸动画播放完后调用）
    private void DestroySelf()
    {
        Destroy(gameObject);
    }

    // 防止法球飞出场景未命中任何物体，强制爆炸
    void OnDestroy()
    {
        if (!isExploding)
        {
            PlayExplodeAnim();
        }
    }
}