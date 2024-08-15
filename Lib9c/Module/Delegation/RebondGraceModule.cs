#nullable enable
using System;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Nekoyume.Delegation;

namespace Nekoyume.Module.Delegation
{
    public static class RebondGraceModule
    {
        public static RebondGrace GetRebondGrace(
            this IWorldState world, IDelegatee delegatee, Address delegatorAddress)
            => GetRebondGrace(
                world,
                delegatee.RebondGraceAddress(delegatorAddress),
                delegatee.MaxRebondGraceEntries);

        public static RebondGrace GetRebondGrace(
            this IWorldState world, Address address, int maxEntries)
            => TryGetRebondGrace(world, address, maxEntries, out var rebondGrace)
                ? rebondGrace!
                : throw new InvalidOperationException("RebondGrace not found");

        public static bool TryGetRebondGrace(
            this IWorldState world,
            Address address,
            int maxEntries,
            out RebondGrace? rebondGrace)
        {
            try
            {
                var value = world.GetAccountState(Addresses.RebondGrace).GetState(address);
                if (!(value is Bencodex.Types.List list))
                {
                    rebondGrace = null;
                    return false;
                }

                rebondGrace = new RebondGrace(address, maxEntries, list);
                return true;
            }
            catch
            {
                rebondGrace = null;
                return false;
            }
        }
    }
}
