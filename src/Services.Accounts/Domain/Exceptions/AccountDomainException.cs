using QltS.Core.Domain.Exceptions;

namespace QltS.Accounts.Domain.Exceptions
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
