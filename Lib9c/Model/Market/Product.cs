using System;
using Bencodex.Types;
using Libplanet;
using Libplanet.Assets;
using Nekoyume.Action;
using Nekoyume.Model.Item;
using Nekoyume.Model.State;

namespace Nekoyume.Model.Market
{
    public class Product
    {
        public static Address DeriveAddress(Guid productId)
        {
            return Addresses.Market.Derive(productId.ToString());
        }


        public Guid ProductId;
        public FungibleAssetValue Price;
        public long RegisteredBlockIndex;

        protected Product()
        {
        }

        protected Product(List serialized)
        {
            ProductId = serialized[0].ToGuid();
            Price = serialized[1].ToFungibleAssetValue();
            RegisteredBlockIndex = serialized[2].ToLong();
        }

        public virtual IValue Serialize()
        {
            return List.Empty
                .Add(ProductId.Serialize())
                .Add(Price.Serialize())
                .Add(RegisteredBlockIndex.Serialize());
        }
    }
}
