using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Refit
{
    public static class HttpClientFactoryExtensions
    {
        /// <summary>
        /// Adds a Refit client to the DI container
        /// </summary>
        /// <typeparam name="T">Type of the Refit interface</typeparam>
        /// <param name="services">container</param>
        /// <param name="settings">Optional. Settings to configure the instance with</param>
        /// <returns></returns>
        public static IHttpClientBuilder AddRefitClient<T>(this IServiceCollection services, RefitSettings? settings = null) where T : class
        {
            return AddRefitClient<T>(services, _ => settings);
        }

        /// <summary>
        /// Adds a Refit client to the DI container
        /// </summary>
        /// <param name="services">container</param>
        /// <param name="refitInterfaceType">Type of the Refit interface</param>
        /// <param name="settings">Optional. Settings to configure the instance with</param>
        /// <returns></returns>
        public static IHttpClientBuilder AddRefitClient(this IServiceCollection services, Type refitInterfaceType, RefitSettings? settings = null)
        {
            return AddRefitClient(services, refitInterfaceType, _ => settings);
        }

        /// <summary>
        /// Adds a Refit client to the DI container
        /// </summary>
        /// <typeparam name="T">Type of the Refit interface</typeparam>
        /// <param name="services">container</param>
        /// <param name="settingsAction">Optional. Action to configure refit settings.  This method is called once and only once, avoid using any scoped dependencies that maybe be disposed automatically.</param>
        /// <returns></returns>
        public static IHttpClientBuilder AddRefitClient<T>(this IServiceCollection services, Func<IServiceProvider, RefitSettings?>? settingsAction) where T : class
        {
            services.AddSingleton(provider => new SettingsFor<T>(settingsAction?.Invoke(provider)));
            services.AddSingleton(provider => RequestBuilder.ForType<T>(provider.GetRequiredService<SettingsFor<T>>().Settings));

            var settings = services.BuildServiceProvider().GetService<SettingsFor<T>>()?.Settings;
            var httpClientName = (settings == null || string.IsNullOrEmpty(settings.HttpClientName)) ? UniqueName.ForType<T>() : settings.HttpClientName!;

            var builder = services
                .AddHttpClient(httpClientName)
                .ConfigureHttpMessageHandlerBuilder(builder =>
                {
                    // check to see if user provided custom auth token
                    if (CreateInnerHandlerIfProvided(builder.Services.GetRequiredService<SettingsFor<T>>().Settings) is {} innerHandler)
                    {
                        builder.PrimaryHandler = innerHandler;
                    }
                });

            if (settings is null || string.IsNullOrEmpty(settings.HttpClientName))
                return builder.AddTypedClient((client, serviceProvider)
                                => RestService.For<T>(client, serviceProvider.GetService<IRequestBuilder<T>>()!));

            // Re-use HttpClients by name across multiple Refit interface types...
            services.TryAddSingleton(new RegisteredHttpClientsForRefit());
            return AddRefitClientWithNamedHttpClient<T>(builder, settings.HttpClientBaseAddress!, false, (client, serviceProvider)
                                => RestService.For<T>(client, serviceProvider.GetService<IRequestBuilder<T>>()!));
        }

        /// <summary>
        /// Adds a Refit client to the DI container
        /// </summary>
        /// <param name="services">container</param>
        /// <param name="refitInterfaceType">Type of the Refit interface</param>
        /// <param name="settingsAction">Optional. Action to configure refit settings.  This method is called once and only once, avoid using any scoped dependencies that maybe be disposed automatically.</param>
        /// <returns></returns>
        public static IHttpClientBuilder AddRefitClient(this IServiceCollection services, Type refitInterfaceType, Func<IServiceProvider, RefitSettings?>? settingsAction)
        {
            var settingsType = typeof(SettingsFor<>).MakeGenericType(refitInterfaceType);
            var requestBuilderType = typeof(IRequestBuilder<>).MakeGenericType(refitInterfaceType);
            services.AddSingleton(settingsType, provider => Activator.CreateInstance(typeof(SettingsFor<>).MakeGenericType(refitInterfaceType)!, settingsAction?.Invoke(provider))!);
            services.AddSingleton(requestBuilderType, provider => RequestBuilderGenericForTypeMethod.MakeGenericMethod(refitInterfaceType).Invoke(null, new object?[] { ((ISettingsFor)provider.GetRequiredService(settingsType)).Settings })!);

            var settings = ((ISettingsFor)services.BuildServiceProvider().GetService(settingsType))?.Settings;
            var httpClientName = (settings == null || string.IsNullOrEmpty(settings.HttpClientName)) ? UniqueName.ForType(refitInterfaceType) : settings.HttpClientName!;

            var builder = services
                .AddHttpClient(httpClientName)
                .ConfigureHttpMessageHandlerBuilder(builder =>
                {
                    // check to see if user provided custom auth token
                    if (CreateInnerHandlerIfProvided(((ISettingsFor)builder.Services.GetRequiredService(settingsType)).Settings) is { } innerHandler)
                    {
                        builder.PrimaryHandler = innerHandler;
                    }
                });

            if (settings is null || string.IsNullOrEmpty(settings.HttpClientName))
            {
                return builder.AddTypedClient(refitInterfaceType, (client, serviceProvider)
                            => RestService.For(refitInterfaceType, client, (IRequestBuilder)serviceProvider.GetRequiredService(requestBuilderType)));
            }

            services.TryAddSingleton(new RegisteredHttpClientsForRefit());
            return AddRefitClientWithNamedHttpClient(refitInterfaceType, builder, settings.HttpClientBaseAddress!, false, (client, serviceProvider)
                            => RestService.For(refitInterfaceType, client, (IRequestBuilder)serviceProvider.GetRequiredService(requestBuilderType)));
        }

        private static readonly MethodInfo RequestBuilderGenericForTypeMethod = typeof(RequestBuilder)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(z => z.IsGenericMethodDefinition && z.GetParameters().Length == 1);

        static HttpMessageHandler? CreateInnerHandlerIfProvided(RefitSettings? settings)
        {
            HttpMessageHandler? innerHandler = null;
            if (settings != null)
            {
                if (settings.HttpMessageHandlerFactory != null)
                {
                    innerHandler = settings.HttpMessageHandlerFactory();
                }

                if (settings.AuthorizationHeaderValueGetter != null)
                {
                    innerHandler = new AuthenticatedHttpClientHandler(settings.AuthorizationHeaderValueGetter, innerHandler);
                }
                else if (settings.AuthorizationHeaderValueWithParamGetter != null)
                {
                    innerHandler = new AuthenticatedParameterizedHttpClientHandler(settings.AuthorizationHeaderValueWithParamGetter, innerHandler);
                }
            }

            return innerHandler;
        }

        static IHttpClientBuilder AddTypedClient(this IHttpClientBuilder builder, Type type, Func<HttpClient, IServiceProvider, object> factory)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            builder.Services.AddTransient(type, s =>
            {
                var httpClientFactory = s.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(builder.Name);

                return factory(httpClient, s);
            });

            return builder;
        }

        // AddRefitClientWithNamedHttpClient<T>:
        // Reuses HttpClient across multiple Refit Service types when they share the same client name.
        // Using the same value for both the HttpClientName and the HttpClientBaseAddress is recommended.
        private static IHttpClientBuilder AddRefitClientWithNamedHttpClient<T>(IHttpClientBuilder builder, Uri baseAddress, bool overwriteExistingBaseAddress, Func<HttpClient, IServiceProvider, T> factory) where T : class
        {
            builder.Services.AddTransient<T>(s =>
            {
                var client = GetHttpClientByName(builder, builder.Name);

                if (client != null)
                {
                    if (client.BaseAddress == null || overwriteExistingBaseAddress)
                        client.BaseAddress = baseAddress;
                    return factory(client, s);;
                }

                var httpClientFactory = s.GetRequiredService<IHttpClientFactory>();
                client = httpClientFactory.CreateClient(builder.Name);
                client.BaseAddress = baseAddress;
                RegisterHttpClient(builder, builder.Name, client);
                return factory(client, s);
            });

            return builder;
        }

        // AddRefitClientWithNamedHttpClient: see comments for AddRefitClientWithNamedHttpClient<T>.
        private static IHttpClientBuilder AddRefitClientWithNamedHttpClient(Type type, IHttpClientBuilder builder, Uri baseAddress, bool overwriteExistingBaseAddress, Func<HttpClient, IServiceProvider, object> factory)
        {
            builder.Services.AddTransient(type, s =>
            {
                var client = GetHttpClientByName(builder, builder.Name);

                if (client != null)
                {
                    if (client.BaseAddress == null || overwriteExistingBaseAddress)
                        client.BaseAddress = baseAddress;
                    return factory(client, s);
                }

                var httpClientFactory = s.GetRequiredService<IHttpClientFactory>();
                client = httpClientFactory.CreateClient(builder.Name);
                client.BaseAddress = baseAddress;
                RegisterHttpClient(builder, builder.Name, client);
                return factory(client, s);
            });

            return builder;
        }

        private static HttpClient? GetHttpClientByName(IHttpClientBuilder builder, string name)
        {
            var registryService = builder.Services.FirstOrDefault(sd => sd.ServiceType == typeof(RegisteredHttpClientsForRefit));

            if (registryService == null)
            {
                builder.Services.TryAddSingleton(RegisteredHttpClientsForRefitInstance);
                registryService = builder.Services.First(sd => sd.ServiceType == typeof(RegisteredHttpClientsForRefit))!;
            }

            var registry = (RegisteredHttpClientsForRefit)registryService.ImplementationInstance!;

            registry.NamedClients.TryGetValue(name, out var httpClient);

            return httpClient;
        }

        private static readonly RegisteredHttpClientsForRefit RegisteredHttpClientsForRefitInstance = new();

        private static void RegisterHttpClient(IHttpClientBuilder builder, string name, HttpClient httpClient)
        {
            builder.Services.TryAddSingleton(RegisteredHttpClientsForRefitInstance);

            var registryService = builder.Services.First(sd => sd.ServiceType == typeof(RegisteredHttpClientsForRefit));
            var registry = (RegisteredHttpClientsForRefit)registryService.ImplementationInstance!;

            if (registry.NamedClients.ContainsKey(name))
                return;

            registry.NamedClients[name] = httpClient;
            return;
        }
    }
}
