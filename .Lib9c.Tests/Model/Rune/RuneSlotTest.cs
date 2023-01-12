using Bencodex.Types;
using Lib9c.Model.EnumType;
using Lib9c.Model.Rune;
using Xunit;

namespace Lib9c.Tests.Model.Rune
{
    public class RuneSlotTest
    {
        [Fact]
        public void Serialize()
        {
            var slot = new RuneSlot(0, RuneSlotType.Default, RuneType.Stat, true);
            var serialized = (List)slot.Serialize();
            var deserialized = new RuneSlot(serialized);

            Assert.Equal(slot.RuneId, deserialized.RuneId);
            Assert.Equal(slot.RuneSlotType, deserialized.RuneSlotType);
            Assert.Equal(slot.RuneType, deserialized.RuneType);
            Assert.Equal(slot.Index, deserialized.Index);
            Assert.Equal(slot.IsLock, deserialized.IsLock);
        }
    }
}
