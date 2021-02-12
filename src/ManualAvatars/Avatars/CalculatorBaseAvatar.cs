//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

#nullable enable
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8605, CS8618, CS8625, CS8765
using Avatars;
using Sample;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Avatars.Sample
{
    [CompilerGenerated]
    partial class CalculatorBaseAvatar : CalculatorBase, IAvatar
    {
        readonly BehaviorPipeline pipeline = BehaviorPipelineFactory.Default.CreatePipeline<CalculatorBaseAvatar>();

        [CompilerGenerated]
        public CalculatorBaseAvatar() => pipeline.Execute(MethodInvocation.Create(this, MethodBase.GetCurrentMethod(), (m, n) => m.CreateReturn()));

        [CompilerGenerated]
        IList<IAvatarBehavior> IAvatar.Behaviors => pipeline.Behaviors;

        [CompilerGenerated]
        public override int? this[string name]
        {
            get => pipeline.Execute<int?>(MethodInvocation.Create(this, MethodBase.GetCurrentMethod(), name));
            set => pipeline.Execute(MethodInvocation.Create(this, MethodBase.GetCurrentMethod(), name, value));
        }

        [CompilerGenerated]
        public override bool IsOn => pipeline.Execute<bool>(MethodInvocation.Create(this, MethodBase.GetCurrentMethod()));

        [CompilerGenerated]
        public override CalculatorMode Mode
        {
            get => pipeline.Execute<CalculatorMode>(MethodInvocation.Create(this, MethodBase.GetCurrentMethod()));
            set => pipeline.Execute(MethodInvocation.Create(this, MethodBase.GetCurrentMethod(), value));
        }

        [CompilerGenerated]
        public override ICalculatorMemory Memory => pipeline.Execute<ICalculatorMemory>(MethodInvocation.Create(this, MethodBase.GetCurrentMethod()));

        [CompilerGenerated]
        public override int Add(int x, int y) => pipeline.Execute<int>(MethodInvocation.Create(this, MethodBase.GetCurrentMethod(), x, y));

        [CompilerGenerated]
        public override int Add(int x, int y, int z) => pipeline.Execute<int>(MethodInvocation.Create(this, MethodBase.GetCurrentMethod(), x, y, z));

        [CompilerGenerated]
        public override void Clear(string name) => pipeline.Execute(MethodInvocation.Create(this, MethodBase.GetCurrentMethod(), name));

        [CompilerGenerated]
        public override bool Equals(object obj) => pipeline.Execute<bool>(MethodInvocation.Create(this, MethodBase.GetCurrentMethod(), (m, n) => m.CreateValueReturn(base.Equals(obj)), obj));

        [CompilerGenerated]
        public override int GetHashCode() => pipeline.Execute<int>(MethodInvocation.Create(this, MethodBase.GetCurrentMethod(), (m, n) => m.CreateValueReturn(base.GetHashCode())));

        [CompilerGenerated]
        public override int? Recall(string name) => pipeline.Execute<int?>(MethodInvocation.Create(this, MethodBase.GetCurrentMethod(), name));

        [CompilerGenerated]
        public override void Store(string name, int value) => pipeline.Execute(MethodInvocation.Create(this, MethodBase.GetCurrentMethod(), name, value));

        [CompilerGenerated]
        public override string ToString() => pipeline.Execute<string>(MethodInvocation.Create(this, MethodBase.GetCurrentMethod(), (m, n) => m.CreateValueReturn(base.ToString())));

        [CompilerGenerated]
        public override bool TryAdd(ref int x, ref int y, out int? z)
        {
            var _method = MethodBase.GetCurrentMethod();
            z = default;
            var _result = pipeline.Invoke(MethodInvocation.Create(this, _method, x, y, z));
            x = _result.Outputs.Get<int>("x");
            y = _result.Outputs.Get<int>("y");
            z = _result.Outputs.GetNullable<int?>("z");
            return (bool)_result.ReturnValue!;
        }

        [CompilerGenerated]
        public override void TurnOn() => pipeline.Execute(MethodInvocation.Create(this, MethodBase.GetCurrentMethod()));

        [CompilerGenerated]
        public override event EventHandler TurnedOn
        {
            add => pipeline.Execute(MethodInvocation.Create(this, MethodBase.GetCurrentMethod(), (m, n) =>
            {
                base.TurnedOn += value;
                return m.CreateReturn();
            }, value));
            remove => pipeline.Execute(MethodInvocation.Create(this, MethodBase.GetCurrentMethod(), (m, n) =>
            {
                base.TurnedOn -= value;
                return m.CreateReturn();
            }, value));
        }
    }
}