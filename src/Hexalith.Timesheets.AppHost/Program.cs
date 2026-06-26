using Hexalith.EventStore.Aspire;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Initialize the shared local security service through the EventStore Aspire helper.
// Full Timesheets runtime topology remains owned by later infrastructure stories.
_ = builder.AddHexalithEventStoreSecurity();

await builder
    .Build()
    .RunAsync()
    .ConfigureAwait(false);
