using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace Stunts.UnitTests
{
    public class DataAnnotationsValidatorTests
    {
        [Fact]
        public void RequiredMatchesTheAttribute()
        {
            var attribute = new RequiredAttribute();
            Assert.Equal(attribute.FormatErrorMessage("Email"), Fail(DataAnnotationsValidator.Required(null, "Email", null, false, null)));
            Assert.Equal(attribute.FormatErrorMessage("Email"), Fail(DataAnnotationsValidator.Required("   ", "Email", null, false, null)));
            Assert.Null(DataAnnotationsValidator.Required("   ", "Email", null, true, null));
            Assert.Null(DataAnnotationsValidator.Required("ada@example.com", "Email", null, false, null));
            Assert.Equal(new RequiredAttribute { ErrorMessage = "Need {0}" }.FormatErrorMessage("Email"), Fail(DataAnnotationsValidator.Required(null, "Email", null, false, "Need {0}")));
        }

        [Fact]
        public void StringLengthMatchesTheAttribute()
        {
            var attribute = new StringLengthAttribute(4) { MinimumLength = 2 };
            Assert.Null(DataAnnotationsValidator.StringLength(null, 2, 4, "Name", null, null));
            Assert.Null(DataAnnotationsValidator.StringLength("abcd", 2, 4, "Name", null, null));
            Assert.Equal(attribute.FormatErrorMessage("Name"), Fail(DataAnnotationsValidator.StringLength("a", 2, 4, "Name", null, null)));
            Assert.Equal(attribute.FormatErrorMessage("Name"), Fail(DataAnnotationsValidator.StringLength("abcde", 2, 4, "Name", null, null)));
            Assert.Throws<InvalidCastException>(() => DataAnnotationsValidator.StringLength(1, 0, 4, "Name", null, null));
        }

        [Fact]
        public void LengthAttributesMatchCollectionsAndStrings()
        {
            Assert.Null(DataAnnotationsValidator.MinLength(null, 1, "Tags", null, null));
            Assert.Null(DataAnnotationsValidator.MinLength("ab", 2, "Tags", null, null));
            Assert.NotNull(DataAnnotationsValidator.MinLength(new[] { 1 }, 2, "Tags", null, null));
            Assert.Null(DataAnnotationsValidator.MaxLength(new List<int> { 1, 2 }, 2, "Tags", null, null));
            Assert.Null(DataAnnotationsValidator.MaxLength("abcdef", -1, "Tags", null, null));
            Assert.NotNull(DataAnnotationsValidator.Length("abcd", 2, 3, "Tags", null, null));
            Assert.Null(DataAnnotationsValidator.Length(new[] { 1, 2 }, 1, 2, "Tags", null, null));
            Assert.Equal(
                new MinLengthAttribute(2).FormatErrorMessage("Tags"),
                Fail(DataAnnotationsValidator.MinLength("", 2, "Tags", null, null)));
            Assert.Throws<InvalidCastException>(() => DataAnnotationsValidator.MaxLength(1, 2, "Tags", null, null));
            Assert.Null(DataAnnotationsValidator.MaxLength(new Bag(1), 2, "Tags", null, null, static value => ((Bag)value).Count));
        }

        [Fact]
        public void RangeMatchesTheAttribute()
        {
            var attribute = new RangeAttribute(1, 10) { MinimumIsExclusive = true };
            Assert.Null(DataAnnotationsValidator.Range(null, 1, 10, true, false, "Age", null, null));
            Assert.Null(DataAnnotationsValidator.Range("", 1, 10, false, false, "Age", null, null));
            Assert.Null(DataAnnotationsValidator.Range(2, 1, 10, true, false, "Age", null, null));
            Assert.Equal(attribute.FormatErrorMessage("Age"), Fail(DataAnnotationsValidator.Range(1, 1, 10, true, false, "Age", null, null)));
            Assert.Null(DataAnnotationsValidator.Range("4", 1, 10, false, false, "Age", null, null));
            Assert.NotNull(DataAnnotationsValidator.Range("nope", 1, 10, false, false, "Age", null, null));

            var dates = new RangeAttribute(typeof(DateTime), "2020-01-01", "2020-01-03") { ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true };
            var parsed = DataAnnotationsValidator.Range(
                "2020-01-02",
                "2020-01-01",
                "2020-01-03",
                static (string text, IFormatProvider provider, out DateTime result) => DateTime.TryParse(text, provider, DateTimeStyles.None, out result),
                true,
                true,
                false,
                false,
                "When",
                null,
                null);
            Assert.Null(parsed);
            Assert.Equal(dates.FormatErrorMessage("When"), Fail(DataAnnotationsValidator.Range(
                "2020-02-01",
                "2020-01-01",
                "2020-01-03",
                static (string text, IFormatProvider provider, out DateTime result) => DateTime.TryParse(text, provider, DateTimeStyles.None, out result),
                true,
                true,
                false,
                false,
                "When",
                null,
                null)));
        }

        [Fact]
        public void PatternRequiresAFullMatch()
        {
            var pattern = new Regex("^a+$", RegexOptions.None, TimeSpan.FromSeconds(1));
            Assert.Null(DataAnnotationsValidator.Pattern(null, pattern, "^a+$", "Code", null, null));
            Assert.Null(DataAnnotationsValidator.Pattern("", pattern, "^a+$", "Code", null, null));
            Assert.Null(DataAnnotationsValidator.Pattern("aaa", pattern, "^a+$", "Code", null, null));
            Assert.Equal(
                new RegularExpressionAttribute("^a+$").FormatErrorMessage("Code"),
                Fail(DataAnnotationsValidator.Pattern("ab", pattern, "^a+$", "Code", null, null)));
        }

        [Fact]
        public void BuiltInFormatsMatchTheAttributes()
        {
            Assert.Equal(new EmailAddressAttribute().IsValid("a@b.co"), DataAnnotationsValidator.EmailAddress("a@b.co", "Email", null, null) == null);
            Assert.Equal(new EmailAddressAttribute().IsValid("a@b.co\n"), DataAnnotationsValidator.EmailAddress("a@b.co\n", "Email", null, null) == null);
            Assert.Equal(new EmailAddressAttribute().IsValid("ab.co"), DataAnnotationsValidator.EmailAddress("ab.co", "Email", null, null) == null);
            Assert.Equal(new PhoneAttribute().IsValid("425-555-0100 x12"), DataAnnotationsValidator.Phone("425-555-0100 x12", "Phone", null, null) == null);
            Assert.Equal(new PhoneAttribute().IsValid("nope"), DataAnnotationsValidator.Phone("nope", "Phone", null, null) == null);
            Assert.Equal(new CreditCardAttribute().IsValid("4111111111111111"), DataAnnotationsValidator.CreditCard("4111111111111111", "Card", null, null) == null);
            Assert.Equal(new CreditCardAttribute().IsValid("4111-1111-1111-1112"), DataAnnotationsValidator.CreditCard("4111-1111-1111-1112", "Card", null, null) == null);
            Assert.Equal(new UrlAttribute().IsValid("https://example.com"), DataAnnotationsValidator.Url("https://example.com", "Site", null, null) == null);
            Assert.Equal(new UrlAttribute().IsValid("mailto:a@b.co"), DataAnnotationsValidator.Url("mailto:a@b.co", "Site", null, null) == null);
            Assert.Equal(new UrlAttribute().IsValid(new Uri("ftp://files.example.com")), DataAnnotationsValidator.Url(new Uri("ftp://files.example.com"), "Site", null, null) == null);
            Assert.Equal(new Base64StringAttribute().IsValid("YQ=="), DataAnnotationsValidator.Base64String("YQ==", "Token", null, null) == null);
            Assert.Equal(new Base64StringAttribute().IsValid("@@@@"), DataAnnotationsValidator.Base64String("@@@@", "Token", null, null) == null);
            Assert.Equal(new FileExtensionsAttribute().IsValid("photo.PNG"), DataAnnotationsValidator.FileExtensions("photo.PNG", null, "File", null, null) == null);
            Assert.Equal(new FileExtensionsAttribute { Extensions = "pdf" }.IsValid("notes.pdf"), DataAnnotationsValidator.FileExtensions("notes.pdf", "pdf", "File", null, null) == null);
            Assert.Equal(new FileExtensionsAttribute().IsValid("notes.pdf"), DataAnnotationsValidator.FileExtensions("notes.pdf", null, "File", null, null) == null);
        }

        [Fact]
        public void EnumAllowedDeniedAndCompare()
        {
            var defined = new EnumDataTypeAttribute(typeof(DayOfWeek));
            Assert.Equal(defined.IsValid(null), DataAnnotationsValidator.Enum<DayOfWeek>(null, false, "Day", null, null) == null);
            Assert.Equal(defined.IsValid(""), DataAnnotationsValidator.Enum<DayOfWeek>("", false, "Day", null, null) == null);
            Assert.Equal(defined.IsValid(DayOfWeek.Monday), DataAnnotationsValidator.Enum<DayOfWeek>(DayOfWeek.Monday, false, "Day", null, null) == null);
            Assert.Equal(defined.IsValid(9), DataAnnotationsValidator.Enum<DayOfWeek>(9, false, "Day", null, null) == null);
            Assert.Equal(defined.IsValid("Monday"), DataAnnotationsValidator.Enum<DayOfWeek>("Monday", false, "Day", null, null) == null);

            var flags = new EnumDataTypeAttribute(typeof(ConsoleModifiers));
            Assert.Equal(flags.IsValid(ConsoleModifiers.Alt | ConsoleModifiers.Shift), DataAnnotationsValidator.Enum<ConsoleModifiers>(ConsoleModifiers.Alt | ConsoleModifiers.Shift, true, "Mods", null, null) == null);
            Assert.Equal(flags.IsValid((ConsoleModifiers)99), DataAnnotationsValidator.Enum<ConsoleModifiers>((ConsoleModifiers)99, true, "Mods", null, null) == null);

            Assert.Null(DataAnnotationsValidator.Allowed("open", "Status", null, null, "open", "closed"));
            Assert.Null(DataAnnotationsValidator.Allowed(null, "Status", null, null, "open", null));
            Assert.NotNull(DataAnnotationsValidator.Allowed(null, "Status", null, null, "open"));
            Assert.NotNull(DataAnnotationsValidator.Denied("no", "Status", null, null, "no"));
            Assert.Null(DataAnnotationsValidator.Denied(null, "Status", null, null, "no"));
            Assert.Null(DataAnnotationsValidator.Compare("a", "a", "Confirm", null, "Password", null));
            Assert.Equal(
                new CompareAttribute("Password").FormatErrorMessage("Confirm"),
                Fail(DataAnnotationsValidator.Compare("a", "b", "Confirm", null, "Password", null)));
        }

        [Fact]
        public void CustomResultUsesTheMethodMessage()
        {
            Assert.Null(DataAnnotationsValidator.CustomResult(ValidationResult.Success, "Name", null, null));
            Assert.Equal("Name is not valid.", Fail(DataAnnotationsValidator.CustomResult(new ValidationResult(null), "Name", null, null)));
            Assert.Equal("Bad Name", Fail(DataAnnotationsValidator.CustomResult(new ValidationResult("Bad {0}"), "Name", null, null)));
            Assert.Contains("string", DataAnnotationsValidator.ConversionFailed(1, "string", "Checks", "Name", "Name").ErrorMessage, StringComparison.Ordinal);
        }

        [Fact]
        public void SignatureMatchesRuntimeTypes()
        {
            Assert.Equal("System.Int32", DataAnnotationsSignature.Format(typeof(int)));
            Assert.Equal("System.Int32&", DataAnnotationsSignature.Format(typeof(int).MakeByRefType()));
            Assert.Equal("System.Int32[]", DataAnnotationsSignature.Format(typeof(int[])));
            Assert.Equal("System.Int32[,]", DataAnnotationsSignature.Format(typeof(int[,])));
            Assert.Equal("System.Int32[][]", DataAnnotationsSignature.Format(typeof(int[][])));
            Assert.Equal("System.Nullable<System.Int32>", DataAnnotationsSignature.Format(typeof(int?)));
            Assert.Equal("System.Collections.Generic.Dictionary<System.String,System.Int32>", DataAnnotationsSignature.Format(typeof(Dictionary<string, int>)));
            Assert.Equal("Stunts.UnitTests.DataAnnotationsValidatorTests.Outer<System.Int32>.Inner<System.String>", DataAnnotationsSignature.Format(typeof(Outer<int>.Inner<string>)));

            var method = typeof(DataAnnotationsValidatorTests).GetMethod(nameof(Generic)).MakeGenericMethod(typeof(int));
            Assert.Equal("!!0,System.Int32", DataAnnotationsSignature.Format(method));
            Assert.Equal("!!0", DataAnnotationsSignature.MethodTypeParameter(0));
            Assert.Equal("System.Int32,System.String", DataAnnotationsSignature.Join(DataAnnotationsSignature.Format(typeof(int)), DataAnnotationsSignature.Format(typeof(string))));
        }

        public void Generic<T>(T value, int id)
        {
        }

        static string Fail(ValidationResult result) => result.ErrorMessage;

        public class Outer<T>
        {
            public class Inner<U>
            {
            }
        }

        sealed class Bag : IEnumerable
        {
            public Bag(int count) => Count = count;

            public int Count { get; }

            public IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
        }
    }
}
