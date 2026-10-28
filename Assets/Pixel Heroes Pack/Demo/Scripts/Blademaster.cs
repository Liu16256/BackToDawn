using UnityEngine;
using System.Collections;

// 内嵌IDamageable6接口定义
public interface IDamageable6
{
    void TakeDamage(int damage);
    bool IsDead { get; }
    void Die();
}

// 实现IDamageable6接口，兼容敌人攻击逻辑
public class Blademaster : MonoBehaviour, IDamageable6
{
    [Header("移动属性")]
    [SerializeField] float m_speed = 4.0f; // 剑圣移速更快（高机动）
    [SerializeField] float m_jumpForce = 8.0f; // 跳跃力略高

    [Header("战斗属性")]
    [SerializeField] int m_maxHealth = 100; // 剑圣血量（偏脆，高机动换血）
    [SerializeField] int m_attackDamage = 22; // 普通攻击伤害
    [SerializeField] float m_attackRange = 1.8f; // 近战攻击范围
    [SerializeField] float m_attackBufferTime = 0.7f; // 攻击间隔（更短，高攻速）
    [SerializeField] int m_bladeStormDamage = 45; // 剑刃风暴伤害
    [SerializeField] float m_bladeStormCD = 6.0f; // 剑刃风暴冷却

    [Header("特效设置")]
    [SerializeField] bool m_noBlood = false;

    // 核心组件
    private Animator m_animator;
    private Rigidbody2D m_body2d;
    private Sensor_PixelHeroes m_groundSensor;
    private SpriteRenderer m_spriteRenderer;
    private bool m_grounded = false;
    private float m_delayToIdle = 0.0f;

    // 战斗状态
    private int m_currentHealth;
    private bool m_isDead = false;
    private float m_attackBufferTimer = 0.0f; // 普通攻击缓冲计时器
    private float m_bladeStormCDTimer = 0.0f; // 剑刃风暴冷却计时器

    void Start()
    {
        // 初始化组件（增加空值保护）
        m_animator = GetComponent<Animator>();
        m_body2d = GetComponent<Rigidbody2D>();
        m_spriteRenderer = GetComponent<SpriteRenderer>();

        // 地面传感器空值保护
        Transform groundSensorTrans = transform.Find("GroundSensor");
        if (groundSensorTrans != null)
        {
            m_groundSensor = groundSensorTrans.GetComponent<Sensor_PixelHeroes>();
        }
        else
        {
            Debug.LogError("[剑圣] 未找到GroundSensor子物体，请检查物体层级！");
        }

        // 组件缺失提示
        if (m_animator == null) Debug.LogError("[剑圣] 未挂载Animator组件！");
        if (m_body2d == null) Debug.LogError("[剑圣] 未挂载Rigidbody2D组件！");

        // 初始化战斗状态
        m_currentHealth = m_maxHealth;
        m_attackBufferTimer = 0.0f;
        m_bladeStormCDTimer = 0.0f;

        Debug.Log($"[剑圣] 初始化完成，血量：{m_currentHealth}/{m_maxHealth}");
    }

    void Update()
    {
        // 死亡状态不执行任何逻辑
        if (m_isDead) return;

        // 更新所有计时器
        UpdateTimers();

        // 地面检测（复用原逻辑，增加空值保护）
        GroundCheck();

        // 处理移动输入
        HandleMovement();

        // 处理技能和动画
        HandleSkillsAndAnimations();
    }

    #region 基础逻辑
    // 更新所有计时器（攻击缓冲、剑刃风暴冷却）
    private void UpdateTimers()
    {
        // 普通攻击缓冲计时器
        if (m_attackBufferTimer > 0)
        {
            m_attackBufferTimer -= Time.deltaTime;
        }

        // 剑刃风暴冷却计时器
        if (m_bladeStormCDTimer > 0)
        {
            m_bladeStormCDTimer -= Time.deltaTime;
        }
    }

    // 地面检测（完全复用原逻辑，增加空值保护）
    private void GroundCheck()
    {
        if (m_groundSensor == null || m_animator == null || m_body2d == null) return;

        //Check if character just landed on the ground
        if (!m_grounded && m_groundSensor.State())
        {
            m_grounded = true;
            m_animator.SetBool("Grounded", m_grounded);
        }

        //Check if character just started falling
        if (m_grounded && !m_groundSensor.State())
        {
            m_grounded = false;
            m_animator.SetBool("Grounded", m_grounded);
        }

        //Set AirSpeed in animator
        m_animator.SetFloat("AirSpeedY", m_body2d.velocity.y);
    }

    // 处理移动输入（优化原逻辑，统一SpriteRenderer引用，增加空值保护）
    private void HandleMovement()
    {
        if (m_body2d == null || m_spriteRenderer == null || m_animator == null) return;

        float inputX = Input.GetAxis("Horizontal");

        // Swap direction of sprite depending on walk direction（优化重复GetComponent）
        if (inputX > 0)
            m_spriteRenderer.flipX = false;
        else if (inputX < 0)
            m_spriteRenderer.flipX = true;

        // Move（保留原移动逻辑）
        m_body2d.velocity = new Vector2(inputX * m_speed, m_body2d.velocity.y);

        // 处理移动/闲置动画（复用原逻辑）
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

    #region 技能和动画处理（保留核心逻辑，新增剑圣特色，适配IDamageable6）
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

        // 剑圣特色：剑刃风暴（鼠标右键，带冷却）
        if (Input.GetMouseButtonDown(1) && m_bladeStormCDTimer <= 0)
        {
            TriggerBladeStorm();
            m_bladeStormCDTimer = m_bladeStormCD; // 重置冷却
            return;
        }

        // 普通攻击（鼠标左键，0.7秒仅触发一次，高攻速）
        if (Input.GetMouseButtonDown(0) && m_attackBufferTimer <= 0)
        {
            TriggerAttack();
            m_attackBufferTimer = m_attackBufferTime; // 重置攻击缓冲
            return;
        }

        // 跳跃（空格键，复用原逻辑，增加空值保护）
        if (Input.GetKeyDown(KeyCode.Space) && m_grounded && m_groundSensor != null && m_animator != null && m_body2d != null)
        {
            m_animator.SetTrigger("Jump");
            m_grounded = false;
            m_animator.SetBool("Grounded", m_grounded);
            m_body2d.velocity = new Vector2(m_body2d.velocity.x, m_jumpForce);
            m_groundSensor.Disable(0.2f);
        }
    }

    // 触发普通攻击（保留原动画，补充伤害检测，适配IDamageable6）
    private void TriggerAttack()
    {
        // 双重保险：缓冲未结束则直接返回
        if (m_attackBufferTimer > 0 || m_animator == null) return;

        // 复用原攻击动画触发逻辑
        m_animator.SetTrigger("Attack");
        Debug.Log($"[剑圣] 普通攻击，伤害：{m_attackDamage}");

        // 检测攻击范围内的敌人并造成伤害
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, m_attackRange);
        foreach (Collider2D enemy in hitEnemies)
        {
            if (enemy.CompareTag("Enemy") && enemy.TryGetComponent(out IDamageable6 damageable))
            {
                damageable.TakeDamage(m_attackDamage);
                Debug.Log($"[剑圣] 攻击命中{enemy.name}，造成{m_attackDamage}点伤害");
            }
        }
    }

    // 触发剑刃风暴（剑圣大招，范围伤害，适配IDamageable6）
    private void TriggerBladeStorm()
    {
        if (m_animator == null) return;

        // 播放剑刃风暴动画（可替换为专属Trigger，如"BladeStorm"）
        m_animator.SetTrigger("Attack"); // 若无专属动画，先用Attack替代
        Debug.Log($"[剑圣] 释放剑刃风暴，伤害：{m_bladeStormDamage}（冷却{m_bladeStormCD}秒）");

        // 剑刃风暴范围更大（普通攻击范围的1.5倍）
        float stormRange = m_attackRange * 1.5f;
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, stormRange);

        foreach (Collider2D enemy in hitEnemies)
        {
            if (enemy.CompareTag("Enemy") && enemy.TryGetComponent(out IDamageable6 damageable))
            {
                damageable.TakeDamage(m_bladeStormDamage);
                Debug.Log($"[剑圣] 剑刃风暴命中{enemy.name}，造成{m_bladeStormDamage}点伤害");
            }
        }
    }
    #endregion

    #region IDamageable6接口实现（受击/死亡逻辑，增加空值保护）
    public void TakeDamage(int damage)
    {
        if (m_isDead) return;

        // 扣除血量（不低于0）
        m_currentHealth = Mathf.Max(0, m_currentHealth - damage);

        // 空值保护：仅当m_animator不为null时调用SetTrigger
        m_animator?.SetTrigger("Hurt");

        Debug.Log($"[剑圣] 受到{damage}点伤害，剩余血量：{m_currentHealth}/{m_maxHealth}");

        // 血量为0触发死亡
        if (m_currentHealth <= 0)
        {
            Die();
        }
        // 剑圣受击反击：短时间提升攻速（特色）
        else
        {
            StartCoroutine(BerserkAfterHurt(1.0f));
        }
    }

    // 受击狂暴：1秒内攻击间隔减半（高攻速特色）
    private IEnumerator BerserkAfterHurt(float berserkTime)
    {
        float originalAttackCD = m_attackBufferTime;
        m_attackBufferTime /= 2; // 攻速翻倍
        yield return new WaitForSeconds(berserkTime);
        m_attackBufferTime = originalAttackCD; // 恢复原间隔
    }

    public void Die()
    {
        if (m_isDead) return;

        m_isDead = true;
        Debug.Log("[剑圣] 已死亡");

        // 复用原死亡动画逻辑（空值保护）
        if (m_animator != null)
        {
            m_animator.SetBool("noBlood", m_noBlood);
            m_animator.SetTrigger("Death");
        }

        // 停止移动（空值保护）
        if (m_body2d != null)
        {
            m_body2d.velocity = Vector2.zero;
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
        // 普通攻击范围（红色）
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, m_attackRange);

        // 剑刃风暴范围（橙色，更大）
        Gizmos.color = new Color(1f, 0.5f, 0f); // 橙色
        Gizmos.DrawWireSphere(transform.position, m_attackRange * 1.5f);
    }
    #endregion
}