using System;

namespace Stunts
{
    /// <summary>
    /// Requests a compile-time stunt for <typeparamref name="T"/> when no factory call
    /// in the project closes over that type, such as a type only known at run time:
    /// <c>[assembly: Stunt&lt;IDictionary&lt;string, int&gt;&gt;]</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class StuntAttribute<T> : Attribute { }

    /// <summary>
    /// Requests a compile-time stunt for <typeparamref name="T"/> that also implements 
    /// <typeparamref name="T1"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class StuntAttribute<T, T1> : Attribute { }

    /// <summary>
    /// Requests a compile-time stunt for <typeparamref name="T"/> that also implements 
    /// the additional interfaces.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class StuntAttribute<T, T1, T2> : Attribute { }

    /// <summary>
    /// Requests a compile-time stunt for <typeparamref name="T"/> that also implements 
    /// the additional interfaces.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class StuntAttribute<T, T1, T2, T3> : Attribute { }

    /// <summary>
    /// Requests a compile-time stunt for <typeparamref name="T"/> that also implements 
    /// the additional interfaces.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class StuntAttribute<T, T1, T2, T3, T4> : Attribute { }

    /// <summary>
    /// Requests a compile-time stunt for <typeparamref name="T"/> that also implements 
    /// the additional interfaces.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class StuntAttribute<T, T1, T2, T3, T4, T5> : Attribute { }

    /// <summary>
    /// Requests a compile-time stunt for <typeparamref name="T"/> that also implements 
    /// the additional interfaces.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class StuntAttribute<T, T1, T2, T3, T4, T5, T6> : Attribute { }

    /// <summary>
    /// Requests a compile-time stunt for <typeparamref name="T"/> that also implements 
    /// the additional interfaces.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class StuntAttribute<T, T1, T2, T3, T4, T5, T6, T7> : Attribute { }

    /// <summary>
    /// Requests a compile-time stunt for <typeparamref name="T"/> that also implements 
    /// the additional interfaces.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class StuntAttribute<T, T1, T2, T3, T4, T5, T6, T7, T8> : Attribute { }
}
