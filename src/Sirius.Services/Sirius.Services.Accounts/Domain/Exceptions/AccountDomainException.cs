using Sirius.Domain.Exceptions;

namespace Sirius.Services.Accounts.Domain.Exceptions
{
    public class AccountDomainException : DomainException
    {
        public AccountDomainException()
        { }

        public AccountDomainException(string message)
            : base(message)
        { }

        public AccountDomainException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }
}
