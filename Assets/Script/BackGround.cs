using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class BackGround : MonoBehaviour
{
    SpriteRenderer spriteRenderer;

    public float n;

    public bool isChange;
    public bool isBack;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        n = 0.05f;

        isChange = false;

        isBack = false;

    }

    void Update()
    {
        if (isChange)
        {
            ChangeBlack();
        }
        else if (isBack)
        {
            InitialColor();
        }
    }
    public void ChangeBlack() 
    {
        if (spriteRenderer.color != new Color(0, 0, 0, 1)) 
        {
            n += 0.01f;
            spriteRenderer.color = new Color(0, 0, 0, n);
        }
        else if (spriteRenderer.color == new Color(0, 0, 0, 1)) 
        {
            isChange = false;
            n = 0.05f;
        }
    }
    public void InitialColor() 
    {
        if (spriteRenderer.color != new Color(0, 0, 0, 0))
        {
            n += 0.01f;
            spriteRenderer.color = new Color(0, 0, 0, 1 - n);
        }
        else if (spriteRenderer.color == new Color(0, 0, 0, 0))   
        {
            n = 0.05f;
            isBack = false;
        }
    }
}
