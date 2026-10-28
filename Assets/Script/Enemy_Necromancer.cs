using UnityEngine;
using System.Collections;

// ===================== 亡灵法师核心逻辑（术士结构） =====================
public class Enemy_Necromancer : MonoBehaviour, IDamageable7
{
    [Header("基础属性")]
    [SerializeField] float m_speed = 3.0f;
    [SerializeField] float m_jumpForce = 7.5f;
    [SerializeField] bool m_noBlood = false;

    [Header("物理参数（解决推人问题）")]
    [SerializeField] float m_bodyMass = 30f; // 降低质量（默认50→30，玩家质量建议设为80+）
    [SerializeField] PhysicsMaterial2D m_collisionMaterial; // 碰撞材质（选填，增加摩擦力）

    [Header("AI距离判定参数（术士风格）")]
    [SerializeField] float m_detectRange = 8.0f; // 侦查距离
    [SerializeField] float m_attackRange = 3.0f; // 攻击距离
    [SerializeField] float m_spellRange = 6.0f; // 法术距离
    [SerializeField] float m_avoidPlayerDistance = 0.5f; // 与玩家保持最小距离（避免贴脸）

    [Header("战斗属性")]
    [SerializeField] int m_maxHealth = 300;
    [SerializeField] int m_attackDamage = 40; // 普攻伤害
    [SerializeField] int m_spellDamage = 70; // 法术伤害

    [Header("冷却参数（术士风格）")]
    [SerializeField] float m_attackInterval = 2.0f; // 普攻间隔
    [SerializeField] float m_spellCD = 5.0f; // 法术冷却
    [SerializeField] float m_patrolRadius = 4.0f; // 巡逻半径

    [Header("调试选项（术士风格）")]
    [SerializeField] bool m_forceAttack = false;
    [SerializeField] KeyCode m_forceAttackKey = KeyCode.F;
    [SerializeField] bool m_forceSpell = false;
    [SerializeField] KeyCode m_forceSpellKey = KeyCode.G;

    // 核心组件（术士风格命名/结构）
    private Animator m_animator;
    private Rigidbody2D m_body2d;
    private Sensor_PixelHeroes m_groundSensor;
    private SpriteRenderer m_spriteRenderer;
    private Collider2D m_collider2d;

    // 基础状态（术士风格）
    private bool m_grounded = false;
    private float m_delayToIdle = 0.0f;
    private int m_currentHealth;
    private bool m_isDead = false;
    private bool m_isAttacking = false; // 攻击状态锁
    private bool m_isCastingSpell = false; // 施法状态锁

    // AI状态（术士风格：枚举+状态机）
    private Transform m_playerTransform; // 野蛮人玩家
    private enum AIState { Idle, Patrol, Chase, Attack, Spellcast, Hurt }
    private AIState m_currentState = AIState.Idle;
    private float m_stateTimer = 0.0f;
    private Vector2 m_patrolOrigin; // 巡逻原点
    private float m_patrolLeftLimit; // 巡逻左边界
    private float m_patrolRightLimit; // 巡逻右边界
    private float m_patrolDirection = 1f; // 巡逻方向

    // 冷却状态（术士风格）
    private bool m_canAttack = true;
    private float m_attackCooldown = 0f;
    private bool m_canSpell = true;
    private float m_spellCooldown = 0f;

    void Start()
    {
        // 组件初始化（术士风格：简化空引用处理）
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
            Debug.LogError("[亡灵法师] 未找到GroundSensor子物体！");
        }

        // 物理参数初始化（术士风格拆分到独立方法）
        InitPhysicsSettings();

        // 玩家目标初始化（术士风格：按类名查找）
        m_playerTransform = FindObjectOfType<Barbarian>()?.transform;
        if (m_playerTransform == null)
        {
            Debug.LogError("[亡灵法师] 未找到Barbarian类的玩家物体！");
        }
        else
        {
            Debug.Log($"[亡灵法师] 找到野蛮人玩家，侦查距离: {m_detectRange}");
        }

        // 巡逻范围初始化（术士风格）
        InitPatrolRange();

        // 血量初始化
        m_currentHealth = m_maxHealth;
    }

    void Update()
    {
        // 死亡状态直接返回（术士风格）
        if (m_isDead) return;

        // 核心逻辑拆分（术士风格：调试输入→地面检测→冷却更新→AI状态机→动画更新）
        HandleDebugInputs();
        HandleGroundCheck();
        UpdateCooldowns();

        // 攻击/施法时跳过AI状态机（术士风格）
        if (!m_isAttacking && !m_isCastingSpell)
        {
            UpdateAIStateMachine(); // 核心：距离判定AI
        }
        else
        {
            // 攻击/施法时强制停止移动（术士风格）
            m_body2d.velocity = Vector2.zero;
        }

        HandleAnimations();
    }

    #region 基础逻辑（术士风格拆分）
    /// <summary>
    /// 初始化物理参数（解决推人问题，术士风格）
    /// </summary>
    private void InitPhysicsSettings()
    {
        if (m_body2d == null) return;

        // 降低刚体质量，避免推人
        m_body2d.mass = m_bodyMass;
        m_body2d.bodyType = RigidbodyType2D.Dynamic;
        m_body2d.freezeRotation = true;
        m_body2d.drag = 3f; // 增加拖拽，减少惯性
        m_body2d.constraints = RigidbodyConstraints2D.FreezeRotation;

        // 设置碰撞材质（增加摩擦力，减少滑动）
        if (m_collider2d != null && m_collisionMaterial != null)
        {
            m_collider2d.sharedMaterial = m_collisionMaterial;
        }
    }

    /// <summary>
    /// 初始化巡逻范围（术士风格）
    /// </summary>
    private void InitPatrolRange()
    {
        m_patrolOrigin = transform.position;
        m_patrolLeftLimit = m_patrolOrigin.x - m_patrolRadius;
        m_patrolRightLimit = m_patrolOrigin.x + m_patrolRadius;
    }

    /// <summary>
    /// 处理调试输入（强制攻击/施法，术士风格）
    /// </summary>
    private void HandleDebugInputs()
    {
        if (m_forceAttack && Input.GetKeyDown(m_forceAttackKey))
        {
            TriggerAttack();
            return;
        }

        if (m_forceSpell && Input.GetKeyDown(m_forceSpellKey))
        {
            TriggerSpellcast();
            return;
        }
    }

    /// <summary>
    /// 地面检测逻辑（术士风格）
    /// </summary>
    private void HandleGroundCheck()
    {
        if (m_groundSensor == null) return;

        bool newGrounded = m_groundSensor.State();
        if (!m_grounded && newGrounded)
        {
            m_grounded = true;
            m_animator.SetBool("Grounded", m_grounded);
        }
        else if (m_grounded && !newGrounded)
        {
            m_grounded = false;
            m_animator.SetBool("Grounded", m_grounded);
        }

        m_animator.SetFloat("AirSpeedY", m_body2d.velocity.y);
    }

    /// <summary>
    /// 更新冷却计时器（术士风格）
    /// </summary>
    private void UpdateCooldowns()
    {
        // 普攻冷却
        if (!m_canAttack)
        {
            m_attackCooldown -= Time.deltaTime;
            if (m_attackCooldown <= 0)
            {
                m_canAttack = true;
                m_attackCooldown = 0;
                m_isAttacking = false; // 攻击冷却结束，解锁移动
            }
        }

        // 法术冷却
        if (!m_canSpell)
        {
            m_spellCooldown -= Time.deltaTime;
            if (m_spellCooldown <= 0)
            {
                m_canSpell = true;
                m_spellCooldown = 0;
                m_isCastingSpell = false; // 施法冷却结束，解锁移动
            }
        }

        m_stateTimer += Time.deltaTime;
    }

    /// <summary>
    /// 动画状态更新（术士风格）
    /// </summary>
    private void HandleAnimations()
    {
        if (m_animator == null) return;

        // 攻击/施法时强制显示闲置动画（术士风格）
        if (m_isAttacking || m_isCastingSpell)
        {
            m_animator.SetInteger("AnimState", 0);
            return;
        }

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
    #endregion

    #region AI状态机（术士风格：距离判定核心）
    /// <summary>
    /// 更新AI状态机（核心：距离判定）
    /// </summary>
    private void UpdateAIStateMachine()
    {
        if (m_playerTransform == null)
        {
            SwitchState(AIState.Patrol);
            HandlePatrolState();
            return;
        }

        // 核心：计算与玩家的距离（术士风格）
        float distanceToPlayer = Vector2.Distance(transform.position, m_playerTransform.position);
        // 距离判定条件（术士风格：拆分变量，逻辑更清晰）
        bool isPlayerInAttackRange = distanceToPlayer <= m_attackRange;
        bool isPlayerInSpellRange = distanceToPlayer <= m_spellRange;
        bool isPlayerInDetectRange = distanceToPlayer <= m_detectRange;
        bool isPlayerTooClose = distanceToPlayer < m_avoidPlayerDistance; // 玩家过近则后退

        // 可视化距离线（术士风格：Scene视图蓝色线）
        Debug.DrawLine(transform.position, m_playerTransform.position, Color.blue);
        Debug.Log($"[亡灵法师] 距离玩家: {distanceToPlayer:F1} | 攻击范围: {m_attackRange} | 法术范围: {m_spellRange}");

        // 优先级1：玩家过近→优先后退（避免贴脸推人）
        if (isPlayerTooClose)
        {
            SwitchState(AIState.Hurt);
            RetreatFromPlayer();
            return;
        }

        // 优先级2：法术攻击（距离足够+冷却完成）
        if (isPlayerInSpellRange && !isPlayerInAttackRange && m_canSpell)
        {
            SwitchState(AIState.Spellcast);
            HandleSpellcastState();
        }
        // 优先级3：普攻（距离足够+冷却完成）
        else if (isPlayerInAttackRange && m_canAttack)
        {
            SwitchState(AIState.Attack);
            HandleAttackState();
        }
        // 优先级4：追击（侦查范围内）
        else if (isPlayerInDetectRange)
        {
            SwitchState(AIState.Chase);
            HandleChaseState(distanceToPlayer);
        }
        // 优先级5：巡逻/闲置
        else
        {
            SwitchState(m_stateTimer > 1f ? AIState.Patrol : AIState.Idle);
            if (m_currentState == AIState.Patrol) HandlePatrolState();
            else HandleIdleState();
        }
    }

    /// <summary>
    /// 切换AI状态（术士风格：统一管理状态切换）
    /// </summary>
    /// <param name="newState">新状态</param>
    private void SwitchState(AIState newState)
    {
        if (m_currentState == newState) return;

        m_currentState = newState;
        m_stateTimer = 0f;
        Debug.Log($"[亡灵法师] 状态切换：{newState}");
    }

    /// <summary>
    /// 闲置状态逻辑（术士风格）
    /// </summary>
    private void HandleIdleState()
    {
        m_body2d.velocity = new Vector2(0, m_body2d.velocity.y);
    }

    /// <summary>
    /// 巡逻状态逻辑（术士风格）
    /// </summary>
    private void HandlePatrolState()
    {
        float currentX = transform.position.x;

        // 巡逻边界检测
        if (currentX <= m_patrolLeftLimit) m_patrolDirection = 1f;
        else if (currentX >= m_patrolRightLimit) m_patrolDirection = -1f;

        // 巡逻移动
        m_body2d.velocity = new Vector2(m_patrolDirection * m_speed, m_body2d.velocity.y);
        // 翻转精灵朝向
        m_spriteRenderer.flipX = m_patrolDirection < 0;
    }

    /// <summary>
    /// 追击状态逻辑（术士风格：防贴脸）
    /// </summary>
    /// <param name="distance">与玩家的距离</param>
    private void HandleChaseState(float distance)
    {
        // 距离过近则后退（术士风格）
        if (distance < m_avoidPlayerDistance + 0.2f)
        {
            RetreatFromPlayer();
            return;
        }

        // 追击移动
        float dir = Mathf.Sign(m_playerTransform.position.x - transform.position.x);
        m_spriteRenderer.flipX = dir < 0;
        m_body2d.velocity = new Vector2(dir * m_speed, m_body2d.velocity.y);
    }

    /// <summary>
    /// 普攻状态逻辑（术士风格）
    /// </summary>
    private void HandleAttackState()
    {
        // 攻击时强制停止移动
        m_body2d.velocity = Vector2.zero;
        m_isAttacking = true; // 锁定攻击状态
        FacePlayer(); // 面向玩家

        // 触发普攻
        TriggerAttack();
        m_canAttack = false;
        m_attackCooldown = m_attackInterval;
    }

    /// <summary>
    /// 法术状态逻辑（术士风格）
    /// </summary>
    private void HandleSpellcastState()
    {
        // 施法时强制停止移动
        m_body2d.velocity = Vector2.zero;
        m_isCastingSpell = true; // 锁定施法状态
        FacePlayer(); // 面向玩家

        // 触发法术
        TriggerSpellcast();
        m_canSpell = false;
        m_spellCooldown = m_spellCD;
    }

    /// <summary>
    /// 远离玩家（防贴脸，术士风格）
    /// </summary>
    private void RetreatFromPlayer()
    {
        if (m_playerTransform == null || !m_grounded) return;

        float retreatDirX = Mathf.Sign(transform.position.x - m_playerTransform.position.x);
        m_body2d.velocity = new Vector2(retreatDirX * m_speed * 0.8f, m_body2d.velocity.y);
        m_spriteRenderer.flipX = retreatDirX < 0;
    }

    /// <summary>
    /// 面向玩家（术士风格）
    /// </summary>
    private void FacePlayer()
    {
        if (m_playerTransform == null || m_spriteRenderer == null) return;

        float dir = Mathf.Sign(m_playerTransform.position.x - transform.position.x);
        m_spriteRenderer.flipX = dir < 0;
    }
    #endregion

    #region 攻击/施法逻辑（术士风格：调用IDamageable7）
    /// <summary>
    /// 触发普攻（调用玩家IDamageable7接口）
    /// </summary>
    private void TriggerAttack()
    {
        if (m_isDead || m_animator == null) return;

        m_body2d.velocity = Vector2.zero;
        m_animator.SetTrigger("Attack");
        Debug.Log($"[亡灵法师] 近战攻击，造成{m_attackDamage}伤害");

        // 调用玩家的IDamageable7接口
        if (m_playerTransform.TryGetComponent(out IDamageable7 player))
        {
            player.TakeDamage(m_attackDamage);
        }
    }

    /// <summary>
    /// 触法术术（调用玩家IDamageable7接口）
    /// </summary>
    private void TriggerSpellcast()
    {
        if (m_isDead || m_animator == null) return;

        m_body2d.velocity = Vector2.zero;
        m_animator.SetTrigger("Spellcast");
        Debug.Log($"[亡灵法师] 释放法术，造成{m_spellDamage}伤害（冷却{m_spellCD}秒）");

        // 调用玩家的IDamageable7接口
        if (m_playerTransform.TryGetComponent(out IDamageable7 player))
        {
            player.TakeDamage(m_spellDamage);
        }
    }
    #endregion

    #region IDamageable7接口实现（全局接口）
    /// <summary>
    /// 接收伤害（接口实现）
    /// </summary>
    /// <param name="damage">伤害值</param>
    public void TakeDamage(int damage)
    {
        if (m_isDead) return;

        // 受击时解锁攻击/施法状态
        m_isAttacking = false;
        m_isCastingSpell = false;

        // 扣血逻辑
        m_currentHealth = Mathf.Max(0, m_currentHealth - damage);
        Debug.Log($"[亡灵法师] 受到{damage}伤害，剩余血量：{m_currentHealth}/{m_maxHealth}");

        // 触发受击动画+切换状态
        m_animator.SetTrigger("Hurt");
        SwitchState(AIState.Hurt);

        // 血量为0死亡
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
        // 死亡时解锁所有状态
        m_isAttacking = false;
        m_isCastingSpell = false;
        Debug.Log("[亡灵法师] 死亡");

        // 冻结刚体
        m_body2d.velocity = Vector2.zero;
        m_body2d.bodyType = RigidbodyType2D.Static;

        // 死亡动画
        m_animator.SetBool("noBlood", m_noBlood);
        m_animator.SetTrigger("Death");

        // 禁用碰撞
        if (m_collider2d != null) m_collider2d.enabled = false;
    }

    /// <summary>
    /// 暴露是否死亡的属性（接口实现）
    /// </summary>
    public bool IsDead => m_isDead;
    #endregion

    #region Gizmos可视化（术士风格）
    private void OnDrawGizmosSelected()
    {
        // 侦查范围（紫色）
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, m_detectRange);

        // 普攻范围（白色）
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, m_attackRange);

        // 法术范围（蓝色）
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, m_spellRange);

        // 避免贴脸范围（黄色）
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, m_avoidPlayerDistance);

        // 巡逻范围（绿色）
        Gizmos.color = Color.green;
        Gizmos.DrawLine(new Vector2(m_patrolLeftLimit, transform.position.y),
                        new Vector2(m_patrolRightLimit, transform.position.y));
    }
    #endregion
}