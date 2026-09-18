using UnityEngine;

public class EnemyHitbox : MonoBehaviour
{
    public Enemy enemy;
    public bool isHead;

    public void NotifyHit(RaycastHit hit)
    {
        if (enemy == null)
            return;

        float amount = isHead ? enemy.headDamage : enemy.bodyDamage;
        enemy.TakeDamage(amount, isHead, hit);
    }
}
