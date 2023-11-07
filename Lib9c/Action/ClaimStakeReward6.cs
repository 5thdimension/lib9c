using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using Bencodex.Types;
using Lib9c;
using Lib9c.Abstractions;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Nekoyume.Extensions;
using Nekoyume.Helper;
using Nekoyume.Model.Item;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using static Lib9c.SerializeKeys;

namespace Nekoyume.Action
{
    /// <summary>
    /// Hard forked at https://github.com/planetarium/lib9c/pull/2071
    /// </summary>
    [ActionType(ActionTypeText)]
    [ActionObsolete(ObsoleteBlockIndex)]
    public class ClaimStakeReward6 : GameAction, IClaimStakeReward, IClaimStakeRewardV1
    {
        private const string ActionTypeText = "claim_stake_reward6";
        public const long ObsoleteBlockIndex = ActionObsoleteConfig.V200061ObsoleteIndex;

        /// <summary>
        /// This is the version 1 of the stake reward sheet.
        /// The version 1 is used for calculating the reward for the stake
        /// that is accumulated before the table patch.
        /// </summary>
        public static class V1
        {
            public const int MaxLevel = 5;

            public const string StakeRegularRewardSheetCsv =
                @"level,required_gold,item_id,rate,type,currency_ticker,currency_decimal_places,decimal_rate
1,50,400000,,Item,,,10
1,50,500000,,Item,,,800
1,50,20001,,Rune,,,6000
2,500,400000,,Item,,,8
2,500,500000,,Item,,,800
2,500,20001,,Rune,,,6000
3,5000,400000,,Item,,,5
3,5000,500000,,Item,,,800
3,5000,20001,,Rune,,,6000
4,50000,400000,,Item,,,5
4,50000,500000,,Item,,,800
4,50000,20001,,Rune,,,6000
5,500000,400000,,Item,,,5
5,500000,500000,,Item,,,800
5,500000,20001,,Rune,,,6000";

            public const string StakeRegularFixedRewardSheetCsv =
                @"level,required_gold,item_id,count
1,50,500000,1
2,500,500000,2
3,5000,500000,2
4,50000,500000,2
5,500000,500000,2";

            private static StakeRegularRewardSheet _stakeRegularRewardSheet;
            private static StakeRegularFixedRewardSheet _stakeRegularFixedRewardSheet;

            public static StakeRegularRewardSheet StakeRegularRewardSheet
            {
                get
                {
                    if (_stakeRegularRewardSheet is null)
                    {
                        _stakeRegularRewardSheet = new StakeRegularRewardSheet();
                        _stakeRegularRewardSheet.Set(StakeRegularRewardSheetCsv);
                    }

                    return _stakeRegularRewardSheet;
                }
            }

            public static StakeRegularFixedRewardSheet StakeRegularFixedRewardSheet
            {
                get
                {
                    if (_stakeRegularFixedRewardSheet is null)
                    {
                        _stakeRegularFixedRewardSheet = new StakeRegularFixedRewardSheet();
                        _stakeRegularFixedRewardSheet.Set(StakeRegularFixedRewardSheetCsv);
                    }

                    return _stakeRegularFixedRewardSheet;
                }
            }
        }

        /// <summary>
        /// This is the version 2 of the stake reward sheet.
        /// The version 2 is used for calculating the reward for the stake
        /// that is accumulated before the table patch.
        /// </summary>
        public static class V2
        {
            public const int MaxLevel = 7;

            public const string StakeRegularRewardSheetCsv =
                @"level,required_gold,item_id,rate,type,currency_ticker
1,50,400000,10,Item,
1,50,500000,800,Item,
1,50,20001,6000,Rune,
2,500,400000,8,Item,
2,500,500000,800,Item,
2,500,20001,6000,Rune,
3,5000,400000,5,Item,
3,5000,500000,800,Item,
3,5000,20001,6000,Rune,
4,50000,400000,5,Item,
4,50000,500000,800,Item,
4,50000,20001,6000,Rune,
5,500000,400000,5,Item,
5,500000,500000,800,Item,
5,500000,20001,6000,Rune,
6,5000000,400000,10,Item,
6,5000000,500000,800,Item,
6,5000000,20001,6000,Rune,
6,5000000,800201,100,Item,
7,10000000,400000,10,Item,
7,10000000,500000,800,Item,
7,10000000,20001,6000,Rune,
7,10000000,600201,50,Item,
7,10000000,800201,50,Item,
7,10000000,,100,Currency,GARAGE
";

            public const string StakeRegularFixedRewardSheetCsv =
                @"level,required_gold,item_id,count
1,50,500000,1
2,500,500000,2
3,5000,500000,2
4,50000,500000,2
5,500000,500000,2
6,5000000,500000,2
7,10000000,500000,2
";

            private static StakeRegularRewardSheet _stakeRegularRewardSheet;
            private static StakeRegularFixedRewardSheet _stakeRegularFixedRewardSheet;

            public static StakeRegularRewardSheet StakeRegularRewardSheet
            {
                get
                {
                    if (_stakeRegularRewardSheet is null)
                    {
                        _stakeRegularRewardSheet = new StakeRegularRewardSheet();
                        _stakeRegularRewardSheet.Set(StakeRegularRewardSheetCsv);
                    }

                    return _stakeRegularRewardSheet;
                }
            }

            public static StakeRegularFixedRewardSheet StakeRegularFixedRewardSheet
            {
                get
                {
                    if (_stakeRegularFixedRewardSheet is null)
                    {
                        _stakeRegularFixedRewardSheet = new StakeRegularFixedRewardSheet();
                        _stakeRegularFixedRewardSheet.Set(StakeRegularFixedRewardSheetCsv);
                    }

                    return _stakeRegularFixedRewardSheet;
                }
            }
        }

        internal Address AvatarAddress { get; private set; }

        Address IClaimStakeRewardV1.AvatarAddress => AvatarAddress;

        public ClaimStakeReward6(Address avatarAddress) : this()
        {
            AvatarAddress = avatarAddress;
        }

        public ClaimStakeReward6()
        {
        }

        protected override IImmutableDictionary<string, IValue> PlainValueInternal =>
            ImmutableDictionary<string, IValue>.Empty
                .Add(AvatarAddressKey, AvatarAddress.Serialize());

        protected override void LoadPlainValueInternal(
            IImmutableDictionary<string, IValue> plainValue)
        {
            AvatarAddress = plainValue[AvatarAddressKey].ToAddress();
        }

        public override IAccount Execute(IActionContext context)
        {
            context.UseGas(1);
            if (context.Rehearsal)
            {
                return context.PreviousState;
            }

            var states = context.PreviousState;
            var addressesHex = GetSignerAndOtherAddressesHex(context, AvatarAddress);
            if (!states.TryGetStakeState(context.Signer, out var stakeState))
            {
                throw new FailedLoadStateException(
                    ActionTypeText,
                    addressesHex,
                    typeof(StakeState),
                    StakeState.DeriveAddress(context.Signer));
            }

            if (!stakeState.IsClaimable(context.BlockIndex, out _, out _))
            {
                throw new RequiredBlockIndexException(
                    ActionTypeText,
                    addressesHex,
                    context.BlockIndex);
            }

            if (!states.TryGetAvatarStateV2(
                    context.Signer,
                    AvatarAddress,
                    out var avatarState,
                    out var migrationRequired))
            {
                throw new FailedLoadStateException(
                    ActionTypeText,
                    addressesHex,
                    typeof(AvatarState),
                    AvatarAddress);
            }

            var sheets = states.GetSheets(sheetTypes: new[]
            {
                typeof(StakeRegularRewardSheet),
                typeof(ConsumableItemSheet),
                typeof(CostumeItemSheet),
                typeof(EquipmentItemSheet),
                typeof(MaterialItemSheet),
            });

            var currency = states.GetGoldCurrency();
            var stakedAmount = states.GetBalance(stakeState.address, currency);
            var stakeRegularRewardSheet = sheets.GetSheet<StakeRegularRewardSheet>();
            var level =
                stakeRegularRewardSheet.FindLevelByStakedAmount(context.Signer, stakedAmount);
            var itemSheet = sheets.GetItemSheet();
            stakeState.CalculateAccumulatedItemRewards(
                context.BlockIndex,
                out var itemV1Step,
                out var itemV2Step,
                out var itemV3Step);
            stakeState.CalculateAccumulatedRuneRewards(
                context.BlockIndex,
                out var runeV1Step,
                out var runeV2Step,
                out var runeV3Step);
            stakeState.CalculateAccumulatedCurrencyRewards(
                context.BlockIndex,
                out var currencyV1Step,
                out var currencyV2Step,
                out var currencyV3Step);
            stakeState.CalculateAccumulatedCurrencyCrystalRewards(
                context.BlockIndex,
                out var currencyCrystalV1Step,
                out var currencyCrystalV2Step,
                out var currencyCrystalV3Step);
            if (itemV1Step > 0)
            {
                var v1Level = Math.Min(level, V1.MaxLevel);
                var fixedRewardV1 = V1.StakeRegularFixedRewardSheet[v1Level].Rewards;
                var regularRewardV1 = V1.StakeRegularRewardSheet[v1Level].Rewards;
                states = ProcessReward(
                    context,
                    states,
                    ref avatarState,
                    itemSheet,
                    stakedAmount,
                    itemV1Step,
                    runeV1Step,
                    currencyV1Step,
                    currencyCrystalV1Step,
                    fixedRewardV1,
                    regularRewardV1);
            }

            if (itemV2Step > 0)
            {
                var v2Level = Math.Min(level, V2.MaxLevel);
                var fixedRewardV2 = V2.StakeRegularFixedRewardSheet[v2Level].Rewards;
                var regularRewardV2 = V2.StakeRegularRewardSheet[v2Level].Rewards;
                states = ProcessReward(
                    context,
                    states,
                    ref avatarState,
                    itemSheet,
                    stakedAmount,
                    itemV2Step,
                    runeV2Step,
                    currencyV2Step,
                    currencyCrystalV2Step,
                    fixedRewardV2,
                    regularRewardV2);
            }

            if (itemV3Step > 0)
            {
                var regularFixedReward = GetRegularFixedRewardInfos(states, level);
                var regularReward = sheets.GetSheet<StakeRegularRewardSheet>()[level].Rewards;
                states = ProcessReward(
                    context,
                    states,
                    ref avatarState,
                    itemSheet,
                    stakedAmount,
                    itemV3Step,
                    runeV3Step,
                    currencyV3Step,
                    currencyCrystalV3Step,
                    regularFixedReward,
                    regularReward);
            }

            stakeState.Claim(context.BlockIndex);

            if (migrationRequired)
            {
                states = states
                    .SetState(avatarState.address, avatarState.SerializeV2())
                    .SetState(
                        avatarState.address.Derive(LegacyWorldInformationKey),
                        avatarState.worldInformation.Serialize())
                    .SetState(
                        avatarState.address.Derive(LegacyQuestListKey),
                        avatarState.questList.Serialize());
            }

            return states
                .SetState(stakeState.address, stakeState.Serialize())
                .SetState(
                    avatarState.address.Derive(LegacyInventoryKey),
                    avatarState.inventory.Serialize());
        }

        private static List<StakeRegularFixedRewardSheet.RewardInfo> GetRegularFixedRewardInfos(
            IAccountState states,
            int level)
        {
            return states.TryGetSheet<StakeRegularFixedRewardSheet>(out var fixedRewardSheet)
                ? fixedRewardSheet[level].Rewards
                : new List<StakeRegularFixedRewardSheet.RewardInfo>();
        }

        private IAccount ProcessReward(
            IActionContext context,
            IAccount states,
            ref AvatarState avatarState,
            ItemSheet itemSheet,
            FungibleAssetValue stakedFav,
            int itemRewardStep,
            int runeRewardStep,
            int currencyRewardStep,
            int currencyCrystalRewardStep,
            List<StakeRegularFixedRewardSheet.RewardInfo> fixedReward,
            List<StakeRegularRewardSheet.RewardInfo> regularReward)
        {
            // Regular Reward
            var random = context.GetRandom();
            foreach (var reward in regularReward)
            {
                var rateFav = FungibleAssetValue.Parse(
                    stakedFav.Currency,
                    reward.DecimalRate.ToString(CultureInfo.InvariantCulture));
                var rewardQuantityForSingleStep = stakedFav.DivRem(rateFav, out _);
                if (rewardQuantityForSingleStep <= 0)
                {
                    continue;
                }

                switch (reward.Type)
                {
                    case StakeRegularRewardSheet.StakeRewardType.Item:
                    {
                        if (itemRewardStep == 0)
                        {
                            continue;
                        }

                        var itemRow = itemSheet[reward.ItemId];
                        var item = itemRow is MaterialItemSheet.Row materialRow
                            ? ItemFactory.CreateTradableMaterial(materialRow)
                            : ItemFactory.CreateItem(itemRow, random);
                        var majorUnit = (int)rewardQuantityForSingleStep * itemRewardStep;
                        if (majorUnit < 1)
                        {
                            continue;
                        }

                        avatarState.inventory.AddItem(item, majorUnit);
                        break;
                    }
                    case StakeRegularRewardSheet.StakeRewardType.Rune:
                    {
                        if (runeRewardStep == 0)
                        {
                            continue;
                        }

                        var majorUnit = rewardQuantityForSingleStep * runeRewardStep;
                        if (majorUnit < 1)
                        {
                            continue;
                        }

                        var runeReward = RuneHelper.StakeRune * majorUnit;
                        states = states.MintAsset(context, AvatarAddress, runeReward);
                        break;
                    }
                    case StakeRegularRewardSheet.StakeRewardType.Currency:
                    {
                        if (string.IsNullOrEmpty(reward.CurrencyTicker))
                        {
                            throw new NullReferenceException("currency ticker is null or empty");
                        }

                        var isCrystal = reward.CurrencyTicker == Currencies.Crystal.Ticker;
                        if (isCrystal
                                ? currencyCrystalRewardStep == 0
                                : currencyRewardStep == 0)
                        {
                            continue;
                        }

                        var rewardCurrency = reward.CurrencyDecimalPlaces == null
                            ? Currencies.GetMinterlessCurrency(reward.CurrencyTicker)
                            : Currency.Uncapped(
                                reward.CurrencyTicker,
                                Convert.ToByte(reward.CurrencyDecimalPlaces.Value),
                                minters: null);
                        var majorUnit = isCrystal
                                ? rewardQuantityForSingleStep * currencyCrystalRewardStep
                                : rewardQuantityForSingleStep * currencyRewardStep;
                        var rewardFav = rewardCurrency * majorUnit;
                        states = states.MintAsset(
                            context,
                            context.Signer,
                            rewardFav);
                        break;
                    }
                    default:
                        throw new ArgumentException(
                            $"Can't handle reward type: {reward.Type}",
                            nameof(regularReward));
                }
            }

            // Fixed Reward
            foreach (var reward in fixedReward)
            {
                var itemRow = itemSheet[reward.ItemId];
                var item = itemRow is MaterialItemSheet.Row materialRow
                    ? ItemFactory.CreateTradableMaterial(materialRow)
                    : ItemFactory.CreateItem(itemRow, random);
                avatarState.inventory.AddItem(item, reward.Count * itemRewardStep);
            }

            return states;
        }
    }
}
