using System;
using System.Collections.Generic;

namespace MonsoonRooftops.Core
{
    public class GameStateMachine
    {
        private readonly Dictionary<GameStateId, IGameState> _states = new Dictionary<GameStateId, IGameState>();

        public IGameState Current { get; private set; }

        public event Action<GameStateId, GameStateId> StateChanged;

        public void Register(IGameState state)
        {
            _states[state.Id] = state;
        }

        public void ChangeTo(GameStateId id)
        {
            if (!_states.TryGetValue(id, out var next))
                throw new InvalidOperationException($"State {id} is not registered.");

            var previousId = Current != null ? Current.Id : id;
            Current?.Exit();
            Current = next;
            Current.Enter();
            StateChanged?.Invoke(previousId, id);
        }

        public void Tick(float deltaTime)
        {
            Current?.Tick(deltaTime);
        }
    }
}
