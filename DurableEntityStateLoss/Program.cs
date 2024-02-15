using Azure.Core.Serialization;
using Microsoft.Extensions.Hosting;

new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(_ => { }, options =>
    {
        options.EnableUserCodeException = true;
        options.Serializer = new NewtonsoftJsonObjectSerializer();
    })
    .Build()
    .Run();
