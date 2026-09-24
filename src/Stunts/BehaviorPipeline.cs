using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace Stunts
{
    /// <summary>
    /// Encapsulates a list of <see cref="IStuntBehavior"/>s
    /// and manages calling them in the proper order with the right inputs.
    /// </summary>
    public class BehaviorPipeline
    {
        /// <summary>
        /// Creates a new <see cref="BehaviorPipeline"/> with the given set of 
        /// <see cref="ExecuteHandler"/> delegates.
        /// </summary>
        /// <param name="behaviors">Behaviors to add to the pipeline.</param>
        public BehaviorPipeline(params ExecuteHandler[] behaviors)
            : this((IEnumerable<ExecuteHandler>)behaviors)
        {
        }

        /// <summary>
        /// Creates a new <see cref="BehaviorPipeline"/> with the given set of 
        /// <see cref="ExecuteHandler"/> delegates.
        /// </summary>
        /// <param name="behaviors">Behaviors to add to the pipeline.</param>
        public BehaviorPipeline(IEnumerable<ExecuteHandler> behaviors)
            : this(behaviors.Select(behavior => new AnonymousBehavior(behavior)))
        {
        }

        /// <summary>
        /// Creates a new <see cref="BehaviorPipeline"/> with the given set of <see cref="IStuntBehavior"/>s.
        /// </summary>
        /// <param name="behaviors">Behaviors to add to the pipeline.</param>
        public BehaviorPipeline(params IStuntBehavior[] behaviors)
            : this((IEnumerable<IStuntBehavior>)behaviors)
        {
        }

        /// <summary>
        /// Creates a new <see cref="BehaviorPipeline"/> with the given set of <see cref="IStuntBehavior"/>s.
        /// </summary>
        /// <param name="behaviors">Behaviors to add to the pipeline.</param>
        public BehaviorPipeline(IEnumerable<IStuntBehavior> behaviors)
            => Behaviors = new BehaviorsCollection(behaviors);

        /// <summary>
        /// Creates a new <see cref="BehaviorPipeline"/>.
        /// </summary>
        public BehaviorPipeline()
            => Behaviors = new BehaviorsCollection();

        /// <summary>
        /// Gets the collection of behaviors applied to this instance.
        /// </summary>
        /// <remarks>
        /// The behaviors in the pipeline can be modified freely at 
        /// any time, but changes to the list are not visible to in-flight 
        /// invocations. Additionally, the list implements <see cref="INotifyCollectionChanged"/> 
        /// to allow components to monitor changes to the pipeline, as well as <see cref="ISupportInitialize"/> 
        /// to perform batches of changes to the behaviors and avoid excessive collection change
        /// notifications.
        /// </remarks>
        public IList<IStuntBehavior> Behaviors { get; }

        /// <summary>
        /// Invoke the pipeline with the given input.
        /// </summary>
        /// <param name="invocation">Input to the method call.</param>
        /// <param name="throwOnException">Whether to throw the <see cref="IMethodReturn.Exception"/> if it has a value after running 
        /// the behaviors.</param>
        /// <returns>Return value from the pipeline.</returns>
        public IMethodReturn Invoke(IMethodInvocation invocation, bool throwOnException = false)
        {
            IMethodReturn InvokeTargetOrThrow(IMethodInvocation invocation) => invocation.HasImplementation ?
                invocation.CreateInvokeReturn() :
                throw new NotImplementedException(ThisAssembly.Strings.NotImplemented(invocation));

            if (Behaviors.Count == 0)
                return InvokeTargetOrThrow(invocation);

            // We convert to array so that the collection of behaviors can potentially 
            // be modified by behaviors themselves for a subsequent pipeline execution. 
            // The current pipeline execution, once started, cannot be modified, though.
            var behaviors = Behaviors.ToArray();

            var index = -1;
            for (var i = 0; i < behaviors.Length; i++)
            {
                if (!invocation.SkipBehaviors.Contains(behaviors[i].GetType()) && behaviors[i].AppliesTo(invocation))
                {
                    index = i;
                    break;
                }
            }

            if (index == -1)
                return InvokeTargetOrThrow(invocation);

            ExecuteHandler GetNext()
            {
                for (index++; index < behaviors.Length; index++)
                    if (!invocation.SkipBehaviors.Contains(behaviors[index].GetType()) && behaviors[index].AppliesTo(invocation))
                        break;

                return (index < behaviors.Length) ?
                    behaviors[index].Execute :
                    (m, n) => InvokeTargetOrThrow(m);
            }

            var result = behaviors[index].Execute(invocation, (m, n) => GetNext().Invoke(m, n));

            if (throwOnException && result.Exception != null)
                throw result.Exception;

            return result;
        }
    }
}
