using System;
using System.Collections.Generic;
using System.Text;
using Sample;
using Xunit;

namespace Stunts.Scenarios.DefaultInterfaceImplementation
{
    public interface IDefault
    {
        void Do();
        int Value => 5;
    }

    public class Test : IRunnable
    {
        public void Run()
        {
            var stunt = Stunt.Of<IDefault>();

            Assert.Equal(5, stunt.Value);
        }
    }
}
