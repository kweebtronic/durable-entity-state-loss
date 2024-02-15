using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DurableEntityStateLoss
{
    public class DurableGameEntity(ILoggerFactory loggerFactory) : TaskEntity<DurableGame>
    {
        private readonly ILogger _logger = loggerFactory != null 
            ? loggerFactory.CreateLogger<DurableGameEntity>()
            : NullLogger.Instance;

        public const int PersistPeriodInSeconds = 2;

        public Task Initialise(GameId input)
        {
            State.Name = input.Name;
            State.CreatedDateTime = DateTime.UtcNow;
            for (var i = 0; i < input.Settings.NumberOfPlayers; i++)
            {
                State.PlayerStatuses[i] = new PlayerStatus { Score = 0, Status = Status.Playing };
            }

            return Task.FromResult(0);
        }

        public void LogScore(ScoreUpdate input)
        {
            if (string.IsNullOrWhiteSpace(State.Name))
            {
                var message = $"Missing State! - Root orchestrator = {Context.Id.Key}";
                _logger.LogError(message);
                throw new Exception(message);
            }

            State.Index++;
            State.PlayerStatuses[input.Player].Score += input.ScoreIncrease;
            
            if (ShouldPersistProgressUpdate()) Persist();
        }

        public DurableGame GameOver()
        {
            State.Index++;
            State.LastUpdatedDateTime = DateTime.UtcNow;
            Persist();

            return State;
        }

        private void Persist()
        {
            State.LastUpdatedDateTime = DateTime.UtcNow;

            var hiScoreIndex = State.PlayerStatuses.MaxBy(kvp => kvp.Value.Score).Key;

            foreach (var playerStatus in State.PlayerStatuses)
            {
                playerStatus.Value.Status = playerStatus.Key == hiScoreIndex
                    ? Status.Winning
                    : Status.Playing;
            }

            _logger.LogWarning($"{State.Identifier} - Player {hiScoreIndex} reached high score {State.PlayerStatuses[hiScoreIndex].Score}!");

            Context.ScheduleNewOrchestration(nameof(StateLossProofOfConcept.PersistStateOrchestrator), State);
        }

        private bool ShouldPersistProgressUpdate()
        {
            if (!State.LastUpdatedDateTime.HasValue)
            {
                return true;
            }

            return (DateTime.UtcNow - State.LastUpdatedDateTime.Value).TotalSeconds >= PersistPeriodInSeconds;
        }

        [Function(nameof(DurableGameEntity))]
        public static Task Run([EntityTrigger] TaskEntityDispatcher dispatcher)
        { 
            return dispatcher.DispatchAsync<DurableGameEntity>();
        }
    }

}
