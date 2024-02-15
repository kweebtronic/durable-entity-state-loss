using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DurableEntityStateLoss
{
    public class StateLossProofOfConcept(ILogger<StateLossProofOfConcept> activityLog)
    {
        private const int RoundsToPlay = 1000;
        private readonly Random _random = new();
        private const string ScoresPath = @"c:\temp\";

        [Function(nameof(Play))]
        public async Task<IActionResult> Play([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req, 
            [DurableClient] DurableTaskClient client)
        {
            var settings = new GameSettings
            {
                NumberOfPlayers = _random.Next(4, 9),
                Rounds = RoundsToPlay
            };

            var tasks = new List<Task>();

            for (var i = 0; i < 3; i++)
            {
                tasks.Add(client.ScheduleNewOrchestrationInstanceAsync(nameof(GameOrchestrator), settings));
            }

            await Task.WhenAll(tasks);

            return new OkObjectResult("Launched");
        }

        [Function(nameof(GameOrchestrator))]
        public async Task GameOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context, GameSettings settings)
        {
            var log = context.CreateReplaySafeLogger(nameof(GameOrchestrator));
            var durableGame = new DurableGameProxy(context.InstanceId);

            var gameId = new GameId
            {
                Name = context.InstanceId,
                Settings = settings
            };

            await durableGame.Initialise(context, gameId);

            var plays = new List<Play>();
            for (var i = 0; i < settings.Rounds; i++)
            {
                plays.Add(new Play
                {
                    Round = i,
                    Settings = settings
                });
            }

            var tasks = plays.Chunk(settings.Rounds / 20)
                .Select(rounds => context.CallSubOrchestratorAsync(nameof(SubGameOrchestrator), new SubGame { GameId = gameId, Plays = [..rounds] }))
                .ToList();

            await Task.WhenAll(tasks);

            var finalResult = await durableGame.GameOver(context);

            log.LogError($"{durableGame.DurableJobId} <<GAME OVER>> - Index: {finalResult.Index}");
        }

        [Function(nameof(SubGameOrchestrator))]
        public async Task SubGameOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context, SubGame subGame)
        {
            var log = context.CreateReplaySafeLogger(nameof(SubGameOrchestrator));
            var durableGame = new DurableGameProxy(subGame.GameId.Name);

            try
            {
                var tasks = subGame.Plays
                    .Select(play => context.CallActivityAsync<ScoreUpdate>(nameof(PlayARound), play))
                    .ToList();

                await context.WhenAllWithScoreUpdate(durableGame, tasks);
            }
            catch (Exception e)
            {
                log.LogError(e.Message);
            }
        }

        [Function(nameof(PlayARound))]
        public ScoreUpdate PlayARound([ActivityTrigger] Play input)
        {
            Thread.Sleep(_random.Next(10, 200)); // Want activities to complete at randomly-different times, imitating an async callout

            var response = new ScoreUpdate
            {
                Player = _random.Next(0, input.Settings.NumberOfPlayers), 
                ScoreIncrease = _random.Next(0, 300),
                Settings = input.Settings
            };

            activityLog.LogInformation($"{nameof(PlayARound)} completed - {input.Round} = {JsonSerializer.Serialize(response)}");
            return response;
        }


        [Function(nameof(PersistStateOrchestrator))]
        public async Task PersistStateOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context, DurableGame state)
        {
            await context.CallActivityAsync(nameof(PersistState), state);
        }

        // This is in *indication* of what I might do in production... write out to some easily-readable storage outside of functions land
        [Function(nameof(PersistState))]
        public async Task PersistState([ActivityTrigger] DurableGame state)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine(state.Identifier +
                                     "\n***************" +
                                     "\nCurrent scores:\n");

            foreach (var player in state.PlayerStatuses.OrderByDescending(s => s.Value.Score))
            {
                stringBuilder.AppendLine($"{player.Key} - {player.Value}");
            }

            stringBuilder.AppendLine("\n***************\n");

            activityLog.LogCritical(stringBuilder.ToString());

            if (IsFunctionsDevelopmentEnvironment())
            {
                await File.WriteAllTextAsync($"{ScoresPath}{state.Identifier}.json", JsonSerializer.Serialize(state));
            }
        }

        public static bool IsFunctionsDevelopmentEnvironment()
        {
            return Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") == Environments.Development;
        }
    }

    public class GameSettings
    {
        public int NumberOfPlayers { get; set; }
        public int Rounds { get; set; }
    }

    public class GameId
    {
        public string Name { get; set; }
        public GameSettings Settings { get; set; }
    }

    public class Play
    {
        public int Round { get; set; }
        public GameSettings Settings { get; set; }
    }

    public class SubGame
    {
        public GameId GameId { get; set; }
        public List<Play> Plays { get; set; }
    }

    public class ScoreUpdate
    {
        public int Player { get; set; }
        public int ScoreIncrease { get; set; }
        public GameSettings Settings { get; set; }
    }
}
