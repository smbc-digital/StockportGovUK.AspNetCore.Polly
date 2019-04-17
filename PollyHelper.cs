using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace StockportGovUK.AspNetCore.Polly
{
    public static class HttpClientHelper
    {
        public static void Register<TClient, TImplementation>(IConfiguration configuration, IServiceCollection services)
            where TClient : class
            where TImplementation : class, TClient
        {
            AddBaseHttpClient<TClient, TImplementation>(services);

            var httpClientConfiguration = configuration.GetSection("HttpClientConfiguration").GetChildren();

            foreach (var config in httpClientConfiguration)
            {
                AddHttpClients(services, config["name"], c =>
                {
                    c.BaseAddress = new Uri(config["baseUrl"]);
                });
            }
        }

        private static void AddBaseHttpClient<TClient, TImplementation>(IServiceCollection services)
            where TClient : class
            where TImplementation : class, TClient
        {
            services.AddHttpClient<TClient, TImplementation>()
                .AddPolicyHandler(GetDefaultRetryPolicy())
                .AddPolicyHandler(GetDefaultCircuitBreakerPolicy());
        }

        private static void AddHttpClients(IServiceCollection services, string name, Action<HttpClient> config)
        {
            services.AddHttpClient(name, config)
                .AddPolicyHandler(GetDefaultRetryPolicy())
                .AddPolicyHandler(GetDefaultCircuitBreakerPolicy());
        }

        public static IAsyncPolicy<HttpResponseMessage> GetDefaultRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(2, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                );
        }

        public static IAsyncPolicy<HttpResponseMessage> GetDefaultCircuitBreakerPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(2, TimeSpan.FromSeconds(10));
        }
    }
}
