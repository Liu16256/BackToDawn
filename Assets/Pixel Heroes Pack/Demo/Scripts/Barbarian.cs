using UnityEngine;
using UnityEngine.UI;
using System.Collections;

// ===================== 全局IDamageable7接口（移除命名空间，修复ExtensionOfNativeClass错误） =====================
public interface IDamageable7
{
    void TakeDamage(int damage);
    bool IsDead { get; }
    void Die();
}

// ===================== 野蛮人核心逻辑（火女结构，无改动） =====================
public class Barbarian : MonoBehaviour, IDamageable7
{
    [Header("移动属性")]
    [SerializeField] float m_speed = 4.0f; // 野蛮人移动速度（偏快）
    [SerializeField] float m_jumpForce = 7.5f; // 跳跃力度
    [SerializeField] bool m_noBlood = false; // 无血迹效果开关

    [Header("战斗属性（距离判定）")]
    [SerializeField] int m_maxHealth = 500; // 野蛮人血量（高，肉盾）
    [SerializeField] int m_attackDamage = 50; // 普攻伤害
    [SerializeField] int m_interactDamage = 80; // 重击/交互伤害
    [SerializeField] float m_attackRange = 2.0f; // 普攻范围
    [SerializeField] float m_interactRange = 2.5f; // 重击范围
    [SerializeField] float m_attackBufferTime = 1.0f; // 攻击间隔

    [Header("物理属性（防推动）")]
    [SerializeField] float m_bodyMass = 100f; // 玩家质量（远大于亡灵法师的30）

    [Header("UI与反馈")]
    [SerializeField] Text m_healthText; // 血量显示文本（可选）

    // 核心组件（火女风格命名/结构）
    private Animator m_animator;
    private Rigidbody2D m_body2d;
    private Sensor_PixelHeroes m_groundSensor;
    private SpriteRenderer m_spriteRenderer;
    private Collider2D m_collider2d;

    // 基础状态（火女风格）
    private bool m_grounded = false;
    private float m_delayToIdle = 0.0f;
    private int m_currentHealth;
    private bool m_isDead = false;
    private bool m_canMove = true; // 控制是否可移动（死亡/受击时禁用）
    private float m_attackBufferTimer = 0f; // 攻击间隔计时器

    void Start()
    {
        // 组件初始化（火女风格：简化空引用处理，保持逻辑一致）
        m_animator = GetComponent<Animator>();
        m_body2d = GetComponent<Rigidbody2D>();
        m_spriteRenderer = GetComponent<SpriteRenderer>();
        m_collider2d = GetComponent<Collider2D>();

        // 地面传感器初始化
        if (transform.Find("GroundSensor") != null)
        {
            m_groundSensor = transform.Find("GroundSensor").GetComponent<Sensor_PixelHeroes>();
        }
        else
        {
            Debug.LogError("[野蛮人] 未找到GroundSensor子物体！请添加地面检测传感器");
        }

        // 刚体设置（火女风格拆分到独立方法）
        InitRigidbodySettings();

        // 血量初始化
        m_currentHealth = m_maxHealth;
        UpdateHealthUI();
        Debug.Log($"[野蛮人] 初始化完成 | 普攻距离: {m_attackRange} | 重击距离: {m_interactRange}");
    }

    void Update()
    {
        // 死亡状态直接返回（火女风格）
        if (m_isDead) return;

        // 攻击冷却更新（火女风格）
        UpdateAttackCooldown();

        // 核心逻辑拆分（火女风格：地面检测→输入处理→动画更新）
        HandleGroundCheck();
        HandlePlayerInput();
        HandleAnimations();
    }

    #region 基础逻辑（火女风格拆分）
    /// <summary>
    /// 初始化刚体参数（防推动）
    /// </summary>
    private void InitRigidbodySettings()
    {
        m_body2d.bodyType = RigidbodyType2D.Dynamic;
        m_body2d.freezeRotation = true;
        m_body2d.drag = 0.5f; // 轻微拖拽，移动更顺滑
        m_body2d.mass = m_bodyMass; // 设置高质量，避免被推动
        m_body2d.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    /// <summary>
    /// 更新攻击冷却计时器
    /// </summary>
    private void UpdateAttackCooldown()
    {
        if (m_attackBufferTimer > 0)
        {
            m_attackBufferTimer -= Time.deltaTime;
        }
    }

    /// <summary>
    /// 地面检测逻辑（火女风格）
    /// </summary>
    private void HandleGroundCheck()
    {
        if (m_groundSensor == null) return;

        bool sensorState = m_groundSensor.State();
        // 落地检测
        if (!m_grounded && sensorState)
        {
            m_grounded = true;
            m_animator.SetBool("Grounded", m_grounded);
        }
        // 下落检测
        else if (m_grounded && !sensorState)
        {
            m_grounded = false;
            m_animator.SetBool("Grounded", m_grounded);
        }

        // 传递空中速度给动画器（火女风格）
        m_animator.SetFloat("AirSpeedY", m_body2d.velocity.y);
    }

    /// <summary>
    /// 动画状态更新（闲置/奔跑）
    /// </summary>
    private void HandleAnimations()
    {
        if (Mathf.Abs(m_body2d.velocity.x) > Mathf.Epsilon)
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

    /// <summary>
    /// 更新血量UI显示
    /// </summary>
    private void UpdateHealthUI()
    {
        if (m_healthText != null)
        {
            m_healthText.text = $"血量：{m_currentHealth}/{m_maxHealth}";
        }
    }
    #endregion

    #region 玩家输入（火女风格：距离判定攻击）
    /// <summary>
    /// 处理玩家所有输入（移动/攻击/跳跃）
    /// </summary>
    private void HandlePlayerInput()
    {
        if (!m_canMove) return;

        // 1. 水平移动输入（火女风格）
        float inputX = Input.GetAxis("Horizontal");
        if (inputX != 0)
        {
            // 翻转精灵朝向
            m_spriteRenderer.flipX = inputX < 0;
            // 移动逻辑
            m_body2d.velocity = new Vector2(inputX * m_speed, m_body2d.velocity.y);
        }
        else
        {
            // 无输入时停止水平移动
            m_body2d.velocity = new Vector2(0, m_body2d.velocity.y);
        }

        // 2. 跳跃输入（火女风格）
        if (Input.GetKeyDown(KeyCode.Space) && m_grounded)
        {
            TriggerJump();
        }

        // 3. 普通攻击（距离判定+冷却）
        if (Input.GetMouseButtonDown(0) && m_attackBufferTimer <= 0)
        {
            TriggerAttackByDistance(m_attackDamage, m_attackRange, "Attack");
            m_attackBufferTimer = m_attackBufferTime;
        }

        // 4. 重击/交互攻击（距离判定+冷却）
        if (Input.GetMouseButtonDown(1) && m_attackBufferTimer <= 0)
        {
            TriggerAttackByDistance(m_interactDamage, m_interactRange, "Interact");
            m_attackBufferTimer = m_attackBufferTime * 1.5f;
        }
    }

    /// <summary>
    /// 跳跃逻辑（火女风格）
    /// </summary>
    private void TriggerJump()
    {
        m_animator.SetTrigger("Jump");
        m_grounded = false;
        m_animator.SetBool("Grounded", m_grounded);
        m_body2d.velocity = new Vector2(m_body2d.velocity.x, m_jumpForce);
        m_groundSensor?.Disable(0.2f); // 空引用保护
    }

    /// <summary>
    /// 距离判定攻击（核心：替代碰撞体检测，火女风格）
    /// </summary>
    /// <param name="damage">攻击伤害</param>
    /// <param name="range">攻击距离</param>
    /// <param name="animTrigger">动画触发名</param>
    private void TriggerAttackByDistance(int damage, float range, string animTrigger)
    {
        // 触发攻击动画
        m_animator.SetTrigger(animTrigger);
        // 可视化攻击范围（火女风格：Scene视图红色/白色圆圈）
        Debug.Log($"[野蛮人] 发起{animTrigger} | 距离: {range} | 伤害: {damage}");

        // 距离判定核心：查找所有亡灵法师敌人
        Enemy_Necromancer[] enemies = FindObjectsOfType<Enemy_Necromancer>();
        bool hitEnemy = false;

        foreach (var enemy in enemies)
        {
            // 跳过已死亡的敌人
            if (enemy.IsDead) continue;

            // 计算与敌人的距离
            float distance = Vector2.Distance(transform.position, enemy.transform.position);
            // 距离判定：小于等于攻击范围则造成伤害
            if (distance <= range)
            {
                // 调用IDamageable7接口方法
                enemy.TakeDamage(damage);
                Debug.Log($"[野蛮人] 命中{enemy.name}，造成{damage}点伤害");
                hitEnemy = true;
            }
        }

        // 无有效敌人提示（火女风格）
        if (!hitEnemy)
        {
            Debug.LogWarning($"[野蛮人] {animTrigger}范围内无存活的亡灵法师！");
        }
    }
    #endregion

    #region IDamageable7接口实现（全局接口，修复ExtensionOfNativeClass错误）
    /// <summary>
    /// 接收伤害（接口实现）
    /// </summary>
    /// <param name="damage">伤害值</param>
    public void TakeDamage(int damage)
    {
        if (m_isDead)
        {
            Debug.Log($"[野蛮人] 已死亡，无法接收伤害");
            return;
        }

        // 扣血并限制最小值为0
        m_currentHealth = Mathf.Max(0, m_currentHealth - damage);
        Debug.Log($"[野蛮人] 受到{damage}点伤害，剩余血量：{m_currentHealth}/{m_maxHealth}");

        // 更新血量UI
        UpdateHealthUI();

        // 触发受击动画
        m_animator.SetTrigger("Hurt");
        // 受击时短暂禁用移动（火女风格：协程实现）
        StartCoroutine(DisableMoveTemporarily(0.2f));

        // 血量为0触发死亡
        if (m_currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 死亡逻辑（接口实现）
    /// </summary>
    public void Die()
    {
        m_isDead = true;
        m_canMove = false; // 禁用所有移动
        Debug.Log("[野蛮人] 已死亡");

        // 停止移动
        m_body2d.velocity = Vector2.zero;
        m_body2d.bodyType = RigidbodyType2D.Static; // 冻结刚体

        // 触发死亡动画
        m_animator.SetBool("noBlood", m_noBlood);
        m_animator.SetTrigger("Death");

        // 禁用碰撞体（可选，避免继续被攻击）
        if (m_collider2d != null)
        {
            m_collider2d.enabled = false;
        }
    }

    /// <summary>
    /// 暴露是否死亡的属性（接口实现）
    /// </summary>
    public bool IsDead => m_isDead;

    /// <summary>
    /// 短暂禁用移动（受击硬直，火女风格）
    /// </summary>
    /// <param name="duration">禁用时长</param>
    private IEnumerator DisableMoveTemporarily(float duration)
    {
        m_canMove = false;
        yield return new WaitForSeconds(duration);
        if (!m_isDead) // 死亡后不再恢复移动
            m_canMove = true;
    }
    #endregion

    #region Gizmos可视化（火女风格）
    private void OnDrawGizmosSelected()
    {
        // 绘制普攻范围（红色）
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, m_attackRange);

        // 绘制重击范围（白色）
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, m_interactRange);

        // 绘制地面传感器范围（如果有）
        if (m_groundSensor != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(m_groundSensor.transform.position, 0.2f);
        }
    }
    #endregion
}