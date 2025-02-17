using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerDamageable : MonoBehaviour, IDamageable
{
    int _health = 8;
    public int Health
    {
        get => _health;
        private set => _health = value;
    }

    public int MaxHealth => _maxHealth;
    int _maxHealth = -1;

    void OnEnable()
    {
        _maxHealth = _health;
    }

    public void Damage(int damage)
    {
        AudioController.instance.PlayAudio("Player Damage");
        Health = Mathf.Clamp(Health - damage, 0, _maxHealth);
        if (Health <= 0)
        {
            OnDeath();
        }
    }

    public void Heal(int heal) { }

    void OnDeath()
    {
        // Reset all the player's stats
        Health = _maxHealth;

        // Reset the player's position
        transform.position = new Vector3(-2f, 0f, 0f);
    }
}
