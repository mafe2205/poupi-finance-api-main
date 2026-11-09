using AppFinancesClassLibrary.Domain.Exceptions;
using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;

namespace AppFinancesClassLibrary.Domain
{
    public static class Validate
    {
        private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
        private static readonly Regex PhoneRegex = new(@"^\+?[0-9]{8,15}$", RegexOptions.Compiled);

        public static void NotNull(object value, string fieldName)
        {
            if (value is null) throw new DomainValidationException($"{fieldName} é obrigatório.");
        }

        public static void String(string value, string fieldName, int minLength = 1, int? maxLength = null)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new DomainValidationException($"{fieldName} é obrigatório.");

            if (value.Length < minLength) throw new DomainValidationException($"{fieldName} precisa de pelo menos {minLength} caracteres.");

            if (maxLength is not null && value.Length > maxLength) throw new DomainValidationException($"{fieldName} precisa de no máximo {maxLength} caracteres.");
        }

        public static void Email(string value, string fieldName = "Email")
        {
            String(value, fieldName);

            if (!EmailRegex.IsMatch(value!)) throw new DomainValidationException($"{fieldName} não é um endereço de email válido.");
        }

        public static void Phone(string value, string fieldName = "Phone")
        {
            String(value, fieldName);

            if (!PhoneRegex.IsMatch(value!)) throw new DomainValidationException($"{fieldName} não é um número de telefone válido.");
        }

        public static void Id(long value, string fieldName)
        {
            if (value <= 0) throw new DomainValidationException($"{fieldName} é obrigatório.");
        }

        public static void Int(int value, string fieldName, int? min = null, int? max = null)
        {
            if (min is not null && value < min) throw new DomainValidationException($"{fieldName} precisa ser maior ou igual a {min}.");

            if (max is not null && value > max) throw new DomainValidationException($"{fieldName} precisa ser menor ou igual a {max}.");
        }

        public static void Decimal(decimal value, string fieldName, decimal? min = null, decimal? max = null)
        {
            if (min is not null && value < min) throw new DomainValidationException($"{fieldName} precisa ser maior ou igual a {min}.");

            if (max is not null && value > max) throw new DomainValidationException($"{fieldName} precisa ser menor ou igual a {max}.");
        }

        public static void Bool(bool value, string fieldName, bool expected)
        {
            if (value != expected) throw new DomainValidationException($"{fieldName} precisa ser {(expected ? "true" : "false")}.");
        }

        public static void DateOnly(DateOnly value, string fieldName, DateOnly? min = null, DateOnly? max = null)
        {
            if (value == default) throw new DomainValidationException($"{fieldName} é obrigatório.");

            if (min is not null && value < min) throw new DomainValidationException($"{fieldName} não pode ser antes de {min:yyyy-MM-dd}.");

            if (max is not null && value > max) throw new DomainValidationException($"{fieldName} não pode ser depois de {max:yyyy-MM-dd}.");
        }

        public static void DateTime(DateTime value, string fieldName, DateTime? min = null, DateTime? max = null)
        {
            if (value == default) throw new DomainValidationException($"{fieldName} é obrigatório.");

            if (min is not null && value < min) throw new DomainValidationException($"{fieldName} não pode ser antes de {min}.");

            if (max is not null && value > max) throw new DomainValidationException($"{fieldName} não pode ser depois de {max}.");
        }

        public static void Enum<TEnum>(TEnum value, string fieldName) where TEnum : struct, Enum
        {
            if (!System.Enum.IsDefined(value)) throw new DomainValidationException($"{fieldName} não é um valor válido.");
        }

        public static void Object(object instance, string fieldName = "Object")
        {
            if (instance is null) throw new DomainValidationException($"{fieldName} é obrigatório.");

            foreach (var property in instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0) continue;

                var value = property.GetValue(instance);
                var isRequired = property.GetCustomAttribute<RequiredAttribute>() is not null;

                Property(value, $"{fieldName}.{property.Name}", isRequired);
            }
        }

        private static void Property(object value, string fieldName, bool isRequired)
        {
            if (value is null)
            {
                if (isRequired) throw new DomainValidationException($"{fieldName} é obrigatório.");
                return;
            }

            switch (value)
            {
                case string s:
                    if (fieldName.Contains("Email", StringComparison.OrdinalIgnoreCase))
                    {
                        if (isRequired || s.Length > 0) Email(s, fieldName);
                    }
                    else if (fieldName.Contains("Phone", StringComparison.OrdinalIgnoreCase))
                    {
                        if (isRequired || s.Length > 0) Phone(s, fieldName);
                    }
                    else if (isRequired)
                    {
                        String(s, fieldName);
                    }
                    break;

                case long g:
                    if (isRequired) Id(g, fieldName);
                    break;

                case DateOnly d:
                    if (isRequired) DateOnly(d, fieldName);
                    break;

                case DateTime dt:
                    if (isRequired) DateTime(dt, fieldName);
                    break;

                case Enum e:
                    if (!System.Enum.IsDefined(e.GetType(), e)) throw new DomainValidationException($"{fieldName} não é um valor válido.");
                    break;

                case bool or int or long or short or decimal or double or float or byte:
                    break;

                case IEnumerable enumerable:
                    var index = 0;
                    foreach (var item in enumerable)
                    {
                        if (item is not null && IsComplex(item.GetType())) Object(item, $"{fieldName}[{index}]");
                        index++;
                    }
                    break;

                default:
                    if (IsComplex(value.GetType())) Object(value, fieldName);
                    break;
            }
        }

        private static bool IsComplex(Type type)
        {
            return type.IsClass && type != typeof(string) && !type.IsEnum && !type.IsArray && !typeof(IEnumerable).IsAssignableFrom(type);
        }
    }
}
