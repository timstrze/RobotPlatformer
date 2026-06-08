using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Hit points for the player robot. Default max is 3 (one per UI heart).
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] int maxHealth = 3;

    int _current;

    public int CurrentHealth => _current;
    public int MaxHealth => maxHealth;

    /// <summary>Fired after health changes (current, max).</summary>
    public event System.Action<int, int> OnHealthChanged;

    /// <summary>Fired once when health reaches zero (after the last point of damage).</summary>
    public event System.Action OnDepleted;

    [SerializeField] UnityEvent onHealthDepleted;

    void Awake()
    {
        _current = maxHealth;
    }

    void Start()
    {
        OnHealthChanged?.Invoke(_current, maxHealth);
    }

    public void TakeDamage(int amount = 1)
    {
        if (amount <= 0 || _current <= 0)
            return;

        _current = Mathf.Max(0, _current - amount);
        OnHealthChanged?.Invoke(_current, maxHealth);

        if (_current == 0)
        {
            onHealthDepleted?.Invoke();
            OnDepleted?.Invoke();
        }
    }

    public void HealToFull()
    {
        if (_current == maxHealth)
            return;
        _current = maxHealth;
        OnHealthChanged?.Invoke(_current, maxHealth);
    }
}
