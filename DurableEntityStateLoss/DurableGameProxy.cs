using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;

namespace DurableEntityStateLoss
{
    public class DurableGameProxy
    {
        public override string ToString() => DurableJobId.Key;

        public DurableGameProxy()
        {
        }

        public DurableGameProxy(string jobId)
        {
            DurableJobId = new EntityInstanceId(nameof(DurableGameEntity), jobId);
        }

        public EntityInstanceId DurableJobId { get; set; }
    }

    public static class DurableGameProxyExtensions 
    {
        public static async Task Initialise(this DurableGameProxy durableGame, TaskOrchestrationContext context, GameId input)
        {
            await context.Entities.CallEntityAsync(durableGame.DurableJobId, nameof(DurableGameEntity.Initialise), input);
        }

        public static async Task LogScore(this DurableGameProxy durableGame, TaskOrchestrationContext context, ScoreUpdate input)
        {
            await context.Entities.SignalEntityAsync(durableGame.DurableJobId, nameof(DurableGameEntity.LogScore), input);
        }

        public static async Task<DurableGame> GameOver(this DurableGameProxy durableGame, TaskOrchestrationContext context)
        {
            return await context.Entities.CallEntityAsync<DurableGame>(durableGame.DurableJobId, nameof(DurableGameEntity.GameOver));
        }
    }
}
