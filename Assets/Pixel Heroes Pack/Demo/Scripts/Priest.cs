using UnityEngine;
using System.Collections;

// 定义IDamageable2接口（替换原IDamageable）
public interface IDamageable2
{
    void TakeDamage(int damage);
    bool IsDead { get; }
    void Die();
}

// 牧师核心逻辑（纯距离判断攻击，无碰撞体依赖）
public class Priest : MonoBehaviour, IDamageable2
{
    [Header("移动属性")]
    [SerializeField] float m_speed = 4.0f;
    [SerializeField] float m_jumpForce = 7.5f;

    [Header("战斗属性")]
    [SerializeField] int m_maxHealth = 100; // 最大血量
    [SerializeField] int m_attackDamage = 20; // 攻击伤害
    [SerializeField] float m_attackRange = 5.0f; // 攻击范围（放大到5方便测试）
    [SerializeField] int m_healAmount = 30; // 治疗量
    [SerializeField] float m_healCD = 8.0f; // 治疗技能冷却时间
    [SerializeField] float m_attackBufferTime = 0.7f; // 攻击间隔缓冲

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
    private float m_attackBufferTimer = 0.0f; // 攻击缓冲计时器
    private float m_healCDTimer = 0.0f; // 治疗技能冷却计时器

    void Start()
    {
        // 初始化组件（增加空值判断）
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
            Debug.LogError("[牧师] 未找到GroundSensor子物体，请检查物体层级！");
        }

        // 组件缺失提示
        if (m_animator == null) Debug.LogError("[牧师] 未挂载Animator组件！");
        if (m_body2d == null) Debug.LogError("[牧师] 未挂载Rigidbody2D组件！");
        if (m_spriteRenderer == null) Debug.LogError("[牧师] 未挂载SpriteRenderer组件！");

        // 初始化血量和计时器
        m_currentHealth = m_maxHealth;
        m_attackBufferTimer = 0.0f;
        m_healCDTimer = 0.0f;

        Debug.Log($"[牧师] 初始化完成，血量：{m_currentHealth}/{m_maxHealth}");
    }

    void Update()
    {
        // 死亡状态不执行任何逻辑
        if (m_isDead) return;

        // 更新计时器
        UpdateTimers();

        // 地面检测
        GroundCheck();

        // 处理输入和移动
        HandleMovement();

        // 处理技能和动画
        HandleSkillsAndAnimations();
    }

    #region 基础逻辑
    // 更新所有计时器（攻击缓冲、治疗冷却）
    private void UpdateTimers()
    {
        // 攻击缓冲计时器
        if (m_attackBufferTimer > 0)
        {
            m_attackBufferTimer -= Time.deltaTime;
        }

        // 治疗冷却计时器
        if (m_healCDTimer > 0)
        {
            m_healCDTimer -= Time.deltaTime;
        }
    }

    // 地面检测
    private void GroundCheck()
    {
        if (m_groundSensor == null) return; // 传感器为空则跳过

        // 检测落地
        if (!m_grounded && m_groundSensor.State())
        {
            m_grounded = true;
            m_animator?.SetBool("Grounded", m_grounded);
        }

        // 检测离地
        if (m_grounded && !m_groundSensor.State())
        {
            m_grounded = false;
            m_animator?.SetBool("Grounded", m_grounded);
        }

        // 更新空中速度动画参数
        if (m_animator != null && m_body2d != null)
        {
            m_animator.SetFloat("AirSpeedY", m_body2d.velocity.y);
        }
    }

    // 处理移动输入
    private void HandleMovement()
    {
        if (m_body2d == null || m_spriteRenderer == null) return;

        float inputX = Input.GetAxis("Horizontal");

        // 翻转精灵朝向
        if (inputX > 0)
            m_spriteRenderer.flipX = false;
        else if (inputX < 0)
            m_spriteRenderer.flipX = true;

        // 设置移动速度
        m_body2d.velocity = new Vector2(inputX * m_speed, m_body2d.velocity.y);

        // 处理移动/闲置动画
        if (m_animator != null)
        {
            if (Mathf.Abs(inputX) > Mathf.Epsilon)
            {
                m_delayToIdle = 0.05f;
                m_animator.SetInteger("AnimState", 1); // 跑步
            }
            else
            {
                m_delayToIdle -= Time.deltaTime;
                if (m_delayToIdle < 0)
                    m_animator.SetInteger("AnimState", 0); // 闲置
            }
        }
    }
    #endregion

    #region 技能和动画处理
    private void HandleSkillsAndAnimations()
    {
        // 死亡测试（E键）
        if (Input.GetKeyDown(KeyCode.E))
        {
            Die();
            return;
        }

        // 受击测试（Q键）
        if (Input.GetKeyDown(KeyCode.Q))
        {
            TakeDamage(10);
            return;
        }

        // 攻击（鼠标左键，带缓冲）
        if (Input.GetMouseButtonDown(0) && m_attackBufferTimer <= 0)
        {
            TriggerAttack();
            m_attackBufferTimer = m_attackBufferTime; // 重置攻击缓冲
            return;
        }

        // 治疗（鼠标右键，带冷却）
        if (Input.GetMouseButtonDown(1) && m_healCDTimer <= 0)
        {
            TriggerHeal();
            m_healCDTimer = m_healCD; // 重置治疗冷却
            return;
        }

        // 跳跃（空格键）
        if (Input.GetKeyDown(KeyCode.Space) && m_grounded && m_body2d != null && m_groundSensor != null)
        {
            m_animator?.SetTrigger("Jump");
            m_grounded = false;
            m_animator?.SetBool("Grounded", m_grounded);
            m_body2d.velocity = new Vector2(m_body2d.velocity.x, m_jumpForce);
            m_groundSensor.Disable(0.2f);
        }
    }

    // 触发攻击（核心：纯距离判断，无碰撞体依赖）
    private void TriggerAttack()
    {
        m_animator?.SetTrigger("Attack");
        Debug.Log($"[牧师] 攻击触发 | 范围：{m_attackRange} | 伤害：{m_attackDamage}");

        // 1. 查找场景中所有Tag为Enemy的敌人
        GameObject[] allEnemies = GameObject.FindGameObjectsWithTag("Enemy");
        if (allEnemies.Length == 0)
        {
            Debug.LogWarning("[牧师] 场景中没有找到Tag为Enemy的敌人！");
            return;
        }
        Debug.Log($"[牧师] 场景中找到 {allEnemies.Length} 个Enemy标签的敌人");

        // 2. 遍历敌人，找攻击范围内的目标
        GameObject targetEnemy = null;
        float minDistance = float.MaxValue;
        foreach (GameObject enemyObj in allEnemies)
        {
            // 跳过空对象
            if (enemyObj == null) continue;

            // 计算与敌人的直线距离
            float distance = Vector2.Distance(transform.position, enemyObj.transform.position);
            Debug.Log($"[牧师] 与 {enemyObj.name} 的距离：{distance}（攻击范围：{m_attackRange}）");

            // 筛选攻击范围内的最近敌人
            if (distance <= m_attackRange && distance < minDistance)
            {
                minDistance = distance;
                targetEnemy = enemyObj;
            }
        }

        // 3. 对目标造成伤害
        if (targetEnemy != null)
        {
            if (targetEnemy.TryGetComponent(out IDamageable2 damageable))
            {
                if (!damageable.IsDead)
                {
                    damageable.TakeDamage(m_attackDamage);
                    Debug.Log($"[牧师] 成功命中 {targetEnemy.name}，造成 {m_attackDamage} 点伤害！");
                }
                else
                {
                    Debug.LogWarning($"[牧师] {targetEnemy.name} 已死亡，无法攻击");
                }
            }
            else
            {
                Debug.LogWarning($"[牧师] {targetEnemy.name} 未实现IDamageable2接口！");
            }
        }
        else
        {
            Debug.LogWarning("[牧师] 攻击范围内没有有效敌人！");
        }
    }

    // 触发治疗
    private void TriggerHeal()
    {
        // 治疗自身（血量不超过最大值）
        m_currentHealth = Mathf.Min(m_currentHealth + m_healAmount, m_maxHealth);
        m_animator?.SetTrigger("Heal");
        Debug.Log($"[牧师] 释放治疗，恢复{m_healAmount}点血量，当前血量：{m_currentHealth}/{m_maxHealth}");
    }
    #endregion

    #region IDamageable2接口实现（受击/死亡）
    public void TakeDamage(int damage)
    {
        if (m_isDead) return;

        // 扣除血量
        m_currentHealth = Mathf.Max(0, m_currentHealth - damage);
        m_animator?.SetTrigger("Hurt");
        Debug.Log($"[牧师] 受到{damage}点伤害，剩余血量：{m_currentHealth}/{m_maxHealth}");

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
        Debug.Log("[牧师] 已死亡");

        // 播放死亡动画
        m_animator?.SetBool("noBlood", m_noBlood);
        m_animator?.SetTrigger("Death");

        // 停止移动 + 禁用物理
        if (m_body2d != null)
        {
            m_body2d.velocity = Vector2.zero;
            m_body2d.gravityScale = 0;
            m_body2d.isKinematic = true;
        }

        // 禁用脚本
        this.enabled = false;
    }

    // 暴露是否死亡的属性
    public bool IsDead => m_isDead;
    #endregion

    #region Gizmos调试（攻击范围可视化）
    private void OnDrawGizmosSelected()
    {
        // 绘制攻击范围（红色）
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, m_attackRange);
    }
    #endregion
}