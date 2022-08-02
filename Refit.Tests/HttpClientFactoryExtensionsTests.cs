
using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

namespace Refit.Tests
{
    using Microsoft.Extensions.DependencyInjection;

    using System.Text.Json;
    using Xunit;

    public class HttpClientFactoryExtensionsTests
    {
        class User
        {
        }

        class Role
        {
        }

        [Fact]
        public void GenericHttpClientsAreAssignedUniqueNames()
        {
            var services = new ServiceCollection();

            var userClientName = services.AddRefitClient<IBoringCrudApi<User, string>>().Name;
            var roleClientName = services.AddRefitClient<IBoringCrudApi<Role, string>>().Name;

            Assert.NotEqual(userClientName, roleClientName);
        }

        [Fact]
        public void RefitClientsGenericWithoutSettingsGetTheirOwnHttpClients()
        {
            var services = new ServiceCollection();

            services.AddRefitClient<IBoringCrudWithClientApi<User, string>>();
            services.AddRefitClient<IBoringOtherCrudWithClientApi<Role, string>>();

            var boringApi = services.BuildServiceProvider().GetRequiredService<IBoringCrudWithClientApi<User, string>>();
            var boringOtherApi = services.BuildServiceProvider().GetRequiredService<IBoringOtherCrudWithClientApi<Role, string>>();
            var sameClient = ReferenceEquals(boringApi.Client, boringOtherApi.Client);

            Assert.False(sameClient);
        }

        [Fact]
        public void RefitClientsGenericWithSameSettingsHttpClientNameShareTheSameHttpClient()
        {
            var services = new ServiceCollection();

            var refitSettings = new RefitSettings
            {
                HttpClientName = "ClientName1", HttpClientBaseAddress = new Uri("https://localhost:5001"),
            };
            services.AddTransient(_ => refitSettings);

            services.AddRefitClient<IBoringCrudWithClientApi<User, string>>(refitSettings);
            services.AddRefitClient<IBoringOtherCrudWithClientApi<Role, string>>(refitSettings);

            var boringApi = services.BuildServiceProvider().GetRequiredService<IBoringCrudWithClientApi<User, string>>();
            var boringOtherApi = services.BuildServiceProvider().GetRequiredService<IBoringOtherCrudWithClientApi<Role, string>>();
            var sameClient = ReferenceEquals(boringApi.Client, boringOtherApi.Client);

            Assert.True(sameClient);

            // Also confirm the client has the same base address as the settings base address.
            Assert.Equal(boringApi.Client.BaseAddress?.AbsoluteUri, refitSettings.HttpClientBaseAddress?.AbsoluteUri);
        }

        [Fact]
        public void RefitClientsGenericWithDifferentSettingsHttpClientNameGetTheirOwnHttpClients()
        {
            var services = new ServiceCollection();

            var refitSettings = new RefitSettings
            {
                HttpClientName = "ClientName1", HttpClientBaseAddress = new Uri("https://foo:5001"),
            };
            services.AddTransient(_ => refitSettings);

            services.AddRefitClient<IBoringCrudWithClientApi<User, string>>(refitSettings);

            var refitSettings2 = new RefitSettings
            {
                HttpClientName = "ClientName2", HttpClientBaseAddress = new Uri("https://bar:5002"),
            };
            services.AddTransient(_ => refitSettings2);

            services.AddRefitClient<IBoringOtherCrudWithClientApi<Role, string>>(refitSettings2);

            var boringApi = services.BuildServiceProvider().GetRequiredService<IBoringCrudWithClientApi<User, string>>();
            var boringOtherApi = services.BuildServiceProvider().GetRequiredService<IBoringOtherCrudWithClientApi<Role, string>>();
            var sameClient = ReferenceEquals(boringApi.Client, boringOtherApi.Client);

            Assert.False(sameClient);

            // Also confirm each client base address equals the corresponding settings base address.
            Assert.Equal(boringApi.Client.BaseAddress?.AbsoluteUri, refitSettings.HttpClientBaseAddress?.AbsoluteUri);
            Assert.Equal(boringOtherApi.Client.BaseAddress?.AbsoluteUri, refitSettings2.HttpClientBaseAddress?.AbsoluteUri);
        }

        [Fact]
        public void RefitClientsTypedWithoutSettingsGetTheirOwnHttpClients()
        {
            var services = new ServiceCollection();

            services.AddRefitClient(typeof(IBoringCrudWithClientApi<User, string>));
            services.AddRefitClient(typeof(IBoringOtherCrudWithClientApi<Role, string>));

            var boringApi = services.BuildServiceProvider().GetRequiredService<IBoringCrudWithClientApi<User, string>>();
            var boringOtherApi = services.BuildServiceProvider().GetRequiredService<IBoringOtherCrudWithClientApi<Role, string>>();
            var sameClient = ReferenceEquals(boringApi.Client, boringOtherApi.Client);

            Assert.False(sameClient);
        }

        [Fact]
        public void RefitClientsTypedWithSameSettingsHttpClientNameShareTheSameHttpClient()
        {
            var services = new ServiceCollection();

            var refitSettings = new RefitSettings
            {
                HttpClientName = "ClientName1", HttpClientBaseAddress = new Uri("https://localhost:5001"),
            };

            services.AddTransient(_ => refitSettings);

            services.AddRefitClient(typeof(IBoringCrudWithClientApi<User, string>), refitSettings);
            services.AddRefitClient(typeof(IBoringOtherCrudWithClientApi<Role, string>), refitSettings);

            var boringApi = services.BuildServiceProvider().GetRequiredService<IBoringCrudWithClientApi<User, string>>();
            var boringOtherApi = services.BuildServiceProvider().GetRequiredService<IBoringOtherCrudWithClientApi<Role, string>>();
            var sameClient = ReferenceEquals(boringApi.Client, boringOtherApi.Client);

            Assert.True(sameClient);

            // Also confirm the client has the same base address as the settings base address.
            Assert.Equal(boringApi.Client.BaseAddress?.AbsoluteUri, refitSettings.HttpClientBaseAddress?.AbsoluteUri);
        }

        [Fact]
        public void RefitClientsTypedWithDifferentSettingsHttpClientNameGetTheirOwnHttpClients()
        {
            var services = new ServiceCollection();

            var refitSettings = new RefitSettings
            {
                HttpClientName = "ClientName1", HttpClientBaseAddress = new Uri("https://foo:5001"),
            };
            services.AddTransient(_ => refitSettings);

            services.AddRefitClient(typeof(IBoringCrudWithClientApi<User, string>), refitSettings);

            var refitSettings2 = new RefitSettings
            {
                HttpClientName = "ClientName2", HttpClientBaseAddress = new Uri("https://bar:5002"),
            };
            services.AddTransient(_ => refitSettings2);

            services.AddRefitClient(typeof(IBoringOtherCrudWithClientApi<Role, string>), refitSettings2);

            var boringApi = services.BuildServiceProvider().GetRequiredService<IBoringCrudWithClientApi<User, string>>();
            var boringOtherApi = services.BuildServiceProvider().GetRequiredService<IBoringOtherCrudWithClientApi<Role, string>>();
            var sameClient = ReferenceEquals(boringApi.Client, boringOtherApi.Client);

            Assert.False(sameClient);

            // Also confirm each client base address equals the corresponding settings base address.
            Assert.Equal(boringApi.Client.BaseAddress?.AbsoluteUri, refitSettings.HttpClientBaseAddress?.AbsoluteUri);
            Assert.Equal(boringOtherApi.Client.BaseAddress?.AbsoluteUri, refitSettings2.HttpClientBaseAddress?.AbsoluteUri);
        }

        [Fact]
        public void HttpClientServicesAreAddedCorrectlyGivenGenericArgument()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddRefitClient<IFooWithOtherAttribute>();
            Assert.Contains(serviceCollection, z => z.ServiceType == typeof(SettingsFor<IFooWithOtherAttribute>));
            Assert.Contains(serviceCollection, z => z.ServiceType == typeof(IRequestBuilder<IFooWithOtherAttribute>));
        }

        [Fact]
        public void HttpClientServicesAreAddedCorrectlyGivenTypeArgument()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddRefitClient(typeof(IFooWithOtherAttribute));
            Assert.Contains(serviceCollection, z => z.ServiceType == typeof(SettingsFor<IFooWithOtherAttribute>));
            Assert.Contains(serviceCollection, z => z.ServiceType == typeof(IRequestBuilder<IFooWithOtherAttribute>));
        }

        [Fact]
        public void HttpClientReturnsClientGivenGenericArgument()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddRefitClient<IFooWithOtherAttribute>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            Assert.NotNull(serviceProvider.GetService<IFooWithOtherAttribute>());
        }

        [Fact]
        public void HttpClientWithSettingsHttpClientNameReturnsClientGivenGenericArgument()
        {
            var serviceCollection = new ServiceCollection();
            var refitSettings = new RefitSettings
            {
                HttpClientName = "ClientName1", HttpClientBaseAddress = new Uri("https://localhost:5001"),
            };
            serviceCollection.AddTransient(_ => refitSettings);
            serviceCollection.AddRefitClient<IFooWithOtherAttribute>(refitSettings);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            Assert.NotNull(serviceProvider.GetService<IFooWithOtherAttribute>());
        }

        [Fact]
        public void HttpClientReturnsClientGivenTypeArgument()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddRefitClient(typeof(IFooWithOtherAttribute));
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var refitService = serviceProvider.GetService(typeof(IFooWithOtherAttribute));
            Assert.NotNull(refitService);
        }

        [Fact]
        public void HttpClientWithSettingsHttpClientNameReturnsClientGivenTypeArgument()
        {
            var serviceCollection = new ServiceCollection();
            var refitSettings = new RefitSettings
            {
                HttpClientName = "ClientName1", HttpClientBaseAddress = new Uri("https://localhost:5001"),
            };
            serviceCollection.AddTransient(_ => refitSettings);
            serviceCollection.AddRefitClient(typeof(IFooWithOtherAttribute), refitSettings);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var refitService = serviceProvider.GetService(typeof(IFooWithOtherAttribute));
            Assert.NotNull(refitService);
        }

        [Fact]
        public void HttpClientSettingsAreInjectableGivenGenericArgument()
        {
            var serviceCollection = new ServiceCollection()
                .Configure<ClientOptions>(o => o.Serializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions()));
            serviceCollection.AddRefitClient<IFooWithOtherAttribute>(_ => new RefitSettings() {ContentSerializer = _.GetRequiredService<IOptions<ClientOptions>>().Value.Serializer});
            var serviceProvider = serviceCollection.BuildServiceProvider();
            Assert.Same(
                serviceProvider.GetRequiredService<IOptions<ClientOptions>>().Value.Serializer,
                serviceProvider.GetRequiredService<SettingsFor<IFooWithOtherAttribute>>().Settings!.ContentSerializer
            );
        }

        [Fact]
        public void HttpClientSettingsAreInjectableGivenTypeArgument()
        {
            var serviceCollection = new ServiceCollection()
                .Configure<ClientOptions>(o => o.Serializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions()));
            serviceCollection.AddRefitClient(typeof(IFooWithOtherAttribute), _ => new RefitSettings() {ContentSerializer = _.GetRequiredService<IOptions<ClientOptions>>().Value.Serializer});
            var serviceProvider = serviceCollection.BuildServiceProvider();
            Assert.Same(
                serviceProvider.GetRequiredService<IOptions<ClientOptions>>().Value.Serializer,
                serviceProvider.GetRequiredService<SettingsFor<IFooWithOtherAttribute>>().Settings!.ContentSerializer
            );
        }

        [Fact]
        public void HttpClientSettingsCanBeProvidedStaticallyGivenGenericArgument()
        {
            var contentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions());
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddRefitClient<IFooWithOtherAttribute>(new RefitSettings() {ContentSerializer = contentSerializer });
            var serviceProvider = serviceCollection.BuildServiceProvider();
            Assert.Same(
                contentSerializer,
                serviceProvider.GetRequiredService<SettingsFor<IFooWithOtherAttribute>>().Settings!.ContentSerializer
            );
        }

        [Fact]
        public void HttpClientSettingsCanBeProvidedStaticallyGivenTypeArgument()
        {
            var contentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions());
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddRefitClient<IFooWithOtherAttribute>(new RefitSettings() {ContentSerializer = contentSerializer });
            var serviceProvider = serviceCollection.BuildServiceProvider();
            Assert.Same(
                contentSerializer,
                serviceProvider.GetRequiredService<SettingsFor<IFooWithOtherAttribute>>().Settings!.ContentSerializer
            );
        }

        class ClientOptions
        {
            public SystemTextJsonContentSerializer Serializer { get; set; }
        }
    }
}
