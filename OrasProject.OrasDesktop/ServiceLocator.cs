using System;
using Microsoft.Extensions.DependencyInjection;
using OrasProject.OrasDesktop.Services;
using OrasProject.OrasDesktop.Themes;
using OrasProject.OrasDesktop.ViewModels;

namespace OrasProject.OrasDesktop
{
    /// <summary>
    /// Service locator for dependency injection using Microsoft.Extensions.DependencyInjection
    /// </summary>
    public static class ServiceLocator
    {
        private static IServiceProvider? _serviceProvider;
        
        /// <summary>
        /// Initializes the service locator with configured services
        /// </summary>
        public static IServiceProvider Initialize()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
            return _serviceProvider;
        }
        
        /// <summary>
        /// Gets a service of the specified type
        /// </summary>
        public static T GetService<T>() where T : class
        {
            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("ServiceLocator has not been initialized. Call Initialize() first.");
            }
            
            var service = _serviceProvider.GetService<T>();
            if (service == null)
            {
                throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered.");
            }
            
            return service;
        }

        /// <summary>
        /// Gets the current service provider
        /// </summary>
        public static IServiceProvider Current
        {
            get
            {
                if (_serviceProvider == null)
                {
                    throw new InvalidOperationException("ServiceLocator has not been initialized. Call Initialize() first.");
                }
                return _serviceProvider;
            }
        }
        
        private static void ConfigureServices(IServiceCollection services)
        {
            // Register theme service as singleton
            services.AddSingleton<IThemeService, ThemeService>();
            
            // Register JSON highlight service as singleton (depends on IThemeService)
            services.AddSingleton<JsonHighlightService>();
            
            // Register registry service as singleton
            services.AddSingleton<IRegistryService, RegistryService>();

            // Register ViewModels
            services.AddTransient<MainViewModel>();
        }
    }
}
