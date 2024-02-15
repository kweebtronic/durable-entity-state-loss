using Microsoft.DurableTask;

namespace DurableEntityStateLoss
{
    // https://joonasw.net/view/track-activity-and-sub-orchestrator-progress-in-azure-durable-functions-orchestrators
    public static class TaskListTracker
    {
        public enum ActivityStatus
        {
            Started,
            Completed,
            Failed
        }

        private static ActivityStatus GetActivityStatusFromTask(Task task)
        {
            return task.Status switch
            {
                TaskStatus.Created => ActivityStatus.Started,
                TaskStatus.WaitingForActivation => ActivityStatus.Started,
                TaskStatus.WaitingToRun => ActivityStatus.Started,
                TaskStatus.Running => ActivityStatus.Started,
                TaskStatus.WaitingForChildrenToComplete => ActivityStatus.Started,
                TaskStatus.RanToCompletion => ActivityStatus.Completed,
                TaskStatus.Canceled => ActivityStatus.Failed,
                TaskStatus.Faulted => ActivityStatus.Failed,
                _ => throw new NotImplementedException(),
            };
        }

        public static async Task WhenAllWithScoreUpdate(
            this TaskOrchestrationContext context,
            DurableGameProxy durableGame,
            List<Task<ScoreUpdate>> tasks)
        {
            var activityStatuses = new ActivityStatus[tasks.Count];
            context.SetCustomStatus(activityStatuses.Select(s => s.ToString()));
            var doneActivityCount = 0;

            while (doneActivityCount < tasks.Count)
            {
                // Wait for one of the not done tasks to complete
                var notDoneTasks = tasks.Where((t, i) => activityStatuses[i] == ActivityStatus.Started);
                var doneTask = await Task.WhenAny(notDoneTasks);

                // Find which one completed
                var doneTaskIndex = tasks.FindIndex(t => ReferenceEquals(t, doneTask));
                // Sanity check
                if (doneTaskIndex < 0 || activityStatuses[doneTaskIndex] != ActivityStatus.Started)
                {
                    throw new Exception("Something went wrong, completed task not found or it was already completed");
                }

                activityStatuses[doneTaskIndex] = GetActivityStatusFromTask(doneTask);
                doneActivityCount++;

                await durableGame.LogScore(context, tasks[doneTaskIndex].Result);

                if (!context.IsReplaying)
                {
                    // Only update status when not replaying
                    context.SetCustomStatus(activityStatuses.Select(s => s.ToString()));
                }
            }

            var failedTasks = tasks.Where(t => t.Exception != null).ToList();
            if (failedTasks.Count > 0)
            {
                throw new AggregateException("One or more operations failed", failedTasks.Select(t => t.Exception));
            }
        }
    }
}
