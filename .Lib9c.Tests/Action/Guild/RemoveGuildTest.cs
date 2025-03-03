namespace Lib9c.Tests.Action.Guild
{
    using System;
    using Lib9c.Tests.Util;
    using Libplanet.Action.State;
    using Libplanet.Mocks;
    using Nekoyume.Action.Guild;
    using Nekoyume.Module.Guild;
    using Xunit;

    public class RemoveGuildTest
    {
        [Fact]
        public void Serialization()
        {
            var action = new RemoveGuild();
            var plainValue = action.PlainValue;

            var deserialized = new RemoveGuild();
            deserialized.LoadPlainValue(plainValue);
        }

        [Fact]
        public void Execute_By_GuildMember()
        {
            var action = new RemoveGuild();

            var guildMasterAddress = AddressUtil.CreateAgentAddress();
            var guildMemberAddress = AddressUtil.CreateAgentAddress();
            var guildAddress = AddressUtil.CreateGuildAddress();

            IWorld world = new World(MockWorldState.CreateModern());
            world = world.MakeGuild(guildAddress, guildMasterAddress);

            Assert.Throws<InvalidOperationException>(
                () => action.Execute(
                    new ActionContext
                    {
                        PreviousState = world,
                        Signer = guildMemberAddress,
                    }));
        }

        [Fact]
        public void Execute_By_GuildMaster()
        {
            var action = new RemoveGuild();

            var guildMasterAddress = AddressUtil.CreateAgentAddress();
            var guildAddress = AddressUtil.CreateGuildAddress();

            IWorld world = new World(MockWorldState.CreateModern());
            world = world.MakeGuild(guildAddress, guildMasterAddress);

            var changedWorld = action.Execute(
                new ActionContext
                {
                    PreviousState = world,
                    Signer = guildMasterAddress,
                });

            Assert.False(changedWorld.TryGetGuild(guildAddress, out _));
            Assert.Null(changedWorld.GetJoinedGuild(guildMasterAddress));
        }

        [Fact]
        public void Execute_By_Other()
        {
            var action = new RemoveGuild();

            var guildMasterAddress = AddressUtil.CreateAgentAddress();
            var otherAddress = AddressUtil.CreateAgentAddress();
            var guildAddress = AddressUtil.CreateGuildAddress();

            IWorld world = new World(MockWorldState.CreateModern());
            world = world.MakeGuild(guildAddress, guildMasterAddress);

            Assert.Throws<InvalidOperationException>(
                () => action.Execute(
                    new ActionContext
                    {
                        PreviousState = world,
                        Signer = otherAddress,
                    }));
        }

        [Fact]
        public void ResetBannedAddresses()
        {
            var action = new RemoveGuild();

            var guildMasterAddress = AddressUtil.CreateAgentAddress();
            var guildAddress = AddressUtil.CreateGuildAddress();
            var bannedAddress = AddressUtil.CreateAgentAddress();

            IWorld world = new World(MockWorldState.CreateModern());
            world = world.MakeGuild(guildAddress, guildMasterAddress)
                .Ban(guildAddress, guildMasterAddress, bannedAddress);

            Assert.True(world.IsBanned(guildAddress, bannedAddress));

            world = action.Execute(
                new ActionContext
                {
                    PreviousState = world,
                    Signer = guildMasterAddress,
                });

            Assert.False(world.IsBanned(guildAddress, bannedAddress));
        }
    }
}
