[![EULA](https://img.shields.io/badge/EULA-OSMF-blue?labelColor=black&color=C9FF30)](osmfeula.txt)
[![OSS](https://img.shields.io/github/license/devlooped/oss.svg?color=blue)](license.txt)
[![GitHub](https://img.shields.io/badge/-source-181717.svg?logo=GitHub)](https://github.com/devlooped/stunts)

<!-- include https://github.com/devlooped/.github/raw/main/osmf.md -->

`ObservabilityBehavior` logs each call, records its duration in milliseconds, and writes an activity named for the member. Add it before other behaviors so one observation covers the rest of the pipeline.

```csharp
builder.Services.AddStuntObservability();
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource("Stunts"))
    .WithMetrics(metrics => metrics.AddMeter("Stunts"));
```

The meter `Stunts` owns the histogram `stunts.invocation.duration` with unit `ms`. The activity source is `Stunts`. The logger category is `Stunts`.

```csharp
public sealed class Ordering(ObservabilityBehavior observation, IStuntBehavior inner)
{
    public IOrders Create()
    {
        var stunt = Stunt.For<IOrders>();
        stunt.AddBehavior(observation.WithRedaction(static (_, arguments) => arguments.Drop("card")));
        stunt.AddBehavior(inner);
        return stunt.ToObject();
    }
}
```

`Drop` omits that argument from the log and from activity tags. The target still receives the original value. `Replace` sets the displayed value.

<!-- include https://github.com/devlooped/sponsors/raw/main/footer.md -->

<!-- Exclude from auto-expansion by devlooped/actions-include GH action -->
<!-- exclude -->
