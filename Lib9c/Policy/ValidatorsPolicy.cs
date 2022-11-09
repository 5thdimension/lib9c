using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Libplanet;
using Libplanet.Consensus;
using Libplanet.Crypto;

namespace Nekoyume.BlockChain.Policy
{
    public sealed class ValidatorsPolicy : VariableSubPolicy<ValidatorSet>
    {
        public static readonly PrivateKey TestValidatorKey = new PrivateKey(
            "0000000000000000000000000000000000000000000000000000000000000001");

        private ValidatorsPolicy(ValidatorSet defaultValue)
            : base(defaultValue)
        {
        }

        private ValidatorsPolicy(
            ValidatorsPolicy validatorsPolicy,
            SpannedSubPolicy<ValidatorSet> spannedSubPolicy)
            : base(validatorsPolicy, spannedSubPolicy)
        {
        }

        public static IVariableSubPolicy<ValidatorSet> Default =>
            new ValidatorsPolicy(new ValidatorSet(new List<PublicKey>()));

        public static IVariableSubPolicy<ValidatorSet> Mainnet =>
            Default
                .Add(new SpannedSubPolicy<ValidatorSet>(
                    startIndex: 0,
                    endIndex: null,
                    filter: null,
                    value: BlockPolicySource.Validators));

        public static IVariableSubPolicy<ValidatorSet> Permanent =>
            Default
                .Add(new SpannedSubPolicy<ValidatorSet>(
                    startIndex: 0,
                    endIndex: null,
                    filter: null,
                    value: BlockPolicySource.Validators));

        public static IVariableSubPolicy<ValidatorSet> Test =>
            Default
                .Add(new SpannedSubPolicy<ValidatorSet>(
                    startIndex: 0,
                    endIndex: null,
                    filter: null,
                    value: new ValidatorSet(
                        new List<PublicKey>()
                        {
                            TestValidatorKey.PublicKey,
                        })));
    }
}
