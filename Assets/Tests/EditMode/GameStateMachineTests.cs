using System;
using MonsoonRooftops.Core;
using NUnit.Framework;

namespace MonsoonRooftops.Tests
{
    public class GameStateMachineTests
    {
        private class FakeState : IGameState
        {
            public GameStateId Id { get; }
            public int Enters, Ticks, Exits;
            public FakeState(GameStateId id) { Id = id; }
            public void Enter() => Enters++;
            public void Tick(float deltaTime) => Ticks++;
            public void Exit() => Exits++;
        }

        [Test]
        public void ChangeTo_ExitsOldAndEntersNew()
        {
            var machine = new GameStateMachine();
            var prepare = new FakeState(GameStateId.Prepare);
            var storm = new FakeState(GameStateId.Storm);
            machine.Register(prepare);
            machine.Register(storm);

            machine.ChangeTo(GameStateId.Prepare);
            machine.ChangeTo(GameStateId.Storm);

            Assert.AreEqual(1, prepare.Enters);
            Assert.AreEqual(1, prepare.Exits);
            Assert.AreEqual(1, storm.Enters);
            Assert.AreSame(storm, machine.Current);
        }

        [Test]
        public void Tick_OnlyTicksCurrentState()
        {
            var machine = new GameStateMachine();
            var a = new FakeState(GameStateId.Prepare);
            var b = new FakeState(GameStateId.Storm);
            machine.Register(a);
            machine.Register(b);
            machine.ChangeTo(GameStateId.Prepare);

            machine.Tick(0.1f);

            Assert.AreEqual(1, a.Ticks);
            Assert.AreEqual(0, b.Ticks);
        }

        [Test]
        public void ChangeTo_UnregisteredState_Throws()
        {
            var machine = new GameStateMachine();
            Assert.Throws<InvalidOperationException>(() => machine.ChangeTo(GameStateId.Results));
        }
    }
}
