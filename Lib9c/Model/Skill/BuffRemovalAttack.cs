using System;
using System.Collections.Generic;
using Nekoyume.Model.Stat;
using Nekoyume.TableData;

namespace Nekoyume.Model.Skill
{
    [Serializable]
    public class BuffRemovalAttack : AttackSkill
    {
        public BuffRemovalAttack(
            SkillSheet.Row skillRow,
            long power,
            int chance,
            int statPowerRatio,
            StatType referencedStatType) : base(skillRow, power, chance, statPowerRatio, referencedStatType)
        {
        }

        public override BattleStatus.Skill Use(CharacterBase caster,
            int simulatorWaveTurn,
            IEnumerable<Buff.Buff> buffs, bool copyCharacter)
        {
            var clone = copyCharacter ? (CharacterBase) caster.Clone() : null;
            var damage = ProcessDamage(caster, simulatorWaveTurn, copyCharacter: copyCharacter);
            var buff = ProcessBuff(caster, simulatorWaveTurn, buffs, copyCharacter);
            var targets = SkillRow.SkillTargetType.GetTarget(caster);
            foreach (var target in targets)
            {
                target.RemoveRecentStatBuff();
            }

            return new Model.BattleStatus.BuffRemovalAttack(SkillRow.Id, clone, damage, buff);
        }
    }
}
