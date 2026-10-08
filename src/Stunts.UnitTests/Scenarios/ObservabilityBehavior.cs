#pragma warning disable IDE0079
#pragma warning disable CS0436
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stunts;
using Xunit;

[assembly: Stunt<Stunts.UnitTests.IObservabilityOrders>]

namespace Stunts.UnitTests
{
    public class ObservabilityBehaviorTests : IRunnable
    {
        public void Run()
        {
            CompletedCallRecordsOneDurationAndOneActivity();
            TaskDurationIncludesTimeUntilCompletion().GetAwaiter().GetResult();
            RedactionDropsArgumentFromLogAndActivity();
            ReplaceShowsTheDisplayValue();
            SynchronousExceptionIsRecordedThenPropagates();
            CanceledTaskRecordsCanceledStatus().GetAwaiter().GetResult();
            InnerRetriesShareOneSampleAndOneActivity();
            RedactorExceptionDoesNotCallTheTarget();
            UnknownArgumentNameFailsBeforeTheTarget();
            InnerActivityIsParentedToTheObservabilityActivity();
            LoggerFailureDoesNotReplaceTheOutcome();
            NullLoggerOrMeterThrows();
        }

        public void CompletedCallRecordsOneDurationAndOneActivity()
        {
            using var observed = new ObservedCall(builtIn: true);
            var target = new CountingTarget();
            IObservabilityOrders orders = Create(observed.Behavior, target);

            Assert.Equal(1, orders.Count("m1"));

            var sample = Assert.Single(observed.Samples);
            Assert.Equal("stunts.invocation.duration", observed.InstrumentName);
            Assert.Equal("ms", observed.Unit);
            Assert.True(sample.Value >= 0);
            Assert.Equal("ok", Tag(sample.Tags, "status"));
            Assert.Equal(nameof(IObservabilityOrders.Count), Tag(sample.Tags, "member.name"));
            Assert.Equal(orders.GetType().FullName, Tag(sample.Tags, "stunt.type"));
            Assert.DoesNotContain(sample.Tags, tag => tag.Key.StartsWith("arg.", StringComparison.Ordinal));

            var activity = Assert.Single(observed.Activities);
            Assert.Equal(nameof(IObservabilityOrders.Count), activity.OperationName);
            Assert.Equal("Stunts", activity.Source.Name);
            Assert.Equal(ActivityStatusCode.Ok, activity.Status);
            Assert.Equal("m1", activity.GetTagItem("arg.sku"));

            var entry = Assert.Single(observed.Logger.Entries);
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Equal("StuntInvocation", entry.EventId.Name);
            Assert.Contains("Count", entry.Message, StringComparison.Ordinal);
            Assert.Contains("sku=m1", entry.Message, StringComparison.Ordinal);
            Assert.Contains("ok", entry.Message, StringComparison.Ordinal);
        }

        public async Task TaskDurationIncludesTimeUntilCompletion()
        {
            using var observed = new ObservedCall();
            var gate = new TaskCompletionSource<int>();
            IObservabilityOrders orders = Create(observed.Behavior, new DelayedTarget(gate));

            var pending = orders.TrackAsync("ord-1");

            Assert.False(pending.IsCompleted);
            Assert.Empty(observed.Samples);
            var started = Stopwatch.GetTimestamp();
            await Task.Delay(50);
            var waited = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            gate.SetResult(7);

            Assert.Equal(7, await pending);
            var sample = Assert.Single(observed.Samples);
            Assert.True(sample.Value >= waited - 15, $"duration {sample.Value} ms, waited {waited} ms");
            Assert.Equal(nameof(IObservabilityOrders.TrackAsync), Assert.Single(observed.Activities).OperationName);
        }

        public void RedactionDropsArgumentFromLogAndActivity()
        {
            using var observed = new ObservedCall();
            var behavior = observed.Behavior.WithRedaction(static (_, arguments) => arguments.Drop("card"));
            var target = new CountingTarget();
            IObservabilityOrders orders = Create(behavior, target);

            orders.Bill("4111111111111111", 12.5m);

            Assert.Equal("4111111111111111", target.Card);
            Assert.Equal(12.5m, target.Amount);
            var entry = Assert.Single(observed.Logger.Entries);
            Assert.DoesNotContain("4111111111111111", entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("card", entry.Message, StringComparison.Ordinal);
            Assert.Contains("amount=12.5", entry.Message, StringComparison.Ordinal);
            var activity = Assert.Single(observed.Activities);
            Assert.Null(activity.GetTagItem("arg.card"));
            Assert.Equal(12.5m, activity.GetTagItem("arg.amount"));
        }

        public void ReplaceShowsTheDisplayValue()
        {
            using var observed = new ObservedCall();
            var behavior = observed.Behavior.WithRedaction(static (_, arguments) => arguments.Replace("card", "***"));
            var target = new CountingTarget();
            IObservabilityOrders orders = Create(behavior, target);

            orders.Bill("4111111111111111", 1m);

            Assert.Equal("4111111111111111", target.Card);
            Assert.Contains("card=***", Assert.Single(observed.Logger.Entries).Message, StringComparison.Ordinal);
            Assert.Equal("***", Assert.Single(observed.Activities).GetTagItem("arg.card"));
        }

        public void SynchronousExceptionIsRecordedThenPropagates()
        {
            using var observed = new ObservedCall();
            var thrown = new InvalidOperationException("fail");
            IObservabilityOrders orders = Create(observed.Behavior, new ThrowingTarget(thrown));

            var actual = Assert.Throws<InvalidOperationException>(() => orders.Count("m1"));

            Assert.Same(thrown, actual);
            Assert.Equal("error", Tag(Assert.Single(observed.Samples).Tags, "status"));
            var entry = Assert.Single(observed.Logger.Entries);
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.Same(thrown, entry.Exception);
            Assert.Equal(ActivityStatusCode.Error, Assert.Single(observed.Activities).Status);
        }

        public async Task CanceledTaskRecordsCanceledStatus()
        {
            using var observed = new ObservedCall();
            IObservabilityOrders orders = Create(observed.Behavior, new CanceledTarget());

            await Assert.ThrowsAsync<TaskCanceledException>(() => orders.TrackAsync("ord-1"));

            Assert.Equal("canceled", Tag(Assert.Single(observed.Samples).Tags, "status"));
            Assert.Equal(LogLevel.Warning, Assert.Single(observed.Logger.Entries).Level);
            Assert.Equal(ActivityStatusCode.Unset, Assert.Single(observed.Activities).Status);
        }

        public void InnerRetriesShareOneSampleAndOneActivity()
        {
            using var observed = new ObservedCall();
            var twice = new TwiceBehavior();
            var stunt = Stunt.For<IObservabilityOrders>();
            IObservabilityOrders orders = stunt.ToObject();
            stunt.AddBehavior(observed.Behavior);
            stunt.AddBehavior(twice);

            orders.Count("m1");

            Assert.Equal(2, twice.Calls);
            Assert.Single(observed.Samples);
            Assert.Single(observed.Activities);
        }

        public void RedactorExceptionDoesNotCallTheTarget()
        {
            using var observed = new ObservedCall();
            var behavior = observed.Behavior.WithRedaction(static (_, _) => throw new InvalidOperationException("redact"));
            var target = new CountingTarget();
            IObservabilityOrders orders = Create(behavior, target);

            Assert.Throws<InvalidOperationException>(() => orders.Count("m1"));

            Assert.Equal(0, target.Calls);
            Assert.Empty(observed.Samples);
            Assert.Empty(observed.Activities);
            Assert.Empty(observed.Logger.Entries);
        }

        public void UnknownArgumentNameFailsBeforeTheTarget()
        {
            using var observed = new ObservedCall();
            var behavior = observed.Behavior.WithRedaction(static (_, arguments) => arguments.Drop("missing"));
            var target = new CountingTarget();
            IObservabilityOrders orders = Create(behavior, target);

            Assert.Throws<ArgumentOutOfRangeException>(() => orders.Count("m1"));

            Assert.Equal(0, target.Calls);
            Assert.Empty(observed.Samples);
        }

        public void InnerActivityIsParentedToTheObservabilityActivity()
        {
            using var observed = new ObservedCall();
            Activity? parent = null;
            IObservabilityOrders orders = Create(observed.Behavior, new ParentTarget(parentActivity => parent = parentActivity));

            orders.Count("m1");

            Assert.NotNull(parent);
            Assert.Equal(observed.Traces.Name, parent.Source.Name);
            Assert.Equal(nameof(IObservabilityOrders.Count), parent.OperationName);
        }

        public void LoggerFailureDoesNotReplaceTheOutcome()
        {
            using var observed = new ObservedCall(new ThrowingLogger());
            var target = new CountingTarget();
            IObservabilityOrders orders = Create(observed.Behavior, target);

            Assert.Equal(1, orders.Count("m1"));
            Assert.Single(observed.Samples);
        }

        public void NullLoggerOrMeterThrows()
        {
            using var meter = new Meter(nameof(NullLoggerOrMeterThrows));
            var histogram = meter.CreateHistogram<double>("stunts.invocation.duration", "ms");

            Assert.Throws<ArgumentNullException>(() => new ObservabilityBehavior(null!, meter));
            Assert.Throws<ArgumentNullException>(() => new ObservabilityBehavior(new ListLogger(), (Meter)null!));
            Assert.Throws<ArgumentNullException>(() => new ObservabilityBehavior(null!, histogram));
            Assert.Throws<ArgumentNullException>(() => new ObservabilityBehavior(new ListLogger(), (Histogram<double>)null!));
            Assert.Throws<ArgumentNullException>(() => new ObservabilityBehavior(new ListLogger(), meter).WithRedaction(null!));
        }

        static IObservabilityOrders Create(IStuntBehavior behavior, IStuntBehavior target)
        {
            var stunt = Stunt.For<IObservabilityOrders>();
            IObservabilityOrders orders = stunt.ToObject();
            stunt.AddBehavior(behavior);
            stunt.AddBehavior(target);
            return orders;
        }

        static object? Tag(IReadOnlyList<KeyValuePair<string, object?>> tags, string key)
        {
            foreach (var tag in tags)
            {
                if (tag.Key == key)
                    return tag.Value;
            }

            throw new InvalidOperationException(key);
        }

        sealed class ObservedCall : IDisposable
        {
            readonly Meter meter = new("Stunts.Test." + Guid.NewGuid().ToString("N"));
            readonly MeterListener metrics = new();
            readonly ActivityListener traces = new();
            readonly ActivitySource? ownedSource;

            public ObservedCall(ILogger? logger = null, bool builtIn = false)
            {
                Logger = new ListLogger();
                var log = logger ?? Logger;
                if (builtIn)
                    Traces = ObservabilityBehavior.Source;
                else
                    Traces = ownedSource = new ActivitySource("Stunts.Test." + Guid.NewGuid().ToString("N"));

                metrics.InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter == meter)
                    {
                        InstrumentName = instrument.Name;
                        Unit = instrument.Unit;
                        listener.EnableMeasurementEvents(instrument);
                    }
                };
                metrics.SetMeasurementEventCallback<double>((_, value, tags, _) =>
                    Samples.Add(new Sample(value, tags.ToArray())));
                metrics.Start();

                traces.ShouldListenTo = activitySource => activitySource == Traces || activitySource.Name == "Stunts.Probe";
                traces.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
                traces.ActivityStopped = Activities.Add;
                ActivitySource.AddActivityListener(traces);

                if (builtIn)
                    Behavior = new ObservabilityBehavior(log, meter);
                else
                    Behavior = new ObservabilityBehavior(log, meter.CreateHistogram<double>("stunts.invocation.duration", "ms"), Traces);
            }

            public ActivitySource Traces { get; }

            public ListLogger Logger { get; }

            public ObservabilityBehavior Behavior { get; }

            public List<Sample> Samples { get; } = new();

            public List<Activity> Activities { get; } = new();

            public string? InstrumentName { get; private set; }

            public string? Unit { get; private set; }

            public void Dispose()
            {
                traces.Dispose();
                metrics.Dispose();
                ownedSource?.Dispose();
                meter.Dispose();
            }
        }

        sealed record Sample(double Value, KeyValuePair<string, object?>[] Tags);

        sealed class ListLogger : ILogger
        {
            public List<Entry> Entries { get; } = new();

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => Entries.Add(new Entry(logLevel, eventId, exception, formatter(state, exception)));

            public sealed record Entry(LogLevel Level, EventId EventId, Exception? Exception, string Message);

            sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();

                public void Dispose()
                {
                }
            }
        }

        sealed class ThrowingLogger : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => throw new NotImplementedException();

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => throw new InvalidOperationException("log");
        }

        sealed class CountingTarget : IStuntBehavior
        {
            public int Calls { get; private set; }

            public string? Card { get; private set; }

            public decimal Amount { get; private set; }

            public bool AppliesTo(IMethodInvocation invocation) => true;

            public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
            {
                Calls++;
                if (invocation.Arguments.Contains("card"))
                {
                    Card = (string?)invocation.Arguments.GetValue("card");
                    Amount = (decimal)invocation.Arguments.GetValue("amount")!;
                }

                return invocation.MethodBase.Name == nameof(IObservabilityOrders.Bill)
                    ? invocation.CreateValueReturn(null, invocation.Arguments)
                    : invocation.CreateValueReturn(1);
            }
        }

        sealed class DelayedTarget(TaskCompletionSource<int> gate) : IStuntBehavior
        {
            public bool AppliesTo(IMethodInvocation invocation) => true;

            public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
                => invocation.CreateValueReturn(gate.Task);
        }

        sealed class ThrowingTarget(Exception exception) : IStuntBehavior
        {
            public bool AppliesTo(IMethodInvocation invocation) => true;

            public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next) => throw exception;
        }

        sealed class CanceledTarget : IStuntBehavior
        {
            public bool AppliesTo(IMethodInvocation invocation) => true;

            public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
                => invocation.CreateValueReturn(Task.FromCanceled<int>(new CancellationToken(true)));
        }

        sealed class TwiceBehavior : IStuntBehavior
        {
            readonly CountingTarget target = new();

            public int Calls => target.Calls;

            public bool AppliesTo(IMethodInvocation invocation) => true;

            public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
            {
                target.Execute(invocation, next);
                return target.Execute(invocation, next);
            }
        }

        sealed class ParentTarget(Action<Activity?> parent) : IStuntBehavior
        {
            public bool AppliesTo(IMethodInvocation invocation) => true;

            public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
            {
                using var probe = new ActivitySource("Stunts.Probe");
                using var activity = probe.StartActivity("probe");
                parent(activity?.Parent);
                return invocation.CreateValueReturn(1);
            }
        }
    }

    public interface IObservabilityOrders
    {
        int Count(string sku);

        Task<int> TrackAsync(string orderId);

        void Bill(string card, decimal amount);
    }
}
