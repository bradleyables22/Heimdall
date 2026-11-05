using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Heimdall.Server
{
    /// <summary>
    /// Provides additive registration helpers for Heimdall assemblies that contain
    /// content invocation methods.
    /// </summary>
    public static partial class HeimdallServiceCollection
    {
        /// <summary>
        /// Registers the assembly containing the specified marker type for Heimdall invocation discovery.
        /// </summary>
        /// <typeparam name="TMarker">
        /// A type located in the assembly that contains the Heimdall content to discover.
        /// </typeparam>
        /// <param name="services">The application's service collection.</param>
        /// <returns>The same service collection for chaining.</returns>
        public static IServiceCollection AddHeimdallInvocationAssembly<TMarker>(
            this IServiceCollection services)
            => services.AddHeimdallInvocationAssembly(typeof(TMarker).Assembly);

        /// <summary>
        /// Registers an assembly for Heimdall invocation discovery.
        /// </summary>
        /// <param name="services">The application's service collection.</param>
        /// <param name="assembly">The assembly containing Heimdall content to discover.</param>
        /// <returns>The same service collection for chaining.</returns>
        public static IServiceCollection AddHeimdallInvocationAssembly(
            this IServiceCollection services,
            Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(assembly);

            if (services.Any(descriptor =>
                descriptor.ServiceType == typeof(HeimdallInvocationAssemblyRegistration) &&
                descriptor.ImplementationInstance is HeimdallInvocationAssemblyRegistration registration &&
                registration.Assembly == assembly))
            {
                return services;
            }

            services.AddSingleton(new HeimdallInvocationAssemblyRegistration(assembly));
            return services;
        }

        /// <summary>
        /// Compatibility alias for the pre-invocation name.
        /// </summary>
        [Obsolete("Use AddHeimdallInvocationAssembly<TMarker>() instead.")]
        public static IServiceCollection AddHeimdallAssembly<TMarker>(
            this IServiceCollection services)
            => services.AddHeimdallInvocationAssembly<TMarker>();

        /// <summary>
        /// Compatibility alias for the pre-invocation name.
        /// </summary>
        [Obsolete("Use AddHeimdallInvocationAssembly(Assembly) instead.")]
        public static IServiceCollection AddHeimdallAssembly(
            this IServiceCollection services,
            Assembly assembly)
            => services.AddHeimdallInvocationAssembly(assembly);
    }

    internal sealed class HeimdallInvocationAssemblyRegistration
    {
        public HeimdallInvocationAssemblyRegistration(Assembly assembly)
        {
            Assembly = assembly;
        }

        public Assembly Assembly { get; }
    }
}
