using System.Collections.Immutable;
using System.Security.Cryptography;
using Lib9c.Plugin.Shared;
using Libplanet.Action;
using Libplanet.Common;
using Libplanet.Extensions.ActionEvaluatorCommonComponents;
using Libplanet.Store;
using Nekoyume.Action;
using Nekoyume.Action.DPoS.Sys;
using Nekoyume.Action.Loader;

namespace Lib9c.Plugin
{
    public class PluginActionEvaluator : IPluginActionEvaluator
    {
        private readonly IActionEvaluator _actionEvaluator;

        public PluginActionEvaluator(IPluginKeyValueStore keyValueStore)
        {
            var stateStore = new TrieStateStore(new WrappedKeyValueStore(keyValueStore));
            _actionEvaluator = new ActionEvaluator(
                new PolicyActionsRegistry(
                    new IAction[] { new AllocateReward() }.ToImmutableArray(),
                    new IAction[]
                        {
                            new RecordProposer(),
                            new RewardGold()
                        }
                        .ToImmutableArray(),
                    new IAction[] { new Mortgage() }.ToImmutableArray(),
                    new IAction[] { new Reward(), new Refund() }.ToImmutableArray()),
                stateStore,
                new NCActionLoader());
        }

        public byte[][] Evaluate(byte[] blockBytes, byte[]? baseStateRootHashBytes)
        {
            var evals = _actionEvaluator.Evaluate(
                PreEvaluationBlockMarshaller.Deserialize(blockBytes),
                baseStateRootHashBytes is { } bytes ? new HashDigest<SHA256>(bytes) : null);
            return evals.Select(eval => ActionEvaluationMarshaller.Serialize(eval)).ToArray();
        }
    }
}
