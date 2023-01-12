using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Bencodex.Types;
using Lib9c.Action;
using Lib9c.DevExtensions.Action.Interface;
using Lib9c.Helper;
using Lib9c.Model.Faucet;
using Lib9c.Model.State;
using Lib9c.TableData;
using Libplanet.Action;

namespace Lib9c.DevExtensions.Action
{
    [Serializable]
    [ActionType("faucet_rune")]
    public class FaucetRune : GameAction, IFaucetRune
    {
        public Libplanet.Address AvatarAddress { get; set; }
        public List<FaucetRuneInfo> FaucetRuneInfos { get; set; }

        public override IAccountStateDelta Execute(IActionContext context)
        {
            if (context.Rehearsal)
            {
                return context.PreviousStates;
            }

            var states = context.PreviousStates;
            if (!(FaucetRuneInfos is null))
            {
                RuneSheet runeSheet = states.GetSheet<RuneSheet>();
                if (runeSheet.OrderedList != null)
                {
                    foreach (var rune in FaucetRuneInfos)
                    {
                        states = states.MintAsset(AvatarAddress, RuneHelper.ToFungibleAssetValue(
                            runeSheet.OrderedList.First(r => r.Id == rune.RuneId),
                            rune.Amount
                        ));
                    }
                }
            }

            return states;
        }

        protected override IImmutableDictionary<string, IValue> PlainValueInternal =>
            new Dictionary<string, IValue>
            {
                ["avatarAddress"] = AvatarAddress.Serialize(),
                ["faucetRuneInfos"] = FaucetRuneInfos
                    .OrderBy(x => x.RuneId)
                    .ThenBy(x => x.Amount)
                    .Select(x => x.Serialize())
                    .Serialize()
            }.ToImmutableDictionary();

        protected override void LoadPlainValueInternal(
            IImmutableDictionary<string, IValue> plainValue)
        {
            AvatarAddress = plainValue["avatarAddress"].ToAddress();
            FaucetRuneInfos = plainValue["faucetRuneInfos"].ToList(
                x => new FaucetRuneInfo((List)x)
            );
        }
    }
}
