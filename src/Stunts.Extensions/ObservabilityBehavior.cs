using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Stunts
{
    /// <summary>
    /// Logs a call, records its duration in milliseconds, and writes an activity named for the member.
    /// </summary>
    /// <remarks>
    /// Add this behavior first. The pipeline runs the first behavior outermost, so one activity and one
    /// duration cover every behavior added after it. The duration is <see cref="ProceedOutcome.Elapsed"/>
    /// in milliseconds and includes the time until a returned <see cref="Task"/> or <see cref="ValueTask"/> completes.
    /// </remarks>
    public sealed class ObservabilityBehavior : IStuntBehavior
    {
        /// <summary>Event name <c>StuntInvocation</c>.</summary>
        public static EventId StuntInvocation { get; } = new(1, "StuntInvocation");

        /// <summary>Activity source named <c>Stunts</c>.</summary>
        public static ActivitySource Source { get; } = new("Stunts");

        readonly ILogger logger;
        readonly Histogram<double> duration;
        readonly ActivitySource activities;
        readonly ObservabilityRedaction? redact;

        /// <summary>
        /// Observes every call with <paramref name="logger"/> and a histogram named
        /// <c>stunts.invocation.duration</c> (unit <c>ms</c>) on <paramref name="meter"/>.
        /// Activities come from <see cref="Source"/>.
        /// </summary>
        /// <param name="logger">Receives one event per settled call.</param>
        /// <param name="meter">Owns the duration histogram.</param>
        /// <param name="redact">Optional projection of arguments before they are logged or tagged.</param>
        /// <exception cref="ArgumentNullException"><paramref name="logger"/> or <paramref name="meter"/> is <see langword="null"/>.</exception>
        public ObservabilityBehavior(ILogger logger, Meter meter, ObservabilityRedaction? redact = null)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (meter == null)
                throw new ArgumentNullException(nameof(meter));

            this.logger = logger;
            duration = meter.CreateHistogram<double>("stunts.invocation.duration", unit: "ms");
            activities = Source;
            this.redact = redact;
        }

        /// <summary>
        /// Observes every call with <paramref name="logger"/> and <paramref name="duration"/>.
        /// The histogram receives <see cref="ProceedOutcome.Elapsed"/> in milliseconds, so create it with unit <c>ms</c>.
        /// </summary>
        /// <param name="logger">Receives one event per settled call.</param>
        /// <param name="duration">Duration histogram, in milliseconds.</param>
        /// <param name="activities">Activity source. <see cref="Source"/> is used when this is <see langword="null"/>.</param>
        /// <param name="redact">Optional projection of arguments before they are logged or tagged.</param>
        /// <exception cref="ArgumentNullException"><paramref name="logger"/> or <paramref name="duration"/> is <see langword="null"/>.</exception>
        public ObservabilityBehavior(ILogger logger, Histogram<double> duration, ActivitySource? activities = null, ObservabilityRedaction? redact = null)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (duration == null)
                throw new ArgumentNullException(nameof(duration));

            this.logger = logger;
            this.duration = duration;
            this.activities = activities ?? Source;
            this.redact = redact;
        }

        /// <summary>
        /// Returns a behavior that uses this instance's logger, histogram, and activity source with <paramref name="redact"/>.
        /// </summary>
        /// <param name="redact">Projection of arguments before they are logged or tagged.</param>
        /// <exception cref="ArgumentNullException"><paramref name="redact"/> is <see langword="null"/>.</exception>
        public ObservabilityBehavior WithRedaction(ObservabilityRedaction redact)
        {
            if (redact == null)
                throw new ArgumentNullException(nameof(redact));

            return new ObservabilityBehavior(logger, duration, activities, redact);
        }

        /// <summary>Observes every invocation.</summary>
        public bool AppliesTo(IMethodInvocation invocation) => true;

        /// <summary>
        /// Redacts arguments, starts an activity named for the member, and calls <paramref name="next"/> once through <c>Proceed</c>.
        /// A synchronous exception is recorded and then propagates. An awaitable is returned immediately and observed when it settles.
        /// </summary>
        public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
        {
            var projection = new ObservabilityArguments(invocation);
            redact?.Invoke(invocation, projection);

            var member = invocation.MethodBase.Name;
            var stuntType = StuntType(invocation.Target);
            var activity = activities.StartActivity(member, ActivityKind.Internal);
            if (activity != null)
            {
                activity.SetTag("stunt.type", stuntType);
                activity.SetTag("member.name", member);
                projection.Tag(activity);
            }

            return invocation.Proceed(next, (outcome, again) =>
            {
                Observe(outcome, activity, stuntType, member, projection);
                return new ValueTask<ProceedOutcome>(outcome);
            });
        }

        void Observe(ProceedOutcome outcome, Activity? activity, string stuntType, string member, ObservabilityArguments projection)
        {
            var status = Status(outcome);
            try
            {
                if (activity != null && outcome.Exception is not OperationCanceledException)
                    activity.SetStatus(outcome.Exception == null ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
            }
            catch
            {
                // A listener failure must not replace the invocation outcome.
            }

            try
            {
                duration.Record(outcome.Elapsed.TotalMilliseconds, new TagList
                {
                    { "stunt.type", stuntType },
                    { "member.name", member },
                    { "status", status }
                });
            }
            catch
            {
                // A listener failure must not replace the invocation outcome.
            }

            try
            {
                var level = outcome.Exception switch
                {
                    null => LogLevel.Information,
                    OperationCanceledException => LogLevel.Warning,
                    _ => LogLevel.Error
                };
                if (logger.IsEnabled(level))
                {
                    logger.Log(
                        level,
                        StuntInvocation,
                        outcome.Exception is OperationCanceledException ? null : outcome.Exception,
                        "{StuntType}.{Member} {Status} in {DurationMilliseconds} ms {Arguments}",
                        stuntType,
                        member,
                        status,
                        outcome.Elapsed.TotalMilliseconds,
                        projection.Format());
                }
            }
            catch
            {
                // A logger failure must not replace the invocation outcome.
            }

            try
            {
                activity?.Dispose();
            }
            catch
            {
                // Stopping the activity must not replace the invocation outcome.
            }
        }

        static string StuntType(object target)
        {
            var type = target.GetType();
            return type.FullName ?? type.Name;
        }

        static string Status(ProceedOutcome outcome)
            => outcome.Exception switch
            {
                null => "ok",
                OperationCanceledException => "canceled",
                _ => "error"
            };
    }

    /// <summary>
    /// Projects invocation arguments before <see cref="ObservabilityBehavior"/> logs or tags them.
    /// </summary>
    /// <param name="invocation">The invocation whose arguments can be dropped or replaced.</param>
    /// <param name="arguments">The display projection. The target receives the original arguments.</param>
    public delegate void ObservabilityRedaction(IMethodInvocation invocation, ObservabilityArguments arguments);
}
