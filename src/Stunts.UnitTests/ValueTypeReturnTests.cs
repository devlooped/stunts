using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace Stunts.UnitTests
{
    public class ValueTypeReturnTests
    {
        [Fact]
        public void NullReturnForValueTypeThrowsDescriptiveArgumentNullException()
        {
            var pipeline = new BehaviorPipeline((invocation, next) =>
                invocation.CreateValueReturn((object?)null));
            Func<int> method = GetValue;

            var exception = Assert.Throws<ArgumentNullException>(() =>
            {
                pipeline.Execute<int>(MethodInvocation.Create(this, method.GetMethodInfo()));
            });

            Assert.Equal(nameof(IMethodReturn.ReturnValue), exception.ParamName);
            Assert.Contains(nameof(IMethodReturn.ReturnValue), exception.Message);
            Assert.Contains("int", exception.Message);
        }

        [Fact]
        public void NullReturnForNullableValueTypeIsAllowed()
        {
            var pipeline = new BehaviorPipeline((invocation, next) =>
                invocation.CreateValueReturn((object?)null));
            Func<int?> method = GetNullableValue;

            Assert.Null(pipeline.Execute<int?>(MethodInvocation.Create(this, method.GetMethodInfo())));
        }

        [Fact]
        public void GeneratedProxyRejectsNullValueTypeReturnsAndArguments()
            => new Scenarios().Run(Path.Combine("Scenarios", "ValueTypeNull.cs"));

        static int GetValue() => 0;

        static int? GetNullableValue() => null;
    }
}
