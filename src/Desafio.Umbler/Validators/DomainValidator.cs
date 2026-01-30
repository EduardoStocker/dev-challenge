using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Desafio.Umbler.Validators
{
    public static class DomainValidator
    {
        private static readonly Regex DomainRegex = new Regex(
            @"^(?:[a-zA-Z0-9](?:[a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,}$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        public static string CleanDomain(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return string.Empty;

            var cleaned = domain.Trim();

            if (cleaned.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned.Substring(7);
            else if (cleaned.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned.Substring(8);

            if (cleaned.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned.Substring(4);

            var slashIndex = cleaned.IndexOf('/');
            if (slashIndex >= 0)
                cleaned = cleaned.Substring(0, slashIndex);

            var queryIndex = cleaned.IndexOf('?');
            if (queryIndex >= 0)
                cleaned = cleaned.Substring(0, queryIndex);

            var anchorIndex = cleaned.IndexOf('#');
            if (anchorIndex >= 0)
                cleaned = cleaned.Substring(0, anchorIndex);

            return cleaned.Trim().ToLower();
        }

        public static ValidationResult Validate(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return ValidationResult.Fail("O domínio não pode ser vazio.");

            var cleanDomain = CleanDomain(domain);

            if (string.IsNullOrWhiteSpace(cleanDomain))
                return ValidationResult.Fail("O domínio informado é inválido.");

            string domainPart = cleanDomain;
            string portPart = null;

            if (cleanDomain.Contains(':'))
            {
                var parts = cleanDomain.Split(':');
                if (parts.Length != 2)
                    return ValidationResult.Fail("Formato de porta inválido.");

                domainPart = parts[0];
                portPart = parts[1];

                // Valida a porta
                if (!int.TryParse(portPart, out int port) || port < 1 || port > 65535)
                    return ValidationResult.Fail("Número de porta inválido.");
            }

            if (domainPart.Length < 4)
                return ValidationResult.Fail("O domínio deve ter no mínimo 4 caracteres.");

            if (cleanDomain.Length > 253)
                return ValidationResult.Fail("O domínio deve ter no máximo 253 caracteres.");

            if (domainPart.Contains(' '))
                return ValidationResult.Fail("O domínio não pode conter espaços.");

            if (!domainPart.Contains('.'))
                return ValidationResult.Fail("O domínio deve conter uma extensão.");

            if (domainPart.StartsWith('.') || domainPart.EndsWith('.'))
                return ValidationResult.Fail("O domínio não pode começar ou terminar com ponto.");

            if (domainPart.Contains(".."))
                return ValidationResult.Fail("O domínio não pode conter pontos consecutivos.");

            var domainParts = domainPart.Split('.');
            var extension = domainParts[^1];

            if (extension.Length < 2)
                return ValidationResult.Fail("Extensão inválida.");

            foreach (var c in domainPart)
            {
                if (!(char.IsLetterOrDigit(c) || c == '-' || c == '.'))
                {
                    return ValidationResult.Fail("O domínio contém caracteres inválidos.");
                }
            }

            return ValidationResult.Success(cleanDomain);
        }
    }

    public class ValidationResult
    {
        public bool IsValid { get; private set; }
        public string ErrorMessage { get; private set; }
        public string CleanedValue { get; private set; }

        private ValidationResult() { }

        public static ValidationResult Success(string cleanedValue)
        {
            return new ValidationResult
            {
                IsValid = true,
                CleanedValue = cleanedValue
            };
        }

        public static ValidationResult Fail(string errorMessage)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = errorMessage
            };
        }
    }
}