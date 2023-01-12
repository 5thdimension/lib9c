using System.Linq;
using Bencodex.Types;
using Lib9c.Model.Item;
using Lib9c.Model.Skill;
using Xunit;

namespace Lib9c.Tests.Model
{
    public class ItemUsableTest
    {
        private readonly TableSheets _tableSheets;

        public ItemUsableTest()
        {
            _tableSheets = new TableSheets(TableSheetsImporter.ImportSheets());
        }

        [Fact]
        public void Serialize()
        {
            var row = _tableSheets.EquipmentItemSheet.Values.First();
            var itemUsable = (Equipment)ItemFactory.CreateItemUsable(row, default, 0);
            var skillIds = new[] { 100001, 100003 };
            var buffIds = new[] { 200000, 210000 };
            for (var index = 0; index < skillIds.Length; index++)
            {
                var skillId = skillIds[index];
                var skillRow = _tableSheets.SkillSheet[skillId];
                var skill = SkillFactory.Get(skillRow, 1, index + 1);
                itemUsable.Skills.Add(skill);

                var buffId = buffIds[index];
                var buffRow = _tableSheets.SkillSheet[buffId];
                var buff = (BuffSkill)SkillFactory.Get(buffRow, 1, index + 1);
                itemUsable.BuffSkills.Add(buff);
            }

            var serialized = itemUsable.Serialize();
            var deserialized = new Equipment((Dictionary)serialized);

            Assert.Equal(serialized, deserialized.Serialize());
        }
    }
}
