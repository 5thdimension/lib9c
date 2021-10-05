using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Bencodex.Types;
using Libplanet;
using Libplanet.Blockchain;
using Libplanet.Blocks;
using Libplanet.Tx;
using Nekoyume.Action;
using Nekoyume.Model.State;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace Nekoyume.BlockChain.Policy
{
    // Collection of helper methods not directly used as a pluggable component.
    public partial class BlockPolicySource
    {
        internal static bool IsObsolete(Transaction<NCAction> transaction, long blockIndex)
        {
            return transaction.Actions
                .Select(action => action.InnerAction.GetType())
                .Any(
                    at =>
                    at.IsDefined(typeof(ActionObsoleteAttribute), false) &&
                    at.GetCustomAttributes()
                        .OfType<ActionObsoleteAttribute>()
                        .FirstOrDefault()?.ObsoleteIndex < blockIndex
                );
        }

        internal static bool IsAdminTransaction(
            BlockChain<NCAction> blockChain, Transaction<NCAction> transaction)
        {
            return GetAdminState(blockChain) is AdminState admin
                && admin.AdminAddress.Equals(transaction.Signer);
        }

        private static bool IsAuthorizedMinerTransactionRaw(
            Transaction<NCAction> transaction,
            ImmutableHashSet<Address> allAuthorizedMiners)
        {
            return allAuthorizedMiners.Contains(transaction.Signer);
        }

        internal static AdminState GetAdminState(
            BlockChain<NCAction> blockChain)
        {
            try
            {
                return blockChain.GetState(AdminState.Address) is Dictionary rawAdmin
                    ? new AdminState(rawAdmin)
                    : null;
            }
            catch (IncompleteBlockStatesException)
            {
                return null;
            }
        }

        // FIXME: Tx count validation should be done in libplanet, not here.
        // Should be removed once libplanet is updated.
        private static BlockPolicyViolationException ValidateTxCountPerBlockRaw(
            Block<NCAction> block,
            Func<long, int> getMinTransactionsPerBlock,
            Func<long, int> getMaxTransactionsPerSignerPerBlock)
        {
            if (block.Transactions.Count < getMinTransactionsPerBlock(block.Index))
            {
                return new BlockPolicyViolationException(
                    $"Block #{block.Index} {block.Hash} should include " +
                    $"at least {MinTransactionsPerBlock} transaction(s).");
            }
            else
            {
                int maxTransactionsPerSignerPerBlock =
                    getMaxTransactionsPerSignerPerBlock(block.Index);
                if (block.Transactions
                    .GroupBy(tx => tx.Signer)
                    .Any(group => group.Count() > maxTransactionsPerSignerPerBlock))
                {
                    return new BlockPolicyViolationException(
                        $"Block #{block.Index} {block.Hash} includes too many transactions " +
                        $"from a single signer where the maximum number of allowed by " +
                        $"a single signer per block is {maxTransactionsPerSignerPerBlock}.");
                }
            }

            return null;
        }

        private static BlockPolicyViolationException ValidateMinerAuthorityRaw(
            Block<NCAction> block,
            Func<long, ImmutableHashSet<Address>> getAuthorizedMiners,
            Func<long, bool> getAuthorizedMiningNoOpTxRequired)
        {
            ImmutableHashSet<Address> authorizedMiners = getAuthorizedMiners(block.Index);

            // For genesis block, any miner can mine.
            if (block.Index == 0)
            {
                return null;
            }
            // If not an authorized mining block index, any miner can mine.
            else if (authorizedMiners.IsEmpty)
            {
                return null;
            }
            // Otherwise, block's miner should be one of the authorized miners.
            else if (authorizedMiners.Contains(block.Miner))
            {
                return ValidateMinerAuthorityNoOpTxRaw(block, getAuthorizedMiningNoOpTxRequired);
            }
            else
            {
                return new BlockPolicyViolationException(
                    $"The block #{block.Index} {block.Hash} is not mined by an authorized miner.");
            }
        }

        private static BlockPolicyViolationException ValidateMinerAuthorityNoOpTxRaw(
            Block<NCAction> block,
            Func<long, bool> getAuthorizedMiningNoOpTxRequired)
        {
            if (getAuthorizedMiningNoOpTxRequired(block.Index))
            {
                // Authority is proven through a no-op transaction, i.e. a transaction
                // with zero actions, starting from ValidateMinerAuthorityNoOpHardcodedIndex.
                List<Transaction<NCAction>> txs = block.Transactions.ToList();
                if (!txs.Any(tx => tx.Signer.Equals(block.Miner) && !tx.Actions.Any())
                        && block.ProtocolVersion > 0)
                {
#if DEBUG
                    string debug =
                        "  Note that there " +
                        (txs.Count == 1
                            ? "is a transaction:"
                            : $"are {txs.Count} transactions:") +
                        txs.Select((tx, i) =>
                                $"\n    {i}. {tx.Actions.Count} actions; signed by {tx.Signer}")
                            .Aggregate(string.Empty, (a, b) => a + b);
#else
                    const string debug = "";
#endif
                    return new BlockPolicyViolationException(
                        $"Block #{block.Index} {block.Hash}'s miner {block.Miner} should be "
                            + "proven by including a no-op transaction signed by "
                            + "the same authority." + debug);
                }
            }

            return null;
        }

        private static BlockPolicyViolationException ValidateMinerPermissionRaw(
            Block<NCAction> block,
            Func<long, ImmutableHashSet<Address>> getPermissionedMiners)
        {
            Address miner = block.Miner;

            ImmutableHashSet<Address> permissionedMiners = getPermissionedMiners(block.Index);

            // If the set of permissioned miners is empty, any miner can mine.
            if (permissionedMiners.IsEmpty)
            {
                return null;
            }
            else
            {
                // If the set of permissioned miners is not empty, only miners in the set can mine.
                if (permissionedMiners.Contains(miner))
                {
                    // FIXME: Only existance of a transaction with miner signature was checked for
                    // some time.  Checking whether such transaction is a no-op transaction
                    // was missing.  This results in a different definition of proof transaction
                    // for authorized mining and permissioned mining.
                    if (block.Transactions
                        .Any(tx => tx.Signer.Equals(miner)))
                    {
                        return null;
                    }
                    else
                    {
                        return new BlockPolicyViolationException(
                            $"Block #{block.Index} {block.Hash} is mined by " +
                            $"a permissioned miner {miner}, but does not include " +
                            $"a proof transaction for permissioned mining.");
                    }
                }
                else
                {
                    return new BlockPolicyViolationException(
                        $"Block #{block.Index} {block.Hash} is not mined by " +
                        $"a permissioned miner: {miner}");
                }
            }
        }

        private static bool IsAllowedToMineRaw(
            Address miner,
            long index,
            Func<long, ImmutableHashSet<Address>> getAuthorizedMiners,
            Func<long, ImmutableHashSet<Address>> getPermissionedMiners)
        {
            ImmutableHashSet<Address> authorizedMiners = getAuthorizedMiners(index);
            ImmutableHashSet<Address> permissionedMiners = getPermissionedMiners(index);

            // For genesis blocks, any miner is allowed to mine.
            if (index == 0)
            {
                return true;
            }
            else if (!authorizedMiners.IsEmpty)
            {
                return authorizedMiners.Contains(miner);
            }
            else if (!permissionedMiners.IsEmpty)
            {
                return permissionedMiners.Contains(miner);
            }

            // If none of the conditions apply, any miner is allowed to mine.
            return true;
        }
    }
}
