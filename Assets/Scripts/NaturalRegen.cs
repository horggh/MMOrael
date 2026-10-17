using UnityEngine;

/// Regeneração natural do Ragnarok: HP a cada 6 s e SP a cada 8 s, nos valores de
/// CharacterStats.HpRegen / SpRegen (dependem de VIT/INT e do máximo).
public class NaturalRegen : MonoBehaviour
{
    private const float HpEvery = 6f, SpEvery = 8f;

    private Health _health;
    private SkillUser _skills;
    private CharacterStats _stats;
    private float _hpTimer, _spTimer;

    private void Awake()
    {
        _health = GetComponent<Health>();
        _skills = GetComponent<SkillUser>();
        _stats = GetComponent<CharacterStats>();
    }

    private void Update()
    {
        if (_stats == null || (_health != null && _health.IsDead)) return;
        _hpTimer += Time.deltaTime;
        _spTimer += Time.deltaTime;
        if (_hpTimer >= HpEvery)
        {
            _hpTimer -= HpEvery;
            if (_health != null && _health.Hp < _health.maxHp) _health.Heal(_stats.HpRegen);
        }
        if (_spTimer >= SpEvery)
        {
            _spTimer -= SpEvery;
            if (_skills != null) _skills.AddSp(_stats.SpRegen);
        }
    }
}
