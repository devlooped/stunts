using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Castle.DynamicProxy;

namespace Stunts
{
    class DynamicStuntInterceptor : IInterceptor, IStunt // Implemented to detect breaking changes in Stunt
    {
        static readonly MethodInfo expressionFactory = typeof(DynamicStuntInterceptor).GetMethod(nameof(CreatePipeline), BindingFlags.Static | BindingFlags.NonPublic);
        static readonly ConcurrentDictionary<Type, Func<BehaviorPipeline>> createPipelineFactories = new();

        readonly bool notImplemented;
        BehaviorPipeline? pipeline;

        internal DynamicStuntInterceptor(bool notImplemented) => this.notImplemented = notImplemented;

        public IList<IStuntBehavior> Behaviors => pipeline!.Behaviors;

        public virtual void Intercept(IInvocation invocation)
        {
            if (pipeline == null)
                pipeline = createPipelineFactories.GetOrAdd(invocation.Proxy.GetType(), type =>
                {
                    var expression = (Expression<Func<BehaviorPipeline>>)expressionFactory
                        .MakeGenericMethod(type)
                        .Invoke(null, null);

                    return expression.Compile();
                }).Invoke();

            if (invocation.Method.DeclaringType == typeof(IStunt))
            {
                invocation.ReturnValue = Behaviors;
                return;
            }

            var input = new MethodInvocation(invocation.Proxy, invocation.Method,
                (m, n) =>
                {
                    try
                    {
                        if (notImplemented)
                            throw new NotImplementedException();

                        // We need to adapt the invocation so it uses the potentially 
                        // updated values from the pipeline.
                        for (var i = 0; i < m.Arguments.Count; i++)
                            invocation.SetArgumentValue(i, m.Arguments.GetValue(i));

                        invocation.Proceed();

                        // return the new values, including arguments.
                        return m.CreateValueReturn(
                            invocation.ReturnValue,
                            new ArgumentCollection(m.Arguments.Select((arg, index) =>
                                arg.WithRawValue(invocation.GetArgumentValue(index))).ToArray()));
                    }
                    catch (Exception ex)
                    {
                        return m.CreateExceptionReturn(ex);
                    }
                },
                new ArgumentCollection(invocation.Method.GetParameters().Select((p, i) =>
                    new ObjectArgument(p, invocation.GetArgumentValue(i))).ToArray()));

            var returns = pipeline.Invoke(input);
            var exception = returns.Exception;

            if (exception != null)
                throw exception;

            invocation.ReturnValue = returns.ReturnValue;
            var indexed = input.Arguments.Select((p, i) => (p.Parameter.Name, i)).ToDictionary(x => x.Name, x => x.i);
            foreach (var output in returns.Outputs)
                invocation.SetArgumentValue(indexed[output.Parameter.Name], output.RawValue);
        }

        static Expression<Func<BehaviorPipeline>> CreatePipeline<TStunt>() => () => BehaviorPipelineFactory.Default.CreatePipeline<TStunt>();
    }
}
