using UnityEngine;

public class PlayerControl : MonoBehaviour
{
    [Header("移动属性")]
    [SerializeField] float m_speed = 4.0f;
    [SerializeField] float m_jumpForce = 7.5f;
    [SerializeField] float m_rollForce = 6.0f;

    [Header("战斗属性")]
    [SerializeField] int m_maxHP = 100;
    [SerializeField] int m_attack = 20; // 玩家攻击力
    [SerializeField] float m_attackSpeed = 1.0f;
    [SerializeField] int m_defense = 5;

    [Header("攻击设置")]
    [SerializeField] private Transform attackCheckPoint;
    [SerializeField] private float attackCheckRadius = 1f; // 攻击范围半径
    [SerializeField] private LayerMask enemyLayer;
    private bool m_isAttacking = false;

    [Header("特效/显示")]
    [SerializeField] bool m_noBlood = false;

    // 核心组件
    private int m_currentHP;
    private Animator m_animator;
    private Rigidbody2D m_body2d;
    private Sensor_HeroKnight m_groundSensor;

    // 状态变量
    private bool m_grounded = false;
    private bool m_rolling = false;
    private int m_facingDirection = 1;
    private int m_currentAttack = 0;
    private float m_timeSinceAttack = 0.0f;
    private float m_delayToIdle = 0.0f;
    private float m_rollDuration = 8.0f / 14.0f;
    private float m_rollCurrentTime;

    [Header("UI引用")]
    [SerializeField] private UnityEngine.UI.Slider healthSlider;
    [SerializeField] private TMPro.TextMeshProUGUI healthText;

    // 攻击事件（供Enemy监听）
    public static event System.Action<Vector2, int, float> OnPlayerAttack;
    // 血量变更事件
    public event System.Action<int, int> OnHealthChanged;

    void Start()
    {
        // 初始化核心组件（添加空值判断）
        m_animator = GetComponent<Animator>();
        m_body2d = GetComponent<Rigidbody2D>();

        // 修复GroundSensor空引用
        Transform groundSensorTrans = transform.Find("GroundSensor");
        if (groundSensorTrans != null)
        {
            m_groundSensor = groundSensorTrans.GetComponent<Sensor_HeroKnight>();
        }
        else
        {
            Debug.LogError("【PlayerControl】未找到GroundSensor子物体！请检查玩家对象的子物体");
            m_groundSensor = null; // 明确赋值，避免后续空引用
        }

        // 初始化血量
        m_currentHP = m_maxHP;
        UpdateHealthUI();

        // 自动创建攻击检测点（防止空引用）
        if (attackCheckPoint == null)
        {
            GameObject autoCheck = new GameObject("AttackCheckPoint");
            autoCheck.transform.parent = transform;
            autoCheck.transform.localPosition = new Vector3(0.5f * m_facingDirection, 0, 0);
            attackCheckPoint = autoCheck.transform;
            Debug.LogWarning("【PlayerControl】自动创建AttackCheckPoint，建议手动调整位置");
        }
    }

    void Update()
    {
        // 若核心组件缺失，直接返回（避免崩溃）
        if (m_body2d == null || m_animator == null) return;

        // 计时更新
        m_timeSinceAttack += Time.deltaTime;
        if (m_rolling)
            m_rollCurrentTime += Time.deltaTime;

        // 翻滚状态结束
        if (m_rollCurrentTime > m_rollDuration)
            m_rolling = false;

        // 地面检测（带空值防护）
        UpdateGroundCheck();

        // 移动输入与朝向
        UpdateMovementInput();

        // 核心操作逻辑
        HandlePlayerActions();

        // 动画状态更新
        UpdateAnimationState();
    }

    #region 核心辅助方法
    // 地面检测（添加空值防护）
    private void UpdateGroundCheck()
    {
        if (m_groundSensor == null) return; // 无传感器则跳过

        bool groundSensorState = m_groundSensor.State();
        if (!m_grounded && groundSensorState)
        {
            m_grounded = true;
            m_animator.SetBool("Grounded", m_grounded);
        }
        else if (m_grounded && !groundSensorState)
        {
            m_grounded = false;
            m_animator.SetBool("Grounded", m_grounded);
        }
    }

    // 移动输入与朝向控制
    private void UpdateMovementInput()
    {
        float inputX = Input.GetAxis("Horizontal");

        // 朝向翻转
        if (inputX > 0)
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.flipX = false;
            m_facingDirection = 1;
            attackCheckPoint.localPosition = new Vector3(Mathf.Abs(attackCheckPoint.localPosition.x), attackCheckPoint.localPosition.y, 0);
        }
        else if (inputX < 0)
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.flipX = true;
            m_facingDirection = -1;
            attackCheckPoint.localPosition = new Vector3(-Mathf.Abs(attackCheckPoint.localPosition.x), attackCheckPoint.localPosition.y, 0);
        }

        // 移动（非攻击/翻滚状态）
        if (!m_rolling && !m_isAttacking)
            m_body2d.velocity = new Vector2(inputX * m_speed, m_body2d.velocity.y);
    }

    // 玩家操作（攻击/跳跃/翻滚/死亡测试）
    private void HandlePlayerActions()
    {
        // 攻击（鼠标左键）
        if (Input.GetMouseButtonDown(0) && m_timeSinceAttack > (1.0f / m_attackSpeed) && !m_rolling && !m_isAttacking)
        {
            m_isAttacking = true;
            m_currentAttack = (m_currentAttack % 3) + 1;
            m_animator.SetTrigger("Attack" + m_currentAttack);
            m_timeSinceAttack = 0.0f;

            // 广播攻击事件
            Vector2 attackPos = attackCheckPoint.position;
            OnPlayerAttack?.Invoke(attackPos, m_attack, attackCheckRadius);

            // 攻击结束解锁
            Invoke(nameof(EndAttack), 0.5f);
        }
        // 翻滚（左Shift）
        else if (Input.GetKeyDown(KeyCode.LeftShift) && !m_rolling)
        {
            m_rolling = true;
            m_rollCurrentTime = 0.0f;
            m_animator.SetTrigger("Roll");
            m_body2d.velocity = new Vector2(m_facingDirection * m_rollForce, m_body2d.velocity.y);
        }
        // 跳跃（空格）
        else if (Input.GetKeyDown(KeyCode.Space) && m_grounded && !m_rolling)
        {
            m_animator.SetTrigger("Jump");
            m_grounded = false;
            m_animator.SetBool("Grounded", m_grounded);
            m_body2d.velocity = new Vector2(m_body2d.velocity.x, m_jumpForce);
            if (m_groundSensor != null) m_groundSensor.Disable(0.2f);
        }
        // 死亡测试（E键，可根据需求删除）
        else if (Input.GetKeyDown(KeyCode.E) && !m_rolling)
        {
            m_animator.SetBool("noBlood", m_noBlood);
            m_animator.SetTrigger("Death");
            m_currentHP = 0;
            UpdateHealthUI();
            OnHealthChanged?.Invoke(m_currentHP, m_maxHP); // 触发血量更新
        }
        // 受击测试（Q键，可根据需求删除）
        else if (Input.GetKeyDown(KeyCode.Q) && !m_rolling)
        {
            TakeDamage(10);
        }
    }

    // 动画状态更新
    private void UpdateAnimationState()
    {
        // 空中速度
        m_animator.SetFloat("AirSpeedY", m_body2d.velocity.y);

        // 移动/闲置动画
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

    #region 战斗核心逻辑
    // 攻击结束解锁
    private void EndAttack()
    {
        m_isAttacking = false;
    }

    // 受伤逻辑
    public void TakeDamage(int damage)
    {
        if (m_currentHP <= 0) return;

        // 计算实际伤害（防御减免，最低1点）
        int realDamage = Mathf.Max(1, damage - m_defense);
        m_currentHP -= realDamage;
        if (m_currentHP < 0) m_currentHP = 0;

        // 触发事件+更新UI
        OnHealthChanged?.Invoke(m_currentHP, m_maxHP);
        UpdateHealthUI();

        // 播放受击动画
        m_animator.SetTrigger("Hurt");

        // 血量为0触发死亡
        if (m_currentHP <= 0)
        {
            m_animator.SetBool("noBlood", m_noBlood);
            m_animator.SetTrigger("Death");
        }
    }

    // 治疗逻辑
    public void Heal(int healAmount)
    {
        if (m_currentHP <= 0) return;

        m_currentHP += healAmount;
        if (m_currentHP > m_maxHP) m_currentHP = m_maxHP;

        OnHealthChanged?.Invoke(m_currentHP, m_maxHP);
        UpdateHealthUI();
    }

    // 重置最大血量（恢复该方法，兼容CheckpointManager）
    public void SetMaxHealth(int newMaxHP, bool healToFull = false)
    {
        m_maxHP = newMaxHP;
        if (healToFull)
            m_currentHP = m_maxHP;
        else if (m_currentHP > m_maxHP)
            m_currentHP = m_maxHP;

        OnHealthChanged?.Invoke(m_currentHP, m_maxHP);
        UpdateHealthUI();
    }

    // 更新血量UI
    private void UpdateHealthUI()
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = m_maxHP;
            healthSlider.value = m_currentHP;
        }
        if (healthText != null)
        {
            healthText.text = $"{m_currentHP} / {m_maxHP}";
        }
    }
    #endregion

    #region 公共获取方法（核心属性）
    public int GetCurrentHP() => m_currentHP;
    public int GetMaxHP() => m_maxHP;
    public float GetHealthPercentage() => m_maxHP <= 0 ? 0f : (float)m_currentHP / m_maxHP;
    public bool IsAlive() => m_currentHP > 0;
    public int GetAttack() => m_attack;
    #endregion

    // 绘制攻击范围（调试用）
    private void OnDrawGizmosSelected()
    {
        if (attackCheckPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackCheckPoint.position, attackCheckRadius);
        }
    }
}