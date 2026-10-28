using UnityEngine;
using System.Collections;

public class Warlock : MonoBehaviour
{

    [SerializeField] float m_speed = 4.0f;
    [SerializeField] float m_jumpForce = 7.5f;
    [SerializeField] bool m_noBlood = false;

    private Animator m_animator;
    private Rigidbody2D m_body2d;
    private Sensor_PixelHeroes m_groundSensor;
    private bool m_grounded = false;
    private float m_delayToIdle = 0.0f;

    // Use this for initialization
    void Start()
    {
        // 1. 给组件获取加容错：找不到组件时打印提示，避免空引用
        m_animator = GetComponent<Animator>();
        if (m_animator == null)
        {
            Debug.LogError("[Warlock] 物体上未找到Animator组件！");
        }

        m_body2d = GetComponent<Rigidbody2D>();
        if (m_body2d == null)
        {
            Debug.LogError("[Warlock] 物体上未找到Rigidbody2D组件！");
        }

        // 2. GroundSensor容错：先找子物体，再获取组件
        Transform groundSensorTrans = transform.Find("GroundSensor");
        if (groundSensorTrans != null)
        {
            m_groundSensor = groundSensorTrans.GetComponent<Sensor_PixelHeroes>();
            if (m_groundSensor == null)
            {
                Debug.LogError("[Warlock] GroundSensor子物体上未找到Sensor_PixelHeroes组件！");
            }
        }
        else
        {
            Debug.LogError("[Warlock] 未找到名为GroundSensor的子物体！");
        }
    }

    // Update is called once per frame
    void Update()
    {
        // 3. 核心保护：访问m_groundSensor前先判断是否为空
        if (m_groundSensor != null)
        {
            //Check if character just landed on the ground
            if (!m_grounded && m_groundSensor.State())
            {
                m_grounded = true;
                m_animator?.SetBool("Grounded", m_grounded); // ?. 空传播运算符：m_animator为空时不执行
            }

            //Check if character just started falling
            if (m_grounded && !m_groundSensor.State())
            {
                m_grounded = false;
                m_animator?.SetBool("Grounded", m_grounded);
            }
        }
        else
        {
            // 没有GroundSensor时，简单用Y轴速度判断是否落地（备选方案）
            if (m_body2d != null)
            {
                if (!m_grounded && Mathf.Abs(m_body2d.velocity.y) < 0.1f)
                {
                    m_grounded = true;
                    m_animator?.SetBool("Grounded", m_grounded);
                }
                else if (m_grounded && Mathf.Abs(m_body2d.velocity.y) > 0.1f)
                {
                    m_grounded = false;
                    m_animator?.SetBool("Grounded", m_grounded);
                }
            }
        }

        // -- Handle input and movement --
        float inputX = Input.GetAxis("Horizontal");

        // Swap direction of sprite depending on walk direction
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) // 保护SpriteRenderer为空的情况
        {
            if (inputX > 0)
                spriteRenderer.flipX = false;
            else if (inputX < 0)
                spriteRenderer.flipX = true;
        }

        // Move：保护m_body2d为空的情况
        if (m_body2d != null)
        {
            m_body2d.velocity = new Vector2(inputX * m_speed, m_body2d.velocity.y);
            m_animator?.SetFloat("AirSpeedY", m_body2d.velocity.y);
        }

        // -- Handle Animations --
        //Death
        if (Input.GetKeyDown("e"))
        {
            m_animator?.SetBool("noBlood", m_noBlood);
            m_animator?.SetTrigger("Death");
        }

        //Hurt
        else if (Input.GetKeyDown("q"))
            m_animator?.SetTrigger("Hurt");

        //Attack
        else if (Input.GetMouseButtonDown(0))
            m_animator?.SetTrigger("Attack");

        //Spellcast
        else if (Input.GetMouseButtonDown(1))
            m_animator?.SetTrigger("Spellcast");

        //Jump：增加m_groundSensor和m_body2d的空保护
        else if (Input.GetKeyDown("space") && m_grounded && m_body2d != null)
        {
            m_animator?.SetTrigger("Jump");
            m_grounded = false;
            m_animator?.SetBool("Grounded", m_grounded);
            m_body2d.velocity = new Vector2(m_body2d.velocity.x, m_jumpForce);
            m_groundSensor?.Disable(0.2f); // ?. 空保护：m_groundSensor为空时不执行
        }

        //Run
        else if (Mathf.Abs(inputX) > Mathf.Epsilon)
        {
            // Reset timer
            m_delayToIdle = 0.05f;
            m_animator?.SetInteger("AnimState", 1);
        }

        //Idle
        else
        {
            // Prevents flickering transitions to idle
            m_delayToIdle -= Time.deltaTime;
            if (m_delayToIdle < 0)
                m_animator?.SetInteger("AnimState", 0);
        }
    }
}