namespace Stunts
{
    /// <summary>
    /// Invokes one member on <paramref name="target"/> using the arguments of <paramref name="invocation"/>.
    /// </summary>
    /// <param name="target">The instance that receives the call. The stunt runs its base body, or throws when the member has none.</param>
    /// <param name="invocation">The invocation whose arguments are used.</param>
    /// <returns>The result of the call, including updated <c>ref</c> and <c>out</c> arguments.</returns>
    public delegate IMethodReturn MethodInvoker(object target, IMethodInvocation invocation);
}
