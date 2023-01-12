using System;
using System.Buffers;
using Bencodex;
using Bencodex.Types;
using Lib9c.Model.State;
using Libplanet.Assets;
using MessagePack;
using MessagePack.Formatters;

namespace Lib9c.Formatters
{
    public class FungibleAssetValueFormatter : IMessagePackFormatter<FungibleAssetValue>
    {
        public void Serialize(ref MessagePackWriter writer, FungibleAssetValue value,
            MessagePackSerializerOptions options)
        {
            writer.Write(new Codec().Encode(value.Serialize()));
        }

        public FungibleAssetValue Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            options.Security.DepthStep(ref reader);

            var bytes = reader.ReadBytes();
            if (bytes is null)
            {
                throw new NullReferenceException($"ReadBytes from serialized {nameof(FungibleAssetValue)} is null.");
            }

            return new Codec().Decode(bytes.Value.ToArray()).ToFungibleAssetValue();
        }
    }
}
