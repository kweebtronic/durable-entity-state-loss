using Microsoft.Extensions.Hosting;

new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(_ => { }, options =>
    {
        options.EnableUserCodeException = true;
    })
    .Build()
    .Run();
