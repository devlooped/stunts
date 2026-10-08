#pragma warning disable IDE0079
#pragma warning disable CS0436
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace Stunts.UnitTests
{
    /// <summary>
    /// Data annotation rules generated for a member run before the rest of the pipeline.
    /// </summary>
    public class DataAnnotationsBehaviorTests : IRunnable
    {
        public void Run()
        {
            RejectsAnInvalidPropertyBeforeTheTarget();
            AcceptsAValidPropertyAndOptionalNull();
            RejectsMethodArguments();
            CompareReadsTheOtherProperty();
            ClosedGenericUsesTheSubstitutedSignature();
            VirtualPropertyOnAClassIsChecked();
            ValidatedFactoryClosesItsReturnType();
        }

        public void RejectsAnInvalidPropertyBeforeTheTarget()
        {
            var store = new Store();
            IAccount account = Create(store);

            var error = Assert.Throws<ValidationException>(() => account.Email = "   ");

            Assert.Contains("Email address", error.Message, System.StringComparison.Ordinal);
            Assert.False(store.Set);
            Assert.Equal("Email", Assert.Single(error.ValidationResult.MemberNames));
        }

        public void AcceptsAValidPropertyAndOptionalNull()
        {
            var store = new Store();
            IAccount account = Create(store);

            account.Email = "ada@example.com";
            account.Code = null;

            Assert.Equal("ada@example.com", store.Values["Email"]);
            Assert.True(store.Set);
            Assert.Contains("Code", store.Values.Keys);
        }

        public void RejectsMethodArguments()
        {
            var store = new Store();
            IAccount account = Create(store);

            Assert.Throws<ValidationException>(() => account.Rename(null, 1));
            Assert.Throws<ValidationException>(() => account.Rename("a", 1));
            Assert.Throws<ValidationException>(() => account.Rename("ada", 11));
            Assert.False(store.Called);

            account.Rename("ada", 2);
            account.Pay("4111111111111111");
            Assert.Throws<ValidationException>(() => account.Label(" "));
            account.Label("ada");

            Assert.Equal("ada", store.Name);
            Assert.True(store.Paid);
            Assert.Throws<ValidationException>(() => account.Pay("4111111111111112"));
        }

        public void CompareReadsTheOtherProperty()
        {
            var store = new Store();
            IAccount account = Create(store);
            account.Password = "secret";

            Assert.Throws<ValidationException>(() => account.Confirm = "other");
            account.Confirm = "secret";

            Assert.Equal("secret", store.Values["Confirm"]);
        }

        public void ClosedGenericUsesTheSubstitutedSignature()
        {
            var store = new Store();
            var stunt = Stunt.For<IBox<string>>();
            stunt.AddBehavior(new DataAnnotationsBehavior());
            stunt.AddBehavior(store.Remember);
            IBox<string> box = stunt.ToObject();

            Assert.Throws<ValidationException>(() => box.Value = null);
            box.Value = "kept";

            Assert.Equal("kept", store.Values["Value"]);
        }

        public void VirtualPropertyOnAClassIsChecked()
        {
            var stunt = Stunt.For<Profile>();
            var set = false;
            stunt.AddBehavior(new DataAnnotationsBehavior());
            stunt.AddBehavior((invocation, next) =>
            {
                if (invocation.MethodBase.Name == "set_Name")
                    set = true;
                return invocation.CreateValueReturn((object?)null);
            });
            Profile profile = stunt.ToObject();

            Assert.Throws<ValidationException>(() => profile.Name = "");
            Assert.False(set);
            profile.Name = "Ada";
            Assert.True(set);
            profile.Name = "Ada";
            Assert.True(set);
        }

        public void ValidatedFactoryClosesItsReturnType()
        {
            var store = new Store();
            var reference = Labeled<string>();
            reference.AddBehavior(new DataAnnotationsBehavior());
            reference.AddBehavior(store.Remember);
            ILabeled<string> labeled = reference.ToObject();

            Assert.Throws<ValidationException>(() => labeled.Label = null);
            Assert.False(store.Set);
            labeled.Label = "kept";
            Assert.Equal("kept", store.Values["Label"]);
        }

        [Validated]
        [StuntGenerator]
        static StuntReference<ILabeled<T>> Labeled<T>() => Stunt.For<ILabeled<T>>();

        static IAccount Create(Store store)
        {
            var stunt = Stunt.For<IAccount>();
            stunt.AddBehavior(new DataAnnotationsBehavior());
            stunt.AddBehavior(store.Remember);
            return stunt.ToObject();
        }

        sealed class Store
        {
            public Dictionary<string, object> Values { get; } = new Dictionary<string, object>();

            public bool Set { get; private set; }

            public bool Called { get; private set; }

            public bool Paid { get; private set; }

            public string Name { get; private set; }

            public IMethodReturn Remember(IMethodInvocation invocation, ExecuteHandler next)
            {
                var name = invocation.MethodBase.Name;
                if (name.StartsWith("set_", System.StringComparison.Ordinal))
                {
                    Values[name.Substring(4)] = invocation.Arguments.GetValue(0);
                    Set = true;
                    return invocation.CreateValueReturn((object?)null);
                }

                if (name.StartsWith("get_", System.StringComparison.Ordinal))
                {
                    Values.TryGetValue(name.Substring(4), out var value);
                    return invocation.CreateValueReturn(value);
                }

                if (name == nameof(IAccount.Rename))
                {
                    Called = true;
                    Name = (string)invocation.Arguments.GetValue(0);
                    return invocation.CreateValueReturn((object?)null);
                }

                if (name == nameof(IAccount.Pay))
                {
                    Paid = true;
                    return invocation.CreateValueReturn((object?)null);
                }

                return invocation.CreateValueReturn((object?)null);
            }
        }
    }

    public interface IAccount
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email address")]
        string Email { get; set; }

        [MaxLength(4)]
        string Code { get; set; }

        [Compare(nameof(Password))]
        string Confirm { get; set; }

        string Password { get; set; }

        void Rename([Required, StringLength(8, MinimumLength = 2)] string name, [Range(1, 10)] int attempt);

        void Pay([CreditCard] string card);

        void Label([CustomValidation(typeof(AccountChecks), nameof(AccountChecks.RejectBlank))] string label);
    }

    public static class AccountChecks
    {
        public static ValidationResult RejectBlank(string value) =>
            string.IsNullOrWhiteSpace(value) ? new ValidationResult("blank") : ValidationResult.Success;
    }

    public interface IBox<T>
    {
        [Required]
        T Value { get; set; }
    }

    public interface ILabeled<T>
    {
        [Required]
        T Label { get; set; }
    }

    public class Profile
    {
        [Required]
        public virtual string Name { get; set; }
    }
}
