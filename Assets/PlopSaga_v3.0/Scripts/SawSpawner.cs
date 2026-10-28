using System.Collections;
using UnityEngine;

public enum SpawnerType
{
    left, right, up, down
}

public class SawSpawner : MonoBehaviour
{
    public SpawnerType spawnerType;
    public float time;
    public float speed;
    public int damageAmount = 20;    // 新增：可配置的伤害值

    void Start()
    {
        Spawn();
        StartCoroutine(TimeBetweenSpawn());
    }

    IEnumerator TimeBetweenSpawn()
    {
        yield return new WaitForSeconds(time);
        Spawn();
        StartCoroutine(TimeBetweenSpawn());
    }

    void Spawn()
    {
        GameObject saw = null;

        switch (spawnerType)
        {
            case SpawnerType.left:
                saw = ObjectPooling.instance.GetSpawnedSaw();
                ConfigureSaw(saw, MovementType.onlyLeft, transform.position.x);
                break;

            case SpawnerType.right:
                saw = ObjectPooling.instance.GetSpawnedSaw();
                ConfigureSaw(saw, MovementType.onlyRight, transform.position.x);
                break;

            case SpawnerType.down:
                saw = ObjectPooling.instance.GetSpawnedSaw();
                ConfigureSaw(saw, MovementType.onlyDown, transform.position.y);
                break;

            case SpawnerType.up:
                saw = ObjectPooling.instance.GetSpawnedSaw();
                ConfigureSaw(saw, MovementType.onlyUp, transform.position.y);
                break;
        }

        if (saw != null)
        {
            saw.transform.position = transform.position;
            saw.SetActive(true);
        }
    }

    // 配置锯片的统一方法（避免代码重复）
    private void ConfigureSaw(GameObject saw, MovementType movementType, float currentValue)
    {
        if (saw == null) return;

        Movement movement = saw.GetComponent<Movement>();
        if (movement == null) return;

        // 设置移动属性
        movement.currentValue = currentValue;
        movement.movement = movementType;
        movement.speed = speed;

        // 确保锯片有伤害组件
        SawDamage sawDamage = saw.GetComponent<SawDamage>();
        if (sawDamage == null)
        {
            sawDamage = saw.AddComponent<SawDamage>();
        }

        // 设置伤害值
        sawDamage.SetDamage(damageAmount);
    }
}
