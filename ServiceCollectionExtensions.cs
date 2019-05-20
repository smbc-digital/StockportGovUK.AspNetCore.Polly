using System;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace StockportGovUK.AspNetCore.Polly
{
    public static class ServiceCollectionExtensions
    {
        public static void AddHttpClients<TClient, TImplementation>(this IServiceCollection services, IConfiguration configuration)
            where TClient : class
            where TImplementation : class, TClient
        {
            AddBaseHttpClient<TClient, TImplementation>(services);

            var httpClientConfiguration = configuration.GetSection("HttpClientConfiguration").GetChildren();
            foreach (var config in httpClientConfiguration)
            {
                if(string.IsNullOrEmpty(config["name"]))
                {
                    // TODO: Create new custom exception type 
                    throw new Exception("Name for HttpClient not specified");
                }
                
                AddHttpClients(services, config["name"], c => 
                {
                    c.BaseAddress = string.IsNullOrEmpty(config["baseUrl"]) ? null : new Uri(config["baseUrl"]);

                    c.DefaultRequestHeaders.Authorization = string.IsNullOrEmpty(config["authToken"])
                        ? null 
                        : new AuthenticationHeaderValue("Bearer", config["authToken"]);
                });
            }
        }

        private static void AddBaseHttpClient<TClient, TImplementation>(this IServiceCollection services)
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
