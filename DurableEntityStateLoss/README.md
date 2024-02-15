This is a contrived example to attempt to imitate the Durable Entity behaviours in my application after attempting to migrate to dotnet-isolated
It is the closest I have come to reliably replicating the case where my Entity State reverts to NULL, after retaining its value through a number of transactions.
I need this state to remain durable, for my application to function.

The key to illustrate here is that sometimes I signal the entity directly from my parent orchestrator, for broad state transitions.
Other times I wish to report back on the status of sub-orchestrations or activities upon completion, so there could be a higher volume of low-level status updates

These have completed correctly under dotnet 6, in the non-isolated worker model.
The issue is specifically with Microsoft.Azure.Functions.Worker.Extensions.DurableTask (1.x)