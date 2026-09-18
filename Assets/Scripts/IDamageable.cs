using UnityEngine;

public interface IDamageable
{
    void TakeDamage(float amount, bool isHeadshot, RaycastHit hit);
}
