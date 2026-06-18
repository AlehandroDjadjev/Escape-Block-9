using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour
{
    public static event Action<PlayerHealth> AnyPlayerDied;

    public event Action<int, int> HealthChanged;
    public event Action<PlayerHealth> Died;

    [SerializeField] private int maxHealth = 100;
    [SerializeField] private bool resetOnEnable = true;

    private int currentHealth;
    private bool isDead;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        currentHealth = Mathf.Max(1, maxHealth);
    }

    private void OnEnable()
    {
        if (resetOnEnable)
        {
            ResetHealth();
        }
    }

    public void ResetHealth()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = maxHealth;
        isDead = false;
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public bool TakeDamage(int amount, string source = null, GameObject instigator = null)
    {
        if (isDead || amount <= 0)
        {
            return false;
        }

        currentHealth = Mathf.Max(0, currentHealth - amount);
        string sourceLabel = string.IsNullOrWhiteSpace(source) ? "damage" : source;
        Debug.Log($"[PlayerHealth] Took {amount} damage from {sourceLabel}. HP {currentHealth}/{maxHealth}");
        HealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            isDead = true;
            Debug.Log($"[PlayerHealth] Player died from {sourceLabel}.");
            Died?.Invoke(this);
            AnyPlayerDied?.Invoke(this);
        }

        return true;
    }

    public static PlayerHealth GetOrCreate(Component target)
    {
        if (target == null)
        {
            return null;
        }

        PlayerHealth health = target.GetComponentInParent<PlayerHealth>();
        if (health != null)
        {
            return health;
        }

        FirstPersonController controller = target.GetComponentInParent<FirstPersonController>();
        if (controller != null)
        {
            return controller.gameObject.AddComponent<PlayerHealth>();
        }

        if (target.CompareTag("Player"))
        {
            return target.gameObject.AddComponent<PlayerHealth>();
        }

        return null;
    }

    public static bool TryDamage(Component target, int amount, string source, GameObject instigator = null)
    {
        PlayerHealth health = GetOrCreate(target);
        return health != null && health.TakeDamage(amount, source, instigator);
    }
}
