using UnityEngine;
using System.Collections;

// 内嵌IDamageable3接口定义
public interface IDamageable3
{
    void TakeDamage(int damage);
    bool IsDead { get; }
    void Die();
}

// 实现IDamageable3接口，兼容敌人攻击逻辑
public class Alchemist : MonoBehaviour, IDamageable3
{
    [Header("移动属性")]
    [SerializeField] float m_speed = 3.0f;
    [SerializeField] float m_jumpForce = 7.5f;

    [Header("战斗属性")]
    [SerializeField] int m_maxHealth = 100; // 最大血量
    [SerializeField] int m_attackDamage = 18; // 基础攻击伤害
    [SerializeField] float m_attackRange = 2.5f; // 攻击范围
    [SerializeField] float m_attackBufferTime = 1.0f; // 攻击间隔缓冲（1秒只能攻击一次）
    [SerializeField] int m_eliteAttackDamage = 35; // 精英攻击伤害
    [SerializeField] float m_eliteAttackCD = 6.0f; // 精英攻击冷却时间

    [Header("特效设置")]
    [SerializeField] bool m_noBlood = false;

    // 核心组件
    private Animator m_animator;
    private Rigidbody2D m_body2d;
    private Sensor_PixelHeroes m_groundSensor;
    private SpriteRenderer m_spriteRenderer;
    private Collider2D m_collider2d; // 新增：自身碰撞体，避免攻击自身
    private bool m_grounded = false;
    private float m_delayToIdle = 0.0f;

    // 战斗状态
    private int m_currentHealth;
    private bool m_isDead = false;
    private float m_attackBufferTimer = 0.0f; // 普通攻击缓冲计时器
    private float m_eliteAttackCDTimer = 0.0f; // 精英攻击冷却计时器

    void Start()
    {
        // 初始化组件（增加空值判断，避免报错）
        m_animator = GetComponent<Animator>();
        m_body2d = GetComponent<Rigidbody2D>();
        m_spriteRenderer = GetComponent<SpriteRenderer>();
        m_collider2d = GetComponent<Collider2D>();

        // 地面传感器空值保护
        Transform groundSensorTrans = transform.Find("GroundSensor");
        if (groundSensorTrans != null)
        {
            m_groundSensor = groundSensorTrans.GetComponent<Sensor_PixelHeroes>();
        }
        else
        {
            Debug.LogError("[炼金术士] 未找到GroundSensor子物体，请检查物体层级！");
        }

        // 组件缺失提示（便于排查）
        if (m_animator == null) Debug.LogError("[炼金术士] 未挂载Animator组件！");
        if (m_body2d == null) Debug.LogError("[炼金术士] 未挂载Rigidbody2D组件！");
        if (m_spriteRenderer == null) Debug.LogError("[炼金术士] 未挂载SpriteRenderer组件！");

        // 初始化战斗状态
        m_currentHealth = m_maxHealth;
        m_attackBufferTimer = 0.0f;
        m_eliteAttackCDTimer = 0.0f;

        Debug.Log($"[炼金术士] 初始化完成，血量：{m_currentHealth}/{m_maxHealth}");
    }

    void Update()
    {
        // 死亡状态不执行任何逻辑
        if (m_isDead) return;

        // 更新所有计时器
        UpdateTimers();

        // 地面检测（增加空值保护）
        GroundCheck();

        // 处理移动输入（增加空值保护）
        HandleMovement();

        // 处理技能和动画
        HandleSkillsAndAnimations();
    }

    #region 基础逻辑
    // 更新所有计时器（攻击缓冲、精英攻击冷却）
    private void UpdateTimers()
    {
        // 普通攻击缓冲计时器
        if (m_attackBufferTimer > 0)
        {
            m_attackBufferTimer -= Time.deltaTime;
        }

        // 精英攻击冷却计时器
        if (m_eliteAttackCDTimer > 0)
        {
            m_eliteAttackCDTimer -= Time.deltaTime;
        }
    }

    // 地面检测（增加空值保护）
    private void GroundCheck()
    {
        if (m_groundSensor == null || m_animator == null || m_body2d == null) return;

        // 检测落地
        if (!m_grounded && m_groundSensor.State())
        {
            m_grounded = true;
            m_animator.SetBool("Grounded", m_grounded);
        }

        // 检测离地
        if (m_grounded && !m_groundSensor.State())
        {
            m_grounded = false;
            m_animator.SetBool("Grounded", m_grounded);
        }

        // 更新空中速度动画参数
        m_animator.SetFloat("AirSpeedY", m_body2d.velocity.y);
    }

    // 处理移动输入（增加空值保护）
    private void HandleMovement()
    {
        if (m_body2d == null || m_spriteRenderer == null || m_animator == null) return;

        float inputX = Input.GetAxis("Horizontal");

        // 翻转精灵朝向
        if (inputX > 0)
            m_spriteRenderer.flipX = false;
        else if (inputX < 0)
            m_spriteRenderer.flipX = true;

        // 移动
        m_body2d.velocity = new Vector2(inputX * m_speed, m_body2d.velocity.y);

        // 处理移动/闲置动画
        if (Mathf.Abs(inputX) > Mathf.Epsilon)
        {
            m_delayToIdle = 0.05f;
            m_animator.SetInteger("AnimState", 1);
        }
        else
        {
            m_delayToIdle -= Time.deltaTime;
            if (m_delayToIdle < 0)
                m_animator.SetInteger("AnimState", 0);
        }
    }
    #endregion

    #region 技能和动画处理（核心：限制1秒仅触发一次攻击动画）
    private void HandleSkillsAndAnimations()
    {
        // 死亡测试（E键，替换为KeyCode枚举）
        if (Input.GetKeyDown(KeyCode.E))
        {
            Die();
            return;
        }

        // 受击测试（Q键，替换为KeyCode枚举）
        if (Input.GetKeyDown(KeyCode.Q))
        {
            TakeDamage(10);
            return;
        }

        // 精英攻击（鼠标右键，带冷却）
        if (Input.GetMouseButtonDown(1) && m_eliteAttackCDTimer <= 0)
        {
            TriggerEliteAttack();
            m_eliteAttackCDTimer = m_eliteAttackCD;
            return;
        }

        // 普通攻击（鼠标左键，1秒仅触发一次）
        if (Input.GetMouseButtonDown(0) && m_attackBufferTimer <= 0)
        {
            TriggerRandomColorAttack();
            m_attackBufferTimer = m_attackBufferTime; // 重置1秒缓冲
            return;
        }

        // 跳跃（空格键，增加空值保护）
        if (Input.GetKeyDown(KeyCode.Space) && m_grounded && m_groundSensor != null && m_animator != null && m_body2d != null)
        {
            m_animator.SetTrigger("Jump");
            m_grounded = false;
            m_animator.SetBool("Grounded", m_grounded);
            m_body2d.velocity = new Vector2(m_body2d.velocity.x, m_jumpForce);
            m_groundSensor.Disable(0.2f);
        }
    }

    // 触发随机颜色攻击（核心修改：限制动画重复触发 + 空值保护 + 排除自身）
    private void TriggerRandomColorAttack()
    {
        // 双重保险：缓冲未结束则直接返回，不播放动画、不造成伤害
        if (m_attackBufferTimer > 0 || m_animator == null) return;

        // 随机播放攻击动画（保留炼金术士核心特色）
        string[] AttackTriggers = new string[] { "AttackBlue", "AttackGreen", "AttackRed" };
        string randomAttack = AttackTriggers[Random.Range(0, AttackTriggers.Length)];
        m_animator.SetTrigger(randomAttack);

        Debug.Log($"[炼金术士] 释放{randomAttack}，伤害：{m_attackDamage}");

        // 检测攻击范围内的敌人并造成伤害（排除自身）
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, m_attackRange);
        foreach (Collider2D enemy in hitEnemies)
        {
            // 排除自身 + 检测Enemy标签 + 检测IDamageable3接口
            if (enemy != m_collider2d && enemy.CompareTag("Enemy") && enemy.TryGetComponent(out IDamageable3 damageable))
            {
                damageable.TakeDamage(m_attackDamage);
                Debug.Log($"[炼金术士] {randomAttack}命中{enemy.name}，造成{m_attackDamage}点伤害");
            }
        }
    }

    // 触发精英攻击（增加空值保护 + 排除自身）
    private void TriggerEliteAttack()
    {
        if (m_animator == null) return;

        m_animator.SetTrigger("AttackRed"); // 用红色攻击代表精英攻击
        Debug.Log($"[炼金术士] 释放精英攻击，伤害：{m_eliteAttackDamage}（冷却{m_eliteAttackCD}秒）");

        // 扩大精英攻击范围（排除自身）
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, m_attackRange * 1.5f);
        foreach (Collider2D enemy in hitEnemies)
        {
            if (enemy != m_collider2d && enemy.CompareTag("Enemy") && enemy.TryGetComponent(out IDamageable3 damageable))
            {
                damageable.TakeDamage(m_eliteAttackDamage);
                Debug.Log($"[炼金术士] 精英攻击命中{enemy.name}，造成{m_eliteAttackDamage}点伤害");
            }
        }
    }
    #endregion

    #region IDamageable3接口实现（受击/死亡逻辑，增加空值保护）
    public void TakeDamage(int damage)
    {
        if (m_isDead) return;

        // 扣除血量
        m_currentHealth = Mathf.Max(0, m_currentHealth - damage);

        // 空值保护：仅当m_animator不为null时调用SetTrigger
        m_animator?.SetTrigger("Hurt");

        Debug.Log($"[炼金术士] 受到{damage}点伤害，剩余血量：{m_currentHealth}/{m_maxHealth}");

        // 血量为0触发死亡
        if (m_currentHealth <= 0)
        {
            Die();
        }
    }

    public void Die()
    {
        if (m_isDead) return;

        m_isDead = true;
        Debug.Log("[炼金术士] 已死亡");

        // 播放死亡动画（空值保护）
        if (m_animator != null)
        {
            m_animator.SetBool("noBlood", m_noBlood);
            m_animator.SetTrigger("Death");
        }

        // 停止移动（空值保护）
        if (m_body2d != null)
        {
            m_body2d.velocity = Vector2.zero;
            m_body2d.gravityScale = 0; // 停止下落
            m_body2d.isKinematic = true; // 禁用物理交互
        }

        // 禁用碰撞体（空值保护）
        if (m_collider2d != null)
        {
            m_collider2d.enabled = false;
        }

        // 禁用玩家输入
        this.enabled = false;
    }

    // 暴露是否死亡的属性
    public bool IsDead => m_isDead;
    #endregion

    #region Gizmos调试（攻击范围可视化）
    private void OnDrawGizmosSelected()
    {
        // 普通攻击范围（蓝色）
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, m_attackRange);

        // 精英攻击范围（红色）
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, m_attackRange * 1.5f);
    }
    #endregion
}