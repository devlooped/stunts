#pragma warning disable IDE0079
#pragma warning disable CS0436
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Stunts;
using Xunit;

[assembly: Stunt<Stunts.Scenarios.NotifyPropertyChange.IPerson, INotifyPropertyChanged, INotifyPropertyChanging>]

namespace Stunts.Scenarios.NotifyPropertyChange
{
    public interface IPerson
    {
        string Name { get; set; }
    }

    /// <summary>
    /// A behavior can raise INotifyPropertyChanging/INotifyPropertyChanged from
    /// property setters, with per-stunt notification state.
    /// </summary>
    public class Test : IRunnable
    {
        public void Run()
        {
            NewValueRaisesChangingThenChanged_WithPropertyName_SetterRunsOnce();
            EqualValue_RaisesNeitherEvent_ButStillRunsSetter_ByDefault();
            EqualValue_SkipsSetter_WhenShortCircuitUnchanged();
            ThrowingSetter_RaisesNoPropertyChanged_AndDoesNotRecordValue();
            BuilderStuntsHaveIndependentNotificationState();
            Unsubscribe_StopsNotifications();
            GenericAddBehavior_MaterializesOnToObject();
            GenericAddBehavior_OnLivePipeline_AddsInstanceImmediately();
        }

        public void NewValueRaisesChangingThenChanged_WithPropertyName_SetterRunsOnce()
        {
            var stunt = Stunt.For<IPerson, INotifyPropertyChanged, INotifyPropertyChanging>();
            stunt.AddBehavior(new NotifyPropertyChangeBehavior());
            var sets = new CountingBehavior();
            stunt.AddBehavior(sets);
            stunt.AddBehavior(new DefaultValueBehavior());
            IPerson person = stunt.ToObject();

            var events = new List<string>();
            ((INotifyPropertyChanging)person).PropertyChanging += (_, e) => events.Add("Changing:" + e.PropertyName);
            ((INotifyPropertyChanged)person).PropertyChanged += (_, e) => events.Add("Changed:" + e.PropertyName);

            var baseline = sets.Calls;
            person.Name = "Ada";

            Assert.Equal(
                new[] { "Changing:Name", "Changed:Name" },
                events);
            Assert.Equal(baseline + 1, sets.Calls);
        }

        public void EqualValue_RaisesNeitherEvent_ButStillRunsSetter_ByDefault()
        {
            var stunt = Stunt.For<IPerson, INotifyPropertyChanged, INotifyPropertyChanging>();
            stunt.AddBehavior(new NotifyPropertyChangeBehavior());
            var sets = new CountingBehavior();
            stunt.AddBehavior(sets);
            stunt.AddBehavior(new DefaultValueBehavior());
            IPerson person = stunt.ToObject();

            var events = new List<string>();
            ((INotifyPropertyChanging)person).PropertyChanging += (_, e) => events.Add("Changing:" + e.PropertyName);
            ((INotifyPropertyChanged)person).PropertyChanged += (_, e) => events.Add("Changed:" + e.PropertyName);

            person.Name = "Ada";
            var baseline = sets.Calls;
            events.Clear();

            person.Name = "Ada";

            Assert.Empty(events);
            Assert.Equal(baseline + 1, sets.Calls);
        }

        public void EqualValue_SkipsSetter_WhenShortCircuitUnchanged()
        {
            var stunt = Stunt.For<IPerson, INotifyPropertyChanged, INotifyPropertyChanging>();
            stunt.AddBehavior(new NotifyPropertyChangeBehavior(shortCircuitUnchanged: true));
            var sets = new CountingBehavior();
            stunt.AddBehavior(sets);
            stunt.AddBehavior(new DefaultValueBehavior());
            IPerson person = stunt.ToObject();

            var events = new List<string>();
            ((INotifyPropertyChanging)person).PropertyChanging += (_, e) => events.Add("Changing:" + e.PropertyName);
            ((INotifyPropertyChanged)person).PropertyChanged += (_, e) => events.Add("Changed:" + e.PropertyName);

            person.Name = "Ada";
            var baseline = sets.Calls;
            events.Clear();

            person.Name = "Ada";

            Assert.Empty(events);
            Assert.Equal(baseline, sets.Calls);
        }

        public void ThrowingSetter_RaisesNoPropertyChanged_AndDoesNotRecordValue()
        {
            var stunt = Stunt.For<IPerson, INotifyPropertyChanged, INotifyPropertyChanging>();
            stunt.AddBehavior(new NotifyPropertyChangeBehavior());
            var thrower = new ThrowOnceBehavior();
            stunt.AddBehavior(thrower);
            stunt.AddBehavior(new DefaultValueBehavior());
            IPerson person = stunt.ToObject();

            var events = new List<string>();
            ((INotifyPropertyChanging)person).PropertyChanging += (_, e) => events.Add("Changing:" + e.PropertyName);
            ((INotifyPropertyChanged)person).PropertyChanged += (_, e) => events.Add("Changed:" + e.PropertyName);

            Assert.Throws<InvalidOperationException>(() => person.Name = "Ada");
            // Changing was raised before the setter ran; Changed was not.
            Assert.Equal(new[] { "Changing:Name" }, events);

            // The failed write was not recorded: the next set notifies normally.
            events.Clear();
            person.Name = "Ada";
            Assert.Equal(new[] { "Changing:Name", "Changed:Name" }, events);
        }

        public void BuilderStuntsHaveIndependentNotificationState()
        {
            var builder = Stunt.Builder();
            builder.AddBehavior<NotifyPropertyChangeBehavior>();
            builder.AddBehavior(new DefaultValueBehavior());

            IPerson first = builder.Build<IPerson, INotifyPropertyChanged, INotifyPropertyChanging>();
            IPerson second = builder.Build<IPerson, INotifyPropertyChanged, INotifyPropertyChanging>();

            var firstEvents = new List<string>();
            ((INotifyPropertyChanged)first).PropertyChanged += (_, e) => firstEvents.Add(e.PropertyName);

            second.Name = "Ada";

            Assert.Empty(firstEvents);
        }

        public void Unsubscribe_StopsNotifications()
        {
            var stunt = Stunt.For<IPerson, INotifyPropertyChanged, INotifyPropertyChanging>();
            stunt.AddBehavior(new NotifyPropertyChangeBehavior());
            stunt.AddBehavior(new DefaultValueBehavior());
            IPerson person = stunt.ToObject();

            var events = new List<string>();
            PropertyChangedEventHandler handler = (_, e) => events.Add(e.PropertyName);
            ((INotifyPropertyChanged)person).PropertyChanged += handler;

            person.Name = "Ada";
            Assert.Equal(new[] { "Name" }, events);

            ((INotifyPropertyChanged)person).PropertyChanged -= handler;
            person.Name = "Grace";

            Assert.Equal(new[] { "Name" }, events);
        }

        public void GenericAddBehavior_MaterializesOnToObject()
        {
            var stunt = Stunt.For<IPerson, INotifyPropertyChanged, INotifyPropertyChanging>();
            stunt.AddBehavior<NotifyPropertyChangeBehavior>();
            stunt.AddBehavior(new DefaultValueBehavior());
            IPerson person = stunt.ToObject();

            Assert.IsType<NotifyPropertyChangeBehavior>(stunt.Behaviors[0]);

            var events = new List<string>();
            ((INotifyPropertyChanged)person).PropertyChanged += (_, e) => events.Add(e.PropertyName);

            person.Name = "Ada";

            Assert.Equal(new[] { "Name" }, events);
        }

        public void GenericAddBehavior_OnLivePipeline_AddsInstanceImmediately()
        {
            var stunt = Stunt.For<IPerson, INotifyPropertyChanged, INotifyPropertyChanging>();
            IPerson person = stunt.ToObject();

            stunt.AddBehavior<NotifyPropertyChangeBehavior>();
            stunt.AddBehavior(new DefaultValueBehavior());

            Assert.IsType<NotifyPropertyChangeBehavior>(stunt.Behaviors[0]);

            var events = new List<string>();
            ((INotifyPropertyChanged)person).PropertyChanged += (_, e) => events.Add(e.PropertyName);

            person.Name = "Ada";

            Assert.Equal(new[] { "Name" }, events);
        }

        class CountingBehavior : IStuntBehavior
        {
            public int Calls;

            public bool AppliesTo(IMethodInvocation invocation) => true;

            public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
            {
                Calls++;
                return next(invocation, next);
            }
        }

        class ThrowOnceBehavior : IStuntBehavior
        {
            bool thrown;

            public bool AppliesTo(IMethodInvocation invocation)
                => invocation.MethodBase.Name.StartsWith("set_", StringComparison.Ordinal);

            public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
            {
                if (!thrown)
                {
                    thrown = true;
                    throw new InvalidOperationException("Simulated setter failure.");
                }
                return next(invocation, next);
            }
        }
    }
}
