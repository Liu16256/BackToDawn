using UnityEngine;
using System.Collections;

// 炼金术士核心逻辑（实现IDamageable2接口，兼容牧师攻击）
public class Enemy_Alchemist : MonoBehaviour, IDamageable2
{
    [Header("基础属性")]
    [SerializeField] float m_speed = 3.0f; // 移动速度
    [SerializeField] bool m_noBlood = false; // 无血迹效果开关

    [Header("AI行为参数")]
    [SerializeField] float m_detectRange = 6.0f; // 检测玩家的范围
    [SerializeField] float m_attackRange = 2.5f; // 攻击触发范围
    [SerializeField] float m_idleTime = 2.0f;    // 闲置状态持续时间
    [SerializeField] float m_chaseTime = 8.0f;   // 追击玩家的超时时间
    [SerializeField] float m_attackInterval = 1.2f; // 攻击间隔

    [Header("战斗属性")]
    [SerializeField] int m_maxHealth = 250; // 最大血量
    [SerializeField] int m_attackDamage = 20; // 单次攻击伤害

    // 核心组件引用
    private Animator m_animator;
    private Rigidbody2D m_body2d;
    private Sensor_PixelHeroes m_groundSensor;
    private Collider2D m_collider2d;
    private SpriteRenderer m_spriteRenderer;
    private bool m_grounded = false;
    private float m_delayToIdle = 0.0f;

    // AI状态机相关
    private Transform m_playerTransform; // 玩家目标
    private enum AIState { Idle, Patrol, Chase, Attack } // AI状态
    private AIState m_currentState = AIState.Idle; // 当前状态
    private float m_stateTimer = 0.0f; // 状态持续计时器
    private Vector2 m_patrolPointA; // 巡逻点A
    private Vector2 m_patrolPointB; // 巡逻点B
    private bool m_movingToB = true; // 巡逻方向标记

    // 战斗状态相关
    private int m_currentHealth; // 当前血量
    private bool m_isDead = false; // 是否死亡
    private bool m_isAttacking = false; // 攻击锁定

    void Start()
    {
        // 初始化组件（增加空值保护）
        m_animator = GetComponent<Animator>();
        m_body2d = GetComponent<Rigidbody2D>();
        m_collider2d = GetComponent<Collider2D>();
        m_spriteRenderer = GetComponent<SpriteRenderer>();

        // 地面传感器空值保护
        Transform groundSensorTrans = transform.Find("GroundSensor");
        if (groundSensorTrans != null)
        {
            m_groundSensor = groundSensorTrans.GetComponent<Sensor_PixelHeroes>();
        }
        else
        {
            Debug.LogError("[Enemy_Alchemist] 未找到GroundSensor子物体！");
        }

        // 组件缺失提示
        if (m_animator == null) Debug.LogError("[Enemy_Alchemist] 未挂载Animator组件！");
        if (m_body2d == null) Debug.LogError("[Enemy_Alchemist] 未挂载Rigidbody2D组件！");
        if (m_collider2d == null) Debug.LogError("[Enemy_Alchemist] 未挂载Collider2D组件！");
        if (m_spriteRenderer == null) Debug.LogError("[Enemy_Alchemist] 未挂载SpriteRenderer组件！");

        // 初始化刚体物理（防止被玩家推动）
        InitRigidbodySettings();

        // 初始化玩家引用（通过Tag查找）
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            m_playerTransform = playerObj.transform;
        }
        else
        {
            Debug.LogWarning("[Enemy_Alchemist] 未找到Tag为Player的对象！");
        }

        // 初始化巡逻点（基于初始位置左右各3单位）
        Vector3 initPos = transform.position;
        m_patrolPointA = new Vector2(initPos.x - 3f, initPos.y);
        m_patrolPointB = new Vector2(initPos.x + 3f, initPos.y);

        // 初始化血量（核心：必须赋值，否则血量为0）
        m_currentHealth = m_maxHealth;
        Debug.Log($"[Enemy_Alchemist] 初始化完成，血量：{m_currentHealth}/{m_maxHealth}");
    }

    void Update()
    {
        // 死亡状态直接返回
        if (m_isDead) return;

        // 基础地面检测
        GroundCheck();

        // AI状态机核心逻辑更新
        UpdateAIStateMachine();

        // 动画状态更新
        UpdateAnimations();
    }

    #region 核心初始化方法
    // 初始化刚体设置，防止被玩家推动
    private void InitRigidbodySettings()
    {
        if (m_body2d == null) return;

        m_body2d.bodyType = RigidbodyType2D.Dynamic;
        m_body2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        m_body2d.mass = 100f; // 高质量防止被推动
        m_body2d.drag = 10f; // 增加阻力
        m_body2d.angularDrag = 10f; // 禁用旋转
        m_body2d.freezeRotation = true; // 冻结旋转
    }
    #endregion

    #region 地面检测
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

        // 更新动画的空中速度参数
        m_animator.SetFloat("AirSpeedY", m_body2d.velocity.y);
    }
    #endregion

    #region AI状态机核心逻辑
    private void UpdateAIStateMachine()
    {
        // 更新状态计时器
        m_stateTimer += Time.deltaTime;

        // 如果没有找到玩家，默认进入闲置状态
        if (m_playerTransform == null)
        {
            SwitchState(AIState.Idle);
            return;
        }

        // 计算与玩家的距离
        float distanceToPlayer = Vector2.Distance(transform.position, m_playerTransform.position);
        bool isPlayerInDetectRange = distanceToPlayer <= m_detectRange;
        bool isPlayerInAttackRange = distanceToPlayer <= m_attackRange;

        // 根据当前状态执行对应逻辑
        switch (m_currentState)
        {
            case AIState.Idle:
                HandleIdleState(isPlayerInDetectRange);
                break;

            case AIState.Patrol:
                HandlePatrolState(isPlayerInDetectRange);
                break;

            case AIState.Chase:
                HandleChaseState(isPlayerInAttackRange, distanceToPlayer);
                break;

            case AIState.Attack:
                HandleAttackState(isPlayerInAttackRange, distanceToPlayer);
                break;
        }
    }

    // 处理闲置状态
    private void HandleIdleState(bool isPlayerInDetectRange)
    {
        // 停止移动
        if (m_body2d != null)
            m_body2d.velocity = new Vector2(0, m_body2d.velocity.y);

        // 检测到玩家则切换到追击状态
        if (isPlayerInDetectRange)
        {
            SwitchState(AIState.Chase);
            return;
        }

        // 闲置超时则切换到巡逻状态
        if (m_stateTimer >= m_idleTime)
        {
            SwitchState(AIState.Patrol);
        }
    }

    // 处理巡逻状态
    private void HandlePatrolState(bool isPlayerInDetectRange)
    {
        // 检测到玩家则切换到追击状态
        if (isPlayerInDetectRange)
        {
            SwitchState(AIState.Chase);
            return;
        }

        // 执行巡逻移动逻辑
        PatrolMovement();

        // 到达巡逻点则切换回闲置状态
        if (m_movingToB && Vector2.Distance(transform.position, m_patrolPointB) < 0.1f)
        {
            SwitchState(AIState.Idle);
            m_movingToB = false; // 反转下一次巡逻方向
        }
        else if (!m_movingToB && Vector2.Distance(transform.position, m_patrolPointA) < 0.1f)
        {
            SwitchState(AIState.Idle);
            m_movingToB = true; // 反转下一次巡逻方向
        }
    }

    // 处理追击状态
    private void HandleChaseState(bool isPlayerInAttackRange, float distanceToPlayer)
    {
        // 玩家进入攻击范围则切换到攻击状态
        if (isPlayerInAttackRange)
        {
            SwitchState(AIState.Attack);
            return;
        }

        // 追击超时或玩家超出检测范围则返回闲置状态
        if (m_stateTimer >= m_chaseTime || distanceToPlayer > m_detectRange)
        {
            SwitchState(AIState.Idle);
            return;
        }

        // 执行追击移动逻辑
        ChaseMovement();
    }

    // 处理攻击状态
    private void HandleAttackState(bool isPlayerInAttackRange, float distanceToPlayer)
    {
        // 停止移动，准备攻击
        if (m_body2d != null)
            m_body2d.velocity = new Vector2(0, m_body2d.velocity.y);

        // 玩家离开攻击范围则切换回追击状态
        if (!isPlayerInAttackRange)
        {
            m_isAttacking = false;
            SwitchState(AIState.Chase);
            return;
        }

        // 攻击间隔冷却完成则触发攻击
        if (!m_isAttacking && m_stateTimer >= m_attackInterval)
        {
            TriggerAlchemistAttack();
            m_stateTimer = 0; // 重置攻击计时器
            m_isAttacking = true; // 锁定攻击状态
        }
    }

    // 切换AI状态
    private void SwitchState(AIState newState)
    {
        if (m_currentState == newState) return;

        m_currentState = newState;
        m_stateTimer = 0.0f; // 重置状态计时器
        m_isAttacking = false; // 解锁攻击状态
        Debug.Log($"[Enemy_Alchemist] 状态切换：{m_currentState}");
    }
    #endregion

    #region 移动逻辑
    // 巡逻移动
    private void PatrolMovement()
    {
        Vector2 targetPos = m_movingToB ? m_patrolPointB : m_patrolPointA;
        MoveToTarget(targetPos);
    }

    // 追击移动
    private void ChaseMovement()
    {
        if (m_playerTransform == null) return;
        MoveToTarget(m_playerTransform.position);
    }

    // 通用移动方法
    private void MoveToTarget(Vector2 targetPos)
    {
        if (!m_grounded || m_body2d == null || m_spriteRenderer == null) return;

        // 计算移动方向
        float moveDirX = Mathf.Sign(targetPos.x - transform.position.x);
        // 设置移动速度
        m_body2d.velocity = new Vector2(moveDirX * m_speed, m_body2d.velocity.y);
        // 翻转精灵朝向
        m_spriteRenderer.flipX = moveDirX < 0;
    }
    #endregion

    #region 攻击逻辑
    // 触发炼金术士攻击（随机颜色攻击动画）
    private void TriggerAlchemistAttack()
    {
        if (m_isDead || m_playerTransform == null || m_animator == null) return;

        // 随机选择攻击动画触发标签
        string[] attackTriggers = new string[] { "AttackBlue", "AttackGreen", "AttackRed" };
        string randomAttack = attackTriggers[Random.Range(0, attackTriggers.Length)];

        // 播放攻击动画
        m_animator.SetTrigger(randomAttack);
        Debug.Log($"[Enemy_Alchemist] 触发{randomAttack}，伤害：{m_attackDamage}");

        // 给玩家施加伤害（通过IDamageable2接口）
        if (m_playerTransform.TryGetComponent(out IDamageable2 playerDamage))
        {
            playerDamage.TakeDamage(m_attackDamage);
        }

        // 攻击动画播放后解锁攻击状态
        StartCoroutine(UnlockAttackAfterAnimation(0.8f));
    }

    // 延迟解锁攻击状态（匹配攻击动画时长）
    private IEnumerator UnlockAttackAfterAnimation(float delay)
    {
        yield return new WaitForSeconds(delay);
        m_isAttacking = false;
    }
    #endregion

    #region 动画更新
    private void UpdateAnimations()
    {
        if (m_animator == null || m_body2d == null) return;

        // 移动状态：设置跑步动画
        if (Mathf.Abs(m_body2d.velocity.x) > Mathf.Epsilon)
        {
            m_delayToIdle = 0.05f;
            m_animator.SetInteger("AnimState", 1);
        }
        // 闲置状态：设置闲置动画
        else
        {
            m_delayToIdle -= Time.deltaTime;
            if (m_delayToIdle < 0)
                m_animator.SetInteger("AnimState", 0);
        }
    }
    #endregion

    #region IDamageable2接口实现（核心：确保能接收牧师伤害）
    public void TakeDamage(int damage)
    {
        if (m_isDead)
        {
            Debug.Log("[Enemy_Alchemist] 已死亡，无法受击");
            return;
        }

        // 扣除血量（限制最小值为0）
        m_currentHealth = Mathf.Max(0, m_currentHealth - damage);
        Debug.Log($"[Enemy_Alchemist] 受到 {damage} 点伤害，剩余血量：{m_currentHealth}/{m_maxHealth}");

        // 播放受击动画（空值保护）
        if (m_animator != null)
        {
            m_animator.SetTrigger("Hurt");
        }

        // 血量为0则触发死亡
        if (m_currentHealth <= 0)
        {
            Die();
        }
    }

    public void Die()
    {
        if (m_isDead) return;

        m_isDead = true;
        Debug.Log("[Enemy_Alchemist] 已死亡");

        // 播放死亡动画（空值保护）
        if (m_animator != null)
        {
            m_animator.SetBool("noBlood", m_noBlood);
            m_animator.SetTrigger("Death");
        }

        // 停止所有移动
        if (m_body2d != null)
        {
            m_body2d.velocity = Vector2.zero;
            m_body2d.bodyType = RigidbodyType2D.Static;
        }

        // 禁用碰撞体
        if (m_collider2d != null)
        {
            m_collider2d.enabled = false;
        }
    }

    // 暴露是否死亡的属性（牧师会判断）
    public bool IsDead => m_isDead;
    #endregion

    #region Gizmos可视化（调试用）
    private void OnDrawGizmosSelected()
    {
        // 绘制检测范围（黄色）
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, m_detectRange);

        // 绘制攻击范围（红色）
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, m_attackRange);

        // 绘制巡逻点（蓝色）
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(m_patrolPointA, 0.2f);
        Gizmos.DrawSphere(m_patrolPointB, 0.2f);
        Gizmos.DrawLine(m_patrolPointA, m_patrolPointB);
    }
    #endregion
}