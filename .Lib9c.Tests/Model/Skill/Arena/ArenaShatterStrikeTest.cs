namespace Lib9c.Tests.Model.Skill.Arena
{
    using System.Collections.Generic;
    using System.Linq;
    using Lib9c.Tests.Action;
    using Nekoyume.Arena;
    using Nekoyume.Model;
    using Nekoyume.Model.Buff;
    using Nekoyume.Model.Skill.Arena;
    using Nekoyume.Model.Stat;
    using Nekoyume.Model.State;
    using Xunit;

    public class ArenaShatterStrikeTest
    {
        private readonly TableSheets _tableSheets;
        private readonly AvatarState _avatar1;
        private readonly AvatarState _avatar2;

        private readonly ArenaAvatarState _arenaAvatar1;
        private readonly ArenaAvatarState _arenaAvatar2;

        public ArenaShatterStrikeTest()
        {
            _tableSheets = new TableSheets(TableSheetsImporter.ImportSheets());
            _avatar1 = new AvatarState(
                default,
                default,
                0,
                _tableSheets.GetAvatarSheets(),
                new GameConfigState(),
                default
            );
            _avatar2 = new AvatarState(
                default,
                default,
                0,
                _tableSheets.GetAvatarSheets(),
                new GameConfigState(),
                default
            );

            _arenaAvatar1 = new ArenaAvatarState(_avatar1);
            _arenaAvatar2 = new ArenaAvatarState(_avatar2);
        }

        [Fact]
        public void Use()
        {
            var simulator = new ArenaSimulator(new TestRandom());
            var myDigest = new ArenaPlayerDigest(_avatar1, _arenaAvatar1);
            var enemyDigest = new ArenaPlayerDigest(_avatar2, _arenaAvatar2);
            var arenaSheets = _tableSheets.GetArenaSimulatorSheets();
            var challenger =
                new ArenaCharacter(
                    simulator,
                    myDigest,
                    arenaSheets,
                    simulator.HpModifier,
                    new List<StatModifier>()
                );
            var enemy =
                new ArenaCharacter(
                    simulator,
                    enemyDigest,
                    arenaSheets,
                    simulator.HpModifier,
                    new List<StatModifier>()
                );

            var skillRow = _tableSheets.SkillSheet.OrderedList.First(s => s.Id == 700010);
            var shatterStrike = new ArenaShatterStrike(skillRow, 0, 0, 10000, StatType.NONE);
            var used = shatterStrike.Use(challenger, enemy, simulator.Turn, new List<Buff>());
            Assert.Single(used.SkillInfos);
            Assert.Equal(enemy.HP - enemy.DEF, used.SkillInfos.First().Effect);
        }
    }
}
