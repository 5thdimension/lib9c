using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using Bencodex.Types;
using Lib9c.Abstractions;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Nekoyume.Action.Exceptions;
using Nekoyume.Extensions;
using Nekoyume.Helper;
using Nekoyume.Model.Item;
using Nekoyume.Model.Stat;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using Nekoyume.TableData.Summon;
using Serilog;
using static Lib9c.SerializeKeys;

namespace Nekoyume.Action
{
    [Serializable]
    [ActionType("aura_summon")]
    public class AuraSummon : GameAction, IAuraSummonV1
    {
        public const string AvatarAddressKey = "aa";
        public Address AvatarAddress;

        public const string GroupIdKey = "gid";
        public int GroupId;

        public const string SummonCountKey = "sc";
        public int SummonCount;

        private const int SummonLimit = 10;

        Address IAuraSummonV1.AvatarAddress => AvatarAddress;
        int IAuraSummonV1.GroupId => GroupId;
        int IAuraSummonV1.SummonCount => SummonCount;

        public AuraSummon()
        {
        }

        public AuraSummon(Address avatarAddress, int groupId, int summonCount)
        {
            AvatarAddress = avatarAddress;
            GroupId = groupId;
            SummonCount = summonCount;
        }

        protected override IImmutableDictionary<string, IValue> PlainValueInternal =>
            new Dictionary<string, IValue>
            {
                [AvatarAddressKey] = AvatarAddress.Serialize(),
                [GroupIdKey] = GroupId.Serialize(),
                [SummonCountKey] = SummonCount.Serialize(),
            }.ToImmutableDictionary();

        protected override void LoadPlainValueInternal(
            IImmutableDictionary<string, IValue> plainValue)
        {
            AvatarAddress = plainValue[AvatarAddressKey].ToAddress();
            GroupId = plainValue[GroupIdKey].ToInteger();
            SummonCount = plainValue[SummonCountKey].ToInteger();
        }

        public override IAccount Execute(IActionContext context)
        {
            context.UseGas(1);
            var states = context.PreviousState;
            var inventoryAddress = AvatarAddress.Derive(LegacyInventoryKey);
            var questListAddress = AvatarAddress.Derive(LegacyQuestListKey);

            if (context.Rehearsal)
            {
                return states;
            }

            var addressesHex = GetSignerAndOtherAddressesHex(context, AvatarAddress);
            var started = DateTimeOffset.UtcNow;
            Log.Debug($"{addressesHex} AuraSummon Exec. Started.");

            if (!states.TryGetAgentAvatarStatesV2(context.Signer, AvatarAddress, out var agentState,
                    out var avatarState, out _))
            {
                throw new FailedLoadStateException(
                    $"{addressesHex} Aborted as the avatar state of the signer was failed to load.");
            }

            if (SummonCount <= 0 || SummonCount > SummonLimit)
            {
                throw new InvalidSummonCountException(
                    $"{addressesHex} Given summonCount {SummonCount} is not valid. Please use between 1 and 10"
                );
            }

            // Validate Work
            Dictionary<Type, (Address, ISheet)> sheets = states.GetSheets(sheetTypes: new[]
            {
                typeof(SummonSheet),
                typeof(EquipmentItemRecipeSheet),
                typeof(EquipmentItemSheet),
                typeof(MaterialItemSheet),
                typeof(EquipmentItemSubRecipeSheetV2),
                typeof(EquipmentItemOptionSheet),
                typeof(SkillSheet),
            });

            var recipeSheet = sheets.GetSheet<EquipmentItemRecipeSheet>();
            var materialSheet = sheets.GetSheet<MaterialItemSheet>();
            var equipmentItemSheet = sheets.GetSheet<EquipmentItemSheet>();
            var equipmentItemSubRecipeSheetV2 = sheets.GetSheet<EquipmentItemSubRecipeSheetV2>();
            var optionSheet = sheets.GetSheet<EquipmentItemOptionSheet>();
            var skillSheet = sheets.GetSheet<SkillSheet>();

            var summonSheet = sheets.GetSheet<SummonSheet>();
            var summonRow = summonSheet.OrderedList.FirstOrDefault(row => row.GroupId == GroupId);
            if (summonRow is null)
            {
                throw new RowNotInTableException(
                    $"{addressesHex} Failed to get {GroupId} in AuraSummonSheet");
            }

            // Use materials
            var inventory = avatarState.inventory;
            var material = materialSheet.OrderedList.First(m => m.Id == summonRow.CostMaterial);
            if (!inventory.RemoveFungibleItem(material.ItemId, context.BlockIndex,
                    summonRow.CostMaterialCount * SummonCount))
            {
                throw new NotEnoughMaterialException(
                    $"{addressesHex} Aborted as the player has no enough material ({summonRow.CostMaterial} * {summonRow.CostMaterialCount})");
            }

            // Transfer Cost NCG first for fast-fail
            if (summonRow.CostNcg > 0L)
            {
                var arenaSheet = states.GetSheet<ArenaSheet>();
                var arenaData = arenaSheet.GetRoundByBlockIndex(context.BlockIndex);
                var feeStoreAddress =
                    Addresses.GetBlacksmithFeeAddress(arenaData.ChampionshipId, arenaData.Round);

                states = states.TransferAsset(
                    context,
                    context.Signer,
                    feeStoreAddress,
                    states.GetGoldCurrency() * summonRow.CostNcg * SummonCount
                );
            }

            // Ten plus one for summon
            if (SummonCount == 10)
            {
                SummonCount += 1;
            }

            for (var i = 0; i < SummonCount; i++)
            {
                var recipeId = SummonHelper.PickAuraSummonRecipe(summonRow, context.Random);

                // Validate RecipeId
                var recipeRow = recipeSheet.OrderedList.FirstOrDefault(r => r.Id == recipeId);
                if (recipeRow is null)
                {
                    throw new SheetRowNotFoundException(
                        addressesHex,
                        nameof(EquipmentItemRecipeSheet),
                        recipeId);
                }

                // Validate Recipe ResultEquipmentId
                if (!equipmentItemSheet.TryGetValue(recipeRow.ResultEquipmentId,
                        out var equipmentRow))
                {
                    throw new SheetRowNotFoundException(
                        addressesHex,
                        nameof(equipmentItemSheet),
                        recipeRow.ResultEquipmentId);
                }

                // Validate subRecipeId
                if (recipeRow.SubRecipeIds.Count == 0)
                {
                    throw new InvalidRecipeIdException(
                        $"Recipe {recipeId} does not have any subRecipe.");
                }

                var subRecipeId = recipeRow.SubRecipeIds[0];
                if (!equipmentItemSubRecipeSheetV2.TryGetValue(subRecipeId, out var subRecipeRow))
                {
                    throw new SheetRowNotFoundException(
                        addressesHex,
                        nameof(EquipmentItemSubRecipeSheetV2),
                        subRecipeId
                    );
                }


                // Create Equipment
                var equipment = (Equipment)ItemFactory.CreateItemUsable(
                    equipmentRow,
                    context.Random.GenerateRandomGuid(),
                    context.BlockIndex
                );

                AddAndUnlockOption(
                    agentState,
                    equipment,
                    context.Random,
                    subRecipeRow,
                    optionSheet,
                    skillSheet
                );

                // Add or update equipment
                avatarState.questList.UpdateCombinationEquipmentQuest(recipeId);
                avatarState.UpdateFromCombination(equipment);
                avatarState.UpdateQuestRewards(materialSheet);
            }

            Log.Debug(
                $"{addressesHex} AuraSummon Exec. finished: {DateTimeOffset.UtcNow - started} Elapsed");

            avatarState.blockIndex = context.BlockIndex;
            avatarState.updatedAt = context.BlockIndex;

            // Set states
            return states
                .SetState(AvatarAddress, avatarState.SerializeV2())
                .SetState(inventoryAddress, avatarState.inventory.Serialize())
                .SetState(questListAddress, avatarState.questList.Serialize())
                .SetState(context.Signer, agentState.Serialize());
        }

        public static void AddAndUnlockOption(
            AgentState agentState,
            Equipment equipment,
            IRandom random,
            EquipmentItemSubRecipeSheetV2.Row subRecipe,
            EquipmentItemOptionSheet optionSheet,
            SkillSheet skillSheet
        )
        {
            foreach (var optionInfo in subRecipe.Options
                         .OrderByDescending(e => e.Ratio)
                         .ThenBy(e => e.Id))
            {
                if (!optionSheet.TryGetValue(optionInfo.Id, out var optionRow))
                {
                    continue;
                }

                var value = random.Next(1, GameConfig.MaximumProbability + 1);
                var ratio = optionInfo.Ratio;

                if (value > ratio)
                {
                    continue;
                }

                if (optionRow.StatType != StatType.NONE)
                {
                    var stat = CombinationEquipment5.GetStat(optionRow, random);
                    equipment.StatsMap.AddStatAdditionalValue(stat.StatType, stat.BaseValue);
                    equipment.optionCountFromCombination++;
                    agentState.unlockedOptions.Add(optionRow.Id);
                }
                else
                {
                    var skill = CombinationEquipment.GetSkill(optionRow, skillSheet, random);
                    if (skill is null) continue;

                    equipment.Skills.Add(skill);
                    equipment.optionCountFromCombination++;
                    agentState.unlockedOptions.Add(optionRow.Id);
                }
            }
        }
    }
}
