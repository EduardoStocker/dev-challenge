using System;

namespace Desafio.Umbler.Exceptions
{
    public class DomainValidationException : Exception
    {
        public DomainValidationException(string message) : base(message) { }
    }

    public class DomainNotFoundException : Exception
    {
        public DomainNotFoundException(string message) : base(message) { }
    }

    public class ExternalServiceException : Exception
    {
        public ExternalServiceException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}