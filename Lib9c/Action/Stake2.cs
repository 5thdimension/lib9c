using System;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using Bencodex.Types;
using Lib9c.Abstractions;
using Libplanet.Action;
using Libplanet.Action.State;
using Nekoyume.Model.State;
using Nekoyume.Module;
using Nekoyume.TableData;
using Serilog;
using static Lib9c.SerializeKeys;

namespace Nekoyume.Action
{
    [ActionType("stake2")]
    [ActionObsolete(ObsoleteBlockIndex)]
    public class Stake2 : GameAction, IStakeV1
    {
        public const long ObsoleteBlockIndex = ActionObsoleteConfig.V200080ObsoleteIndex;

        internal BigInteger Amount { get; set; }

        BigInteger IStakeV1.Amount => Amount;

        public Stake2(BigInteger amount)
        {
            Amount = amount >= 0
                ? amount
                : throw new ArgumentOutOfRangeException(nameof(amount));
        }

        public Stake2()
        {
        }

        protected override IImmutableDictionary<string, IValue> PlainValueInternal =>
            ImmutableDictionary<string, IValue>.Empty.Add(AmountKey, (IValue) (Integer) Amount);

        protected override void LoadPlainValueInternal(IImmutableDictionary<string, IValue> plainValue)
        {
            Amount = plainValue[AmountKey].ToBigInteger();
        }

        public override IWorld Execute(IActionContext context)
        {
            CheckObsolete(ObsoleteBlockIndex, context);
            context.UseGas(1);
            IWorld states = context.PreviousState;

            // Restrict staking if there is a monster collection until now.
            if (states.GetAgentState(context.Signer) is { } agentState &&
                states.TryGetLegacyState(MonsterCollectionState.DeriveAddress(
                    context.Signer,
                    agentState.MonsterCollectionRound), out Dictionary _))
            {
                throw new MonsterCollectionExistingException();
            }

            var addressesHex = GetSignerAndOtherAddressesHex(context, context.Signer);
            var started = DateTimeOffset.UtcNow;
            Log.Debug("{AddressesHex}Stake exec started", addressesHex);
            if (Amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(Amount));
            }

            var stakeRegularRewardSheet = states.GetSheet<StakeRegularRewardSheet>();
            var minimumRequiredGold = stakeRegularRewardSheet.OrderedRows.Min(x => x.RequiredGold);
            if (Amount != 0 && Amount < minimumRequiredGold)
            {
                throw new ArgumentOutOfRangeException(nameof(Amount));
            }

            var stakeStateAddress = StakeState.DeriveAddress(context.Signer);
            var currency = states.GetGoldCurrency();
            var currentBalance = states.GetBalance(context.Signer, currency);
            var stakedBalance = states.GetBalance(stakeStateAddress, currency);
            var targetStakeBalance = currency * Amount;
            if (currentBalance + stakedBalance < targetStakeBalance)
            {
                throw new NotEnoughFungibleAssetValueException(
                    context.Signer.ToHex(),
                    Amount,
                    currentBalance);
            }

            // Stake if it doesn't exist yet.
            if (!states.TryGetStakeState(context.Signer, out StakeState stakeState))
            {
                if (states.TryGetStakeStateV2(context.Signer, out _))
                {
                    throw new InvalidOperationException(
                        $"{context.Signer} has already staked as versions above 2.");
                }

                stakeState = new StakeState(stakeStateAddress, context.BlockIndex);
                return states
                    .SetLegacyState(
                        stakeStateAddress,
                        stakeState.SerializeV2())
                    .TransferAsset(context, context.Signer, stakeStateAddress, targetStakeBalance);
            }

            if (stakeState.IsClaimable(context.BlockIndex))
            {
                throw new StakeExistingClaimableException();
            }

            if (!stakeState.IsCancellable(context.BlockIndex) &&
                (context.BlockIndex >= 4611070
                    ? targetStakeBalance <= stakedBalance
                    : targetStakeBalance < stakedBalance))
            {
                throw new RequiredBlockIndexException();
            }

            // Cancel
            if (Amount == 0)
            {
                if (stakeState.IsCancellable(context.BlockIndex))
                {
                    return states
                        .RemoveLegacyState(stakeState.address)
                        .TransferAsset(context, stakeState.address, context.Signer, stakedBalance);
                }
            }

            var ended = DateTimeOffset.UtcNow;
            Log.Debug("{AddressesHex}Stake Total Executed Time: {Elapsed}", addressesHex, ended - started);

            // Stake with more or less amount.
            return states
                .TransferAsset(context, stakeState.address, context.Signer, stakedBalance)
                .TransferAsset(context, context.Signer, stakeState.address, targetStakeBalance)
                .SetLegacyState(
                    stakeState.address,
                    new StakeState(stakeState.address, context.BlockIndex).SerializeV2());
        }
    }
}
