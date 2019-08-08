using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
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

        public static void AddResilientHttpClients<TClient, TImplementation>(this IServiceCollection services, IConfiguration configuration)
            where TClient : class
            where TImplementation : class, TClient
        {
            AddBaseHttpClient<TClient, TImplementation>(services);

            var httpClientConfiguration = configuration.GetSection("HttpClientConfiguration").GetChildren();
            foreach (var config in httpClientConfiguration)
            {
                if(string.IsNullOrEmpty(config["gatewayType"]) || string.IsNullOrEmpty(config["iGatewayType"]))
                {
                    throw new Exception("Gateway Type for HttpClient not specified");
                }
                
                AddResilientHttpClients(services, config["gatewayType"], config["iGatewayType"], c => 
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


        private static void AddResilientHttpClients(IServiceCollection services, string gatewayType, string iGatewayType, Action<HttpClient> config)
        {
            var reflectedType = typeof(HttpClientFactoryServiceCollectionExtensions);
            var reflectedMethod = reflectedType
                            .GetMethods()
                            .First(_ => _.Name=="AddHttpClient" 
                                        &&  _.IsGenericMethod 
                                        && _.GetGenericArguments().Count()==2 
                                        && _.GetParameters().Count()==2
                                    );

            var clientType = Type.GetType(gatewayType);            
            var iClientType = clientType.GetInterface(iGatewayType);

            MethodInfo invokeableMethod = reflectedMethod.MakeGenericMethod(iClientType, clientType);
            var invokedMethod = (IHttpClientBuilder)invokeableMethod.Invoke(null, new object[] { services, config });
            invokedMethod.AddPolicyHandler(ServiceCollectionExtensions.GetDefaultRetryPolicy());
            invokedMethod.AddPolicyHandler(ServiceCollectionExtensions.GetDefaultCircuitBreakerPolicy());
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
                .CircuitBreakerAsync(2, TimeSpan.FromSeconds(30));
        }
    }
}
