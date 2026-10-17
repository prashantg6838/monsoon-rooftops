namespace MonsoonRooftops.Core
{
    public interface IGameState
    {
        GameStateId Id { get; }
        void Enter();
        void Tick(float deltaTime);
        void Exit();
    }
}
