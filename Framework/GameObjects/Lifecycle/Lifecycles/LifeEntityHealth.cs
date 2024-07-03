using MyFramework.GameObjects.Attribute;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


namespace MyFramework.GameObjects.Lifecycle
{
    // 伤害结构体：信息传递
    public struct Damage
    {
        public DamageType type;
        public ValueEffectMode method;
        public float value;
    }
    // 治疗结构体
    public struct Heal
    {
        public ValueEffectMode method;
        public float value;
    }

    // 初始化信息
    public struct EntityAttrInfo
    {
        public float rawPhysicDefence;
        public float rawMagicDefence;
        public float rawHealth;

        public float rawAttack;
        
    }

    public sealed class LifeEntityHealth : LifecycleBase<LifeEntityHealth>, IHealthLifecycle
    {
        #region Interface
        new public static LifeEntityHealth Attach(GameObject gameObject, float rawHealth)
        {
            LifeEntityHealth lifecycle = gameObject.AddComponent<LifeEntityHealth>();
            lifecycle.Health.SetRawValue(rawHealth);
            lifecycle.Health.Reset();
            return lifecycle;
        }

        public override void ResetLifecycle()
        {
            Health.Reset();
        }
        #endregion

        #region Attirbute
        // 生命值属性
        private Health _health;

        // 防御力属性
        private Dictionary<DamageType, Defence> _defenceMap; // 多种类型的防御力
        // 护盾值属性
        private Shield _shield;

        
        public Defence PhysicDefence
        {
            get => _defenceMap[DamageType.Physic] ??= new(ValueEffectMode.Absolute, 0f, 0f);
            private set => _defenceMap[DamageType.Physic] = value;
        }
        public Defence MagicDefence
        {
            get => _defenceMap[DamageType.Magic] ??= new(ValueEffectMode.Percentage, 0f, 0f);
            private set => _defenceMap[DamageType.Magic] = value;
        }
        // TODO：将伤害减免改为一个ISimpleValue的独立类型
        public Defence DamageReduction
        {
            get => _defenceMap[DamageType.Real] ??= new(ValueEffectMode.Percentage, 0f, 0f);
            private set => _defenceMap[DamageType.Real] = value;
        }
        public Shield Shield
        {
            get => _shield ??= new(0f);
            private set => _shield = value;
        }
        public Health Health
        {
            get => _health ??= new(1f, 0f, 0f);
            private set => _health = value;
        }

        // 存活
        public bool isAlive { get => _health.valid; }
        // 拥有护盾
        public bool hasShield { get => (_shield != null) && _shield.valid; }

        // 允许被治疗
        private bool _allowHeal = false;
        public bool allowHeal { get => _allowHeal; private set => _allowHeal = value; }

        public void AllowHeal(bool value = true)
        {
            allowHeal = value;
        }
        #endregion

        #region Init
        private void Awake()
        {
            _defenceMap = new();
        }
        #endregion


        #region Event.Health
        // 受伤回调事件
        private UnityEvent<GameObject> takeDamageEvent;
        // 生命恢复回调事件
        private UnityEvent<GameObject> takeHealEvent;

        public void RegisterTakeDamageEvent(UnityAction<GameObject> action)
        {
            takeDamageEvent ??= new UnityEvent<GameObject>();
            takeDamageEvent.AddListener(action);
        }
        public void RemoveTakeDamageEvent(UnityAction<GameObject> action)
        {
            takeDamageEvent?.RemoveListener(action);
        }

        public void RegisterTakeHealEvent(UnityAction<GameObject> action)
        {
            takeHealEvent ??= new UnityEvent<GameObject>();
            takeHealEvent.AddListener(action);
        }
        public void RemoveTakeHealEvent(UnityAction<GameObject> action)
        {
            takeHealEvent?.RemoveListener(action);
        }
        #endregion

        #region Event.Shield
        // 护盾抵挡事件
        private UnityEvent<GameObject> shieldResistEvent;
        // 护盾破碎事件
        private UnityEvent<GameObject> shieldBreakEvent;

        public void RegisterShieldResistEvent(UnityAction<GameObject> action)
        {
            shieldResistEvent ??= new UnityEvent<GameObject>();
            shieldResistEvent.AddListener(action);
        }
        public void RemoveShieldResistEvent(UnityAction<GameObject> action)
        {
            shieldResistEvent?.RemoveListener(action);
        }

        public void RegisterShieldBreakEvent(UnityAction<GameObject> action)
        {
            shieldBreakEvent ??= new UnityEvent<GameObject>();
            shieldBreakEvent.AddListener(action);
        }
        public void RemoveShieldBreakEvent(UnityAction<GameObject> action)
        {
            shieldBreakEvent?.RemoveListener(action);
        }
        #endregion


        #region Lifecycle
        // 说明：bool返回值指示是否成功造成伤害/治疗，用于主动技能的逻辑回调，无需关注其他情况

        // 受到伤害
        public bool TakeDamage(Damage damage)
        {
            // 按百分比扣除血量
            if (damage.method == ValueEffectMode.Percentage)
            {
                Health.CostCurrValueByPer(damage.value);
            }
            // 按绝对值扣除血量（默认）
            else
            {
                // 真实伤害：无视防御值和护盾值
                // 其他类型伤害：先由防御值计算得出，再尝试扣除相应护盾值，最后扣除生命值

                // 1) 防御力
                if (damage.type == DamageType.Physic)
                {
                    damage.value = PhysicDefence.GetDamageValueAfterDefenceDec(damage.value);
                }
                else if (damage.type == DamageType.Magic)
                {
                    damage.value = MagicDefence.GetDamageValueAfterDefenceDec(damage.value);
                }
                // 经过减伤
                damage.value = DamageReduction.GetDamageValueAfterDefenceDec(damage.value);

                // 2) 护盾值
                if(hasShield)
                {
                    damage.value = Shield.GetDamageValueAfterShieldResist(damage.value);
                    // 伤害被全部抵挡
                    if(hasShield)
                    {
                        shieldResistEvent?.Invoke(gameObject);
                        return true; // 返回，不会继续造成伤害
                    }
                    // 伤害被部分抵挡
                    else
                    {
                        shieldBreakEvent?.Invoke(gameObject);
                    }
                }

                // 3) 生命值
                Health.CostCurrValue(damage.value);
                if(isAlive)
                {
                    takeDamageEvent?.Invoke(gameObject);
                }
                else
                {
                    base.Death();
                    return true;
                }
            }
            return true; // 成功受到伤害
        }
        // 受到治疗
        public bool TakeHeal(Heal heal)
        {
            if( !allowHeal )
            {
                return false;
            }
            
            // 按百分比恢复血量
            if (heal.method == ValueEffectMode.Percentage)
            {
                Health.AddCurrValueByPer(heal.value);
            }
            // 按绝对值恢复血量
            else
            {
                Health.AddCurrValue(heal.value);
            }
            takeHealEvent?.Invoke(gameObject);
            return true;
        }
        #endregion

    }

}


