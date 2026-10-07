using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Stunts
{
    /// <summary>
    /// Registers <see cref="ObservabilityBehavior"/> with the generic host.
    /// </summary>
    public static class ObservabilityServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a shared <see cref="ObservabilityBehavior"/> that logs as <c>Stunts</c>
        /// and records on a meter and activity source named <c>Stunts</c>.
        /// </summary>
        /// <param name="services">The service collection. Requires an <see cref="ILoggerFactory"/> and an <see cref="IMeterFactory"/>.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
        public static IServiceCollection AddStuntObservability(this IServiceCollection services)
        {
            if (services == null)
                throw new System.ArgumentNullException(nameof(services));

            services.AddSingleton(static provider => new ObservabilityBehavior(
                provider.GetRequiredService<ILoggerFactory>().CreateLogger("Stunts"),
                provider.GetRequiredService<IMeterFactory>().Create(new MeterOptions("Stunts"))));
            return services;
        }
    }
}
