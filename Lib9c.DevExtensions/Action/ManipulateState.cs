using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Bencodex.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Nekoyume.Action;
using Nekoyume.Model.State;

namespace Lib9c.DevExtensions.Action
{
    [Serializable]
    [ActionType("manipulate_state")]
    public class ManipulateState : GameAction
    {
        public List<(Address addr, IValue value)> StateList { get; set; }
        public List<(Address addr, FungibleAssetValue fav)> BalanceList { get; set; }

        public override IAccountStateDelta Execute(IActionContext context)
        {
            if (context.Rehearsal)
            {
                return context.PreviousStates;
            }

            var states = context.PreviousStates;
            foreach (var (addr, value) in StateList)
            {
                states = states.SetState(addr, value);
            }

            var ncg = states.GetGoldCurrency();
            foreach (var (addr, fav) in BalanceList)
            {
                var currentFav = states.GetBalance(addr, fav.Currency);
                if (currentFav == fav)
                {
                    continue;
                }

                if (fav.Currency.Minters?.Any() ?? false)
                {
                    if (fav.Currency.Equals(ncg))
                    {
                        if (currentFav > fav)
                        {
                            states = states.TransferAsset(
                                addr,
                                GoldCurrencyState.Address,
                                currentFav - fav);
                        }
                        else
                        {
                            states = states.TransferAsset(
                                GoldCurrencyState.Address,
                                addr,
                                fav - currentFav);
                        }

                        continue;
                    }

                    throw new NotSupportedException($"{fav.Currency} is not supported.");
                }

                states = currentFav > fav
                    ? states.BurnAsset(addr, currentFav - fav)
                    : states.MintAsset(addr, fav - currentFav);
            }

            return states;
        }

        protected override IImmutableDictionary<string, IValue> PlainValueInternal =>
            new Dictionary<string, IValue>
            {
                ["stateList"] = StateList.Serialize(),
                ["bl"] = new List(BalanceList
                    .OrderBy(tuple => tuple.addr)
                    .ThenBy(tuple => tuple.fav)
                    .Select(tuple => new List(
                        tuple.addr.Serialize(),
                        tuple.fav.Serialize()))),
            }.ToImmutableDictionary();

        protected override void LoadPlainValueInternal(
            IImmutableDictionary<string, IValue> plainValue)
        {
            StateList = plainValue["stateList"].ToStateList();
            BalanceList = ((List)plainValue["bl"])
                .OfType<List>()
                .Select(list => (list[0].ToAddress(), list[1].ToFungibleAssetValue()))
                .ToList();
        }
    }
}
