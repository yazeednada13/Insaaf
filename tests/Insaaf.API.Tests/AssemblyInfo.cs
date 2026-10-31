using Xunit;

// Program.cs uses Serilog's static bootstrap logger, which can only be frozen once per process.
// Building two WebApplicationFactory hosts concurrently fails, so API test classes run sequentially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
