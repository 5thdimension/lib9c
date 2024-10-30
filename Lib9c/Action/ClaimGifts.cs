using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Nekoyume.Extensions;
using Nekoyume.Model.Item;
using Nekoyume.Model.State;
using Nekoyume.Module;
using Nekoyume.TableData;
using static Lib9c.SerializeKeys;

namespace Nekoyume.Action
{
    [ActionType(ActionTypeText)]
    public class ClaimGifts : GameAction
    {
        private const string ActionTypeText = "claim_gifts";

        public Address AvatarAddress;

        public static Address ClaimedGiftIdsAddress(Address avatarAddress) =>
            avatarAddress.Derive("claimed_gift_ids");

        public ClaimGifts(Address avatarAddress)
        {
            AvatarAddress = avatarAddress;
        }

        public ClaimGifts()
        {
        }

        protected override IImmutableDictionary<string, IValue> PlainValueInternal =>
            ImmutableDictionary<string, IValue>.Empty
                .Add(AvatarAddressKey, AvatarAddress.Serialize());

        protected override void LoadPlainValueInternal(IImmutableDictionary<string, IValue> plainValue)
        {
            AvatarAddress = plainValue[AvatarAddressKey].ToAddress();
        }

        public override IWorld Execute(IActionContext context)
        {
            context.UseGas(1);
            var states = context.PreviousState;
            var random = context.GetRandom();
            var addressesHex = GetSignerAndOtherAddressesHex(context, AvatarAddress);

            var avatarState = states.GetAvatarState(AvatarAddress,
                getWorldInformation: false,
                getQuestList: false);
            if (avatarState is null)
            {
                throw new FailedLoadStateException(
                    ActionTypeText,
                    addressesHex,
                    typeof(AvatarState),
                    AvatarAddress);
            }

            var sheetTypes = new []
            {
                typeof(ClaimableGiftsSheet),
            };
            var sheets = states.GetSheets(
                containItemSheet: true,
                sheetTypes: sheetTypes);

            var claimableGiftsSheet = sheets.GetSheet<ClaimableGiftsSheet>();
            if (!claimableGiftsSheet.TryFindRowByBlockIndex(context.BlockIndex, out var giftRow))
            {
                throw new ClaimableGiftsDoesNotExistException(
                    $"[{addressesHex}] Claimable gift does not exist at block index: {context.BlockIndex}"
                );
            }

            var claimedGiftIdsAddress = ClaimedGiftIdsAddress(AvatarAddress);
            var claimedGiftIds = states.TryGetLegacyState(claimedGiftIdsAddress, out List rawIds)
                ? rawIds.ToList(StateExtensions.ToInteger)
                : new List<int>();
            if (claimedGiftIds.Contains(giftRow.Id))
            {
                throw new AlreadyClaimedGiftsException(
                    $"[{addressesHex}] Already claimed gift. You can only claim gift once : {giftRow.Id}"
                );
            }

            var itemSheet = sheets.GetItemSheet();
            foreach (var (itemId, quantity) in giftRow.Items)
            {
                var item = ItemFactory.CreateItem(itemSheet[itemId], random);
                if (item is INonFungibleItem)
                {
                    foreach (var _ in Enumerable.Range(0, quantity))
                    {
                        avatarState.inventory.AddItem(item);
                    }
                }
                else
                {
                    avatarState.inventory.AddItem(item, quantity);
                }
            }

            claimedGiftIds.Add(giftRow.Id);

            return states
                .SetLegacyState(claimedGiftIdsAddress, claimedGiftIds.Aggregate(List.Empty,
                    (current, giftId) => current.Add(giftId.Serialize())))
                .SetAvatarState(AvatarAddress, avatarState, setWorldInformation: false, setQuestList: false);
        }
    }
}
