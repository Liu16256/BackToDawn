using UnityEngine;
using System.Collections;


// 实现IDamageable4接口，兼容玩家攻击逻辑
public class Enemy_Blademaster : MonoBehaviour, IDamageable4
{
    [Header("基础属性")]
    [SerializeField] float m_speed = 3.5f; // 剑圣移动速度（略快于野蛮人）
    [SerializeField] bool m_noBlood = false; // 无血迹效果开关

    [Header("AI行为参数")]
    [SerializeField] float m_detectRange = 5.0f; // 检测玩家范围（更远）
    [SerializeField] float m_attackRange = 1.8f; // 攻击触发范围
    [SerializeField] float m_idleTime = 1.0f;    // 闲置状态持续时间（更短）
    [SerializeField] float m_chaseTime = 12.0f;  // 追击玩家的超时时间
    [SerializeField] float m_attackInterval = 0.8f; // 普通攻击间隔（更快）
    [SerializeField] float m_patrolRadius = 2.5f; // 巡逻半径

    [Header("战斗属性")]
    [SerializeField] int m_maxHealth = 300; // 剑圣血量（低于野蛮人）
    [SerializeField] int m_attackDamage = 25; // 普通攻击伤害

    // 核心组件引用（仅声明一次）
    private Animator m_animator;
    private Rigidbody2D m_body2d;
    private Sensor_PixelHeroes m_groundSensor;
    private Collider2D m_collider2d;
    private SpriteRenderer m_spriteRenderer;
    private bool m_grounded = false;
    private float m_delayToIdle = 0.0f;

    // AI状态机相关
    private Transform m_playerTransform; // 玩家目标
    private enum AIState { Idle, Patrol, Chase, Attack }
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
            Debug.LogError("[剑圣] 未找到GroundSensor子物体，请检查物体层级！");
        }

        // 组件缺失提示
        if (m_animator == null) Debug.LogError("[剑圣] 未挂载Animator组件！");
        if (m_body2d == null) Debug.LogError("[剑圣] 未挂载Rigidbody2D组件！");

        // 初始化刚体物理（剑圣更灵活，质量低于野蛮人）
        InitRigidbodySettings();

        // 初始化玩家引用（通过Tag查找）
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            m_playerTransform = playerObj.transform;
        }

        // 初始化巡逻点
        Vector3 initPos = transform.position;
        m_patrolPointA = new Vector2(initPos.x - m_patrolRadius, initPos.y);
        m_patrolPointB = new Vector2(initPos.x + m_patrolRadius, initPos.y);

        // 初始化血量
        m_currentHealth = m_maxHealth;
        Debug.Log($"[剑圣] 初始化完成，血量：{m_currentHealth}/{m_maxHealth}，巡逻半径：{m_patrolRadius}");
    }

    void Update()
    {
        // 死亡状态直接返回，不执行任何逻辑
        if (m_isDead) return;

        // 更新状态计时器
        UpdateTimers();

        // 基础地面检测（复用原逻辑，增加空值保护）
        GroundCheck();

        // AI状态机核心逻辑更新
        UpdateAIStateMachine();

        // 动画状态更新（闲置/移动）
        UpdateAnimations();
    }

    #region 核心初始化方法
    // 初始化刚体设置（剑圣更灵活，增加空值保护）
    private void InitRigidbodySettings()
    {
        if (m_body2d == null) return;

        m_body2d.bodyType = RigidbodyType2D.Dynamic;
        m_body2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        m_body2d.mass = 80f; // 低于野蛮人，更灵活
        m_body2d.drag = 10f;
        m_body2d.angularDrag = 10f;
        m_body2d.freezeRotation = true; // 冻结旋转
    }
    #endregion

    #region 计时器更新
    private void UpdateTimers()
    {
        // 状态计时器
        m_stateTimer += Time.deltaTime;
    }
    #endregion

    #region 地面检测（复用原逻辑，移除玩家输入，增加空值保护）
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

        //Set AirSpeed in animator（复用原逻辑）
        m_animator.SetFloat("AirSpeedY", m_body2d.velocity.y);
    }
    #endregion

    #region AI状态机核心逻辑（对齐Enemy_Barbarian架构）
    private void UpdateAIStateMachine()
    {
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
        // 停止移动（增加空值保护）
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
        if (m_movingToB && Vector2.Distance(transform.position, m_patrolPointB) < 0.2f)
        {
            SwitchState(AIState.Idle);
            m_movingToB = false;
        }
        else if (!m_movingToB && Vector2.Distance(transform.position, m_patrolPointA) < 0.2f)
        {
            SwitchState(AIState.Idle);
            m_movingToB = true;
        }
    }

    // 处理追击状态
    private void HandleChaseState(bool isPlayerInAttackRange, float distanceToPlayer)
    {
        // 玩家进入攻击范围 → 切换到攻击状态
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

        // 执行追击移动逻辑（剑圣追击提速20%，更灵活）
        ChaseMovement();
    }

    // 处理攻击状态
    private void HandleAttackState(bool isPlayerInAttackRange, float distanceToPlayer)
    {
        // 停止移动（增加空值保护）
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
            TriggerNormalAttack();
            m_stateTimer = 0;
            m_isAttacking = true;
        }
    }

    // 切换AI状态
    private void SwitchState(AIState newState)
    {
        if (m_currentState == newState) return;

        m_currentState = newState;
        m_stateTimer = 0.0f;
        m_isAttacking = false;
        Debug.Log($"[剑圣] 状态切换：{m_currentHealth}");
    }
    #endregion

    #region 移动逻辑（对齐Enemy_Barbarian结构，适配剑圣特性，增加空值保护）
    // 巡逻移动
    private void PatrolMovement()
    {
        Vector2 targetPos = m_movingToB ? m_patrolPointB : m_patrolPointA;
        MoveToTarget(targetPos, m_speed);
    }

    // 追击移动
    private void ChaseMovement()
    {
        if (m_playerTransform == null) return;
        MoveToTarget(m_playerTransform.position, m_speed * 1.2f);
    }

    // 通用移动方法
    private void MoveToTarget(Vector2 targetPos, float moveSpeed)
    {
        if (!m_grounded || m_body2d == null || m_spriteRenderer == null) return;

        // 计算移动方向
        float moveDirX = Mathf.Sign(targetPos.x - transform.position.x);
        // 设置移动速度
        m_body2d.velocity = new Vector2(moveDirX * moveSpeed, m_body2d.velocity.y);
        // 翻转精灵朝向（复用原逻辑）
        m_spriteRenderer.flipX = moveDirX < 0;
    }
    #endregion

    #region 攻击逻辑（保留原动画触发，增加空值保护）
    // 触发普通攻击（Attack动画）
    private void TriggerNormalAttack()
    {
        if (m_isDead || m_playerTransform == null || m_animator == null) return;

        // 复用原攻击动画触发逻辑
        m_animator.SetTrigger("Attack");
        Debug.Log($"[剑圣] 普通攻击，伤害：{m_attackDamage}");

        // 给玩家造成伤害（适配IDamageable4）
        if (m_playerTransform.TryGetComponent(out IDamageable4 playerDamage))
        {
            playerDamage.TakeDamage(m_attackDamage);
        }

        // 延迟解锁攻击状态（适配剑圣攻击动画时长）
        StartCoroutine(UnlockAttackAfterAnimation(0.6f));
    }

    // 延迟解锁普通攻击
    private IEnumerator UnlockAttackAfterAnimation(float delay)
    {
        yield return new WaitForSeconds(delay);
        m_isAttacking = false;
    }
    #endregion

    #region 动画更新（复用原逻辑，移除玩家输入，增加空值保护）
    private void UpdateAnimations()
    {
        if (m_animator == null || m_body2d == null) return;

        //Run（复用原逻辑）
        if (Mathf.Abs(m_body2d.velocity.x) > Mathf.Epsilon)
        {
            m_delayToIdle = 0.05f;
            m_animator.SetInteger("AnimState", 1);
        }
        //Idle（复用原逻辑）
        else
        {
            m_delayToIdle -= Time.deltaTime;
            if (m_delayToIdle < 0)
                m_animator.SetInteger("AnimState", 0);
        }
    }
    #endregion

    #region IDamageable4接口实现（受击/死亡，复用原动画逻辑，增加空值保护）
    public void TakeDamage(int damage)
    {
        if (m_isDead) return;

        // 扣除血量
        m_currentHealth = Mathf.Max(0, m_currentHealth - damage);
        Debug.Log($"[剑圣] 受到伤害：{damage}，剩余血量：{m_currentHealth}/{m_maxHealth}");

        // 复用原受击动画触发逻辑（空值保护）
        m_animator?.SetTrigger("Hurt");

        // 血量为0触发死亡
        if (m_currentHealth <= 0)
        {
            Die();
        }
        // 剑圣受击加速（特色：受击后短时间提速）
        else
        {
            StartCoroutine(HurtSpeedUp(0.8f));
        }
    }

    // 剑圣受击加速：0.8秒内移速翻倍
    private IEnumerator HurtSpeedUp(float speedUpTime)
    {
        float originalSpeed = m_speed;
        m_speed *= 1.8f; // 提速80%，而非翻倍，更平衡
        yield return new WaitForSeconds(speedUpTime);
        m_speed = originalSpeed;
    }

    public void Die()
    {
        if (m_isDead) return;

        m_isDead = true;
        Debug.Log("[剑圣] 已死亡");

        // 复用原死亡动画触发逻辑（空值保护）
        if (m_animator != null)
        {
            m_animator.SetBool("noBlood", m_noBlood);
            m_animator.SetTrigger("Death");
        }

        // 停止移动（空值保护）
        if (m_body2d != null)
        {
            m_body2d.velocity = new Vector2(0, m_body2d.velocity.y);
            m_body2d.bodyType = RigidbodyType2D.Static;
        }

        // 禁用碰撞体（空值保护）
        if (m_collider2d != null)
        {
            m_collider2d.enabled = false;
        }
    }

    // 暴露是否死亡的属性
    public bool IsDead => m_isDead;
    #endregion

    #region Gizmos可视化（调试用）
    private void OnDrawGizmosSelected()
    {
        // 检测范围（红色）
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, m_detectRange);

        // 攻击范围（绿色）
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, m_attackRange);

        // 巡逻半径（黄色）
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, m_patrolRadius);

        // 巡逻点（蓝色）
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(m_patrolPointA, 0.2f);
        Gizmos.DrawSphere(m_patrolPointB, 0.2f);
        Gizmos.DrawLine(m_patrolPointA, m_patrolPointB);
    }
    #endregion
}