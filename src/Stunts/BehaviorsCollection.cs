using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace Stunts
{
    /// <summary>
    /// The collection of behaviors of a <see cref="BehaviorPipeline"/>.
    /// </summary>
    /// <remarks>
    /// Beyond the typical usage as an <see cref="ObservableCollection{T}"/>, this 
    /// collection implements <see cref="ISupportInitialize"/> to allow adding behaviors 
    /// in batches to optimize the change notifications performance. When 
    /// <see cref="ISupportInitialize.BeginInit"/> is invoked, collection change notifications 
    /// will be suspended entirely, and when the subsequent <see cref="ISupportInitialize.EndInit"/> 
    /// is invoked, a single notification with <see cref="NotifyCollectionChangedAction.Reset"/> 
    /// will be raised instead.
    /// </remarks>
    class BehaviorsCollection : ObservableCollection<IStuntBehavior>, ISupportInitialize, ICloneable
    {
        bool initializing;

        /// <inheritdoc/>
        public BehaviorsCollection() { }

        /// <inheritdoc/>
        public BehaviorsCollection(IEnumerable<IStuntBehavior> collection) : base(collection) { }

        /// <summary>
        /// Suspends collection change notifications until <see cref="EndInit"/> is called.
        /// </summary>
        public void BeginInit() => initializing = true;

        /// <summary>
        /// Creates a copy of the collection.
        /// </summary>
        public object Clone() => new BehaviorsCollection(this.ToArray());

        /// <summary>
        /// Raises a single collection change notification with 
        /// <see cref="NotifyCollectionChangedAction.Reset"/> and resumes 
        /// normal collection change notifications afterwards.
        /// </summary>
        public void EndInit()
        {
            initializing = false;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// Checks whether <see cref="BeginInit"/> has been called, and if not, calls the 
        /// base class.
        /// </summary>
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!initializing)
                base.OnCollectionChanged(e);
        }
    }
}
