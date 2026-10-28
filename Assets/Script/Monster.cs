using UnityEngine;
using System.Collections;

public class Monster : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] float m_speed = 2.0f;
    [SerializeField] float m_jumpForce = 5.0f;
    [SerializeField] float m_chaseRange = 8.0f;
    [SerializeField] float m_attackRange = 2.0f;
    [SerializeField] bool m_isFlying = false;

    [Header("Attack Settings")]
    [SerializeField] bool m_noBlood = false;
    [SerializeField] GameObject m_projectile;
    [SerializeField] Vector3 m_projectionSpawnOffset;
    [SerializeField] float m_attackCooldown = 2.0f;

    private Animator m_animator;
    private Rigidbody2D m_body2d;
    private Sensor_PixelHeroes m_groundSensor;
    private Transform m_playerTarget;
    private bool m_grounded = false;
    private float m_delayToIdle = 0.0f;
    private int m_facingDirection = 1;
    private float m_attackTimer = 0f;
    private MonsterState m_currentState = MonsterState.Patrol;
    private Vector3 m_patrolStartPosition;
    [SerializeField] float m_patrolRange = 5f;

    public enum MonsterState
    {
        Idle,
        Patrol,
        Chase,
        Attack,
        Return
    }

    void Start()
    {
        m_animator = GetComponent<Animator>();
        m_body2d = GetComponent<Rigidbody2D>();
        m_groundSensor = transform.Find("GroundSensor").GetComponent<Sensor_PixelHeroes>();

        // 查找玩家
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            m_playerTarget = player.transform;

        m_patrolStartPosition = transform.position;
    }

    void Update()
    {
        // 检查地面状态
        UpdateGroundStatus();

        // 更新攻击冷却
        if (m_attackTimer > 0)
            m_attackTimer -= Time.deltaTime;

        // 状态机逻辑
        switch (m_currentState)
        {
            case MonsterState.Patrol:
                PatrolBehavior();
                break;
            case MonsterState.Chase:
                ChaseBehavior();
                break;
            case MonsterState.Attack:
                AttackBehavior();
                break;
            case MonsterState.Return:
                ReturnToPatrol();
                break;
        }

        // 更新动画
        UpdateAnimation();
    }

    void UpdateGroundStatus()
    {
        if (!m_isFlying)
        {
            if (!m_grounded && m_groundSensor.State())
            {
                m_grounded = true;
                m_animator.SetBool("Grounded", m_grounded);
            }
            if (m_grounded && !m_groundSensor.State())
            {
                m_grounded = false;
                m_animator.SetBool("Grounded", m_grounded);
            }
        }
    }

    void PatrolBehavior()
    {
        if (m_playerTarget != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, m_playerTarget.position);

            // 如果玩家在追踪范围内，切换到追踪状态
            if (distanceToPlayer <= m_chaseRange)
            {
                m_currentState = MonsterState.Chase;
                return;
            }
        }

        // 简单巡逻：来回移动
        float moveDirection = Mathf.Sign(Mathf.Sin(Time.time));
        Move(moveDirection);

        // 限制巡逻范围
        if (Vector3.Distance(transform.position, m_patrolStartPosition) > m_patrolRange)
        {
            m_currentState = MonsterState.Return;
        }
    }

    void ChaseBehavior()
    {
        if (m_playerTarget == null)
        {
            m_currentState = MonsterState.Return;
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, m_playerTarget.position);

        // 如果玩家超出追踪范围，返回巡逻
        if (distanceToPlayer > m_chaseRange * 1.5f)
        {
            m_currentState = MonsterState.Return;
            return;
        }

        // 如果在攻击范围内，攻击
        if (distanceToPlayer <= m_attackRange)
        {
            m_currentState = MonsterState.Attack;
            m_body2d.velocity = new Vector2(0, m_body2d.velocity.y);
            return;
        }

        // 追踪玩家
        float direction = Mathf.Sign(m_playerTarget.position.x - transform.position.x);
        Move(direction);
    }

    void AttackBehavior()
    {
        if (m_playerTarget == null)
        {
            m_currentState = MonsterState.Return;
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, m_playerTarget.position);

        // 如果玩家离开攻击范围，继续追踪
        if (distanceToPlayer > m_attackRange)
        {
            m_currentState = MonsterState.Chase;
            return;
        }

        // 面向玩家
        float direction = Mathf.Sign(m_playerTarget.position.x - transform.position.x);
        UpdateFacingDirection(direction);

        // 执行攻击
        if (m_attackTimer <= 0)
        {
            PerformAttack();
            m_attackTimer = m_attackCooldown;
        }
    }

    void ReturnToPatrol()
    {
        float distanceToStart = Vector3.Distance(transform.position, m_patrolStartPosition);

        if (distanceToStart < 0.5f)
        {
            m_currentState = MonsterState.Patrol;
            m_body2d.velocity = Vector2.zero;
        }
        else
        {
            float direction = Mathf.Sign(m_patrolStartPosition.x - transform.position.x);
            Move(direction);
        }
    }

    void Move(float direction)
    {
        if (!m_grounded && !m_isFlying) return;

        // 更新朝向
        UpdateFacingDirection(direction);

        // 移动
        m_body2d.velocity = new Vector2(direction * m_speed, m_body2d.velocity.y);

        // 跳跃障碍（简单实现）
        if (m_grounded && ShouldJump())
        {
            Jump();
        }
    }

    void UpdateFacingDirection(float direction)
    {
        if (direction > 0 && m_facingDirection != 1)
        {
            GetComponent<SpriteRenderer>().flipX = false;
            m_facingDirection = 1;
        }
        else if (direction < 0 && m_facingDirection != -1)
        {
            GetComponent<SpriteRenderer>().flipX = true;
            m_facingDirection = -1;
        }
    }

    bool ShouldJump()
    {
        // 简单检测前方是否有障碍物
        float rayLength = 1f;
        Vector2 rayDirection = m_facingDirection > 0 ? Vector2.right : Vector2.left;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, rayDirection, rayLength);

        return hit.collider != null && hit.collider.CompareTag("Ground");
    }

    void Jump()
    {
        if (m_grounded)
        {
            m_animator.SetTrigger("Jump");
            m_grounded = false;
            m_animator.SetBool("Grounded", m_grounded);
            m_body2d.velocity = new Vector2(m_body2d.velocity.x, m_jumpForce);
            m_groundSensor.Disable(0.2f);
        }
    }

    void PerformAttack()
    {
        // 随机选择攻击方式
        int attackType = Random.Range(0, 4);

        switch (attackType)
        {
            case 0:
                m_animator.SetTrigger("Attack");
                break;
            case 1:
                m_animator.SetTrigger("SpecialAttack");
                break;
            case 2:
                m_animator.SetTrigger("Throw");
                break;
            case 3:
                m_animator.SetTrigger("Spellcast");
                break;
        }
    }

    void UpdateAnimation()
    {
        // 设置空中速度
        if (!m_isFlying)
            m_animator.SetFloat("AirSpeedY", m_body2d.velocity.y);

        // 跑步/空闲状态
        if (Mathf.Abs(m_body2d.velocity.x) > 0.1f && m_currentState != MonsterState.Attack)
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

    // 动画事件：在投掷动画中调用
    public void SpawnProjectile()
    {
        if (m_projectile != null)
        {
            Vector3 facingVector = new Vector3(m_facingDirection, 1, 1);
            Vector3 projectionSpawnPosition = transform.localPosition + Vector3.Scale(m_projectionSpawnOffset, facingVector);
            GameObject bolt = Instantiate(m_projectile, projectionSpawnPosition, gameObject.transform.localRotation) as GameObject;
            bolt.transform.localScale = facingVector;

            // 为抛射物添加朝向速度
            Rigidbody2D boltRb = bolt.GetComponent<Rigidbody2D>();
            if (boltRb != null)
            {
                boltRb.velocity = new Vector2(m_facingDirection * 10f, 0);
            }
        }
    }

    // 被攻击时调用
    public void TakeDamage()
    {
        m_animator.SetTrigger("Hurt");

        // 受伤后可能逃跑或反击
        if (Random.value < 0.3f) // 30%几率逃跑
        {
            m_currentState = MonsterState.Return;
        }
    }

    // 死亡
    public void Die()
    {
        m_animator.SetBool("noBlood", m_noBlood);
        m_animator.SetTrigger("Death");

        // 禁用碰撞和AI
        GetComponent<Collider2D>().enabled = false;
        enabled = false;

        // 一段时间后销毁
        Destroy(gameObject, 2f);
    }

    void OnDrawGizmosSelected()
    {
        // 可视化范围
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, m_chaseRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, m_attackRange);
    }
}