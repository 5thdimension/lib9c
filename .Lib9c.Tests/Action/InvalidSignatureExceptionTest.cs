using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Lib9c.Action;
using Lib9c.Model.State;
using Libplanet.Crypto;
using Xunit;

namespace Lib9c.Tests.Action
{
    public class InvalidSignatureExceptionTest
    {
        [Fact]
        public void Serialize()
        {
            var formatter = new BinaryFormatter();
            using var ms = new MemoryStream();
            var pending = new PendingActivationState(
                new byte[] { 0x00 },
                new PrivateKey().PublicKey
            );
            var exc = new InvalidSignatureException(
                pending,
                new byte[] { 0x01 }
            );

            formatter.Serialize(ms, exc);
            ms.Seek(0, SeekOrigin.Begin);
            var deserialized =
                (InvalidSignatureException)formatter.Deserialize(ms);

            Assert.Equal(exc.Pending.Serialize(), deserialized.Pending.Serialize());
            Assert.Equal(exc.Signature, deserialized.Signature);
        }
    }
}
