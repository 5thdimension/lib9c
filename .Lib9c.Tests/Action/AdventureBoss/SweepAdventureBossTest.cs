namespace Lib9c.Tests.Action.AdventureBoss
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using Libplanet.Action.State;
    using Libplanet.Crypto;
    using Libplanet.Mocks;
    using Libplanet.Types.Assets;
    using Nekoyume;
    using Nekoyume.Action;
    using Nekoyume.Action.AdventureBoss;
    using Nekoyume.Extensions;
    using Nekoyume.Model.AdventureBoss;
    using Nekoyume.Model.Item;
    using Nekoyume.Model.State;
    using Nekoyume.Module;
    using Nekoyume.TableData;
    using Xunit;

    public class SweepAdventureBossTest
    {
        private static readonly Dictionary<string, string> Sheets =
            TableSheetsImporter.ImportSheets();

        private static readonly TableSheets TableSheets = new TableSheets(Sheets);
#pragma warning disable CS0618
        // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1419
        private static readonly Currency NCG = Currency.Legacy("NCG", 2, null);
#pragma warning restore CS0618

        // Wanted
        private static readonly Address WantedAddress = new PrivateKey().Address;

        private static readonly Address WantedAvatarAddress =
            Addresses.GetAvatarAddress(WantedAddress, 0);

        private static readonly AvatarState WantedAvatarState = new (
            WantedAvatarAddress, WantedAddress, 0L, TableSheets.GetAvatarSheets(),
            new PrivateKey().Address, name: "wanted"
        );

        private static readonly AgentState WantedState = new (WantedAddress)
        {
            avatarAddresses = { [0] = WantedAvatarAddress, },
        };

        // Test Account
        private static readonly Address TesterAddress =
            new ("2000000000000000000000000000000000000002");

        private static readonly Address TesterAvatarAddress =
            Addresses.GetAvatarAddress(TesterAddress, 0);

        private static readonly AvatarState TesterAvatarState = new (
            TesterAvatarAddress, TesterAddress, 0L, TableSheets.GetAvatarSheets(),
            new PrivateKey().Address, name: "Tester"
        );

        private static readonly AgentState TesterState = new (TesterAddress)
        {
            avatarAddresses =
            {
                [0] = TesterAvatarAddress,
            },
        };

        private readonly IWorld _initialState = new World(MockUtil.MockModernWorldState)
            .SetLegacyState(Addresses.GoldCurrency, new GoldCurrencyState(NCG).Serialize())
            .SetAvatarState(WantedAvatarAddress, WantedAvatarState)
            .SetAgentState(WantedAddress, WantedState)
            .SetAvatarState(TesterAvatarAddress, TesterAvatarState)
            .SetAgentState(TesterAddress, TesterState)
            .MintAsset(new ActionContext(), WantedAddress, 1_000_000 * NCG);

        public static IEnumerable<object[]> GetExecuteMemberData()
        {
            yield return new object[]
            {
                0, 100, 100, typeof(InvalidOperationException), null,
            };
            yield return new object[]
            {
                1, 100, 98, null, new[] { (600301, 10) },
            };
            yield return new object[]
            {
                1, 0, 0, typeof(NotEnoughMaterialException), null,
            };
            yield return new object[]
            {
                10, 10, 10, typeof(NotEnoughMaterialException), null,
            };
            yield return new object[]
            {
                20, 40, 0, null,
                new[] { (600301, 900), (600302, 800), (600303, 50), (600304, 350), },
            };
        }

        [Theory]
        [MemberData(nameof(GetExecuteMemberData))]
        public void Execute(int floor, int initialPotion, int expectedPotion, Type exc, (int, int)[] expectedRewards)
        {
            // Settings
            var state = _initialState;
            foreach (var (key, value) in Sheets)
            {
                state = state.SetLegacyState(Addresses.TableSheet.Derive(key), value.Serialize());
            }

            state = Stake(state, WantedAddress);
            var sheets = state.GetSheets(sheetTypes: new[]
            {
                typeof(MaterialItemSheet),
            });
            var materialSheet = sheets.GetSheet<MaterialItemSheet>();
            var materialRow =
                materialSheet.OrderedList.First(row => row.ItemSubType == ItemSubType.ApStone);
            var apPotion = ItemFactory.CreateMaterial(materialRow);

            var inventory = state.GetInventory(TesterAvatarAddress);
            inventory.AddItem(apPotion, initialPotion);
            state = state.SetInventory(TesterAvatarAddress, inventory);

            // Open season
            state = new Wanted
            {
                Season = 1,
                AvatarAddress = WantedAvatarAddress,
                Bounty = Wanted.MinBounty * NCG,
            }.Execute(new ActionContext
            {
                PreviousState = state,
                Signer = WantedAddress,
                BlockIndex = 0L,
            });
            var exp = new Explorer(TesterAvatarAddress)
            {
                MaxFloor = 5 * Math.Max(floor / 5, 1),
                Floor = floor,
            };
            state = state.SetExplorer(1, exp);

            // Sweep and Test
            var action = new SweepAdventureBoss
            {
                Season = 1,
                AvatarAddress = TesterAvatarAddress,
            };

            if (exc is not null)
            {
                Assert.Throws(exc, () => action.Execute(new ActionContext
                    {
                        PreviousState = state,
                        Signer = TesterAddress,
                        BlockIndex = 1L,
                    }
                ));
            }
            else
            {
                state = action.Execute(new ActionContext
                {
                    PreviousState = state,
                    Signer = TesterAddress,
                    BlockIndex = 1L,
                });

                var potion = state.GetInventory(TesterAvatarAddress).Items
                    .FirstOrDefault(i => i.item.ItemSubType == ItemSubType.ApStone);
                if (expectedPotion == 0)
                {
                    Assert.Null(potion);
                }
                else
                {
                    Assert.Equal(expectedPotion, potion!.count);
                }

                var exploreBoard = state.GetExploreBoard(1);
                var explorer = state.GetExplorer(1, TesterAvatarAddress);
                Assert.True(explorer.Score > 0);
                Assert.True(exploreBoard.TotalPoint > 0);
                Assert.Equal(explorer.Score, exploreBoard.TotalPoint);
                Assert.Equal(floor, explorer.Floor);
                Assert.Equal(floor * SweepAdventureBoss.UnitApPotion, exploreBoard.UsedApPotion);
                Assert.Equal(explorer.UsedApPotion, exploreBoard.UsedApPotion);

                inventory = state.GetInventory(TesterAvatarAddress);
                foreach (var (id, amount) in expectedRewards)
                {
                    Assert.Equal(amount, inventory.Items.First(i => i.item.Id == id).count);
                }
            }
        }

        private IWorld Stake(IWorld world, Address agentAddress)
        {
            var action = new Stake(new BigInteger(500_000));
            var state = action.Execute(new ActionContext
            {
                PreviousState = world,
                Signer = agentAddress,
                BlockIndex = 0L,
            });
            return state;
        }
    }
}
