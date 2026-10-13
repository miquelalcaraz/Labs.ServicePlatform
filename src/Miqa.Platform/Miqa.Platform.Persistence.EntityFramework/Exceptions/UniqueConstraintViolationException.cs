using Microsoft.EntityFrameworkCore;

namespace Miqa.Platform.Persistence.Exceptions
{
    /// <summary>
    /// Thrown when a database unique constraint violation is detected during SaveChanges.
    /// This is a safety net for exceptional cases such as race conditions or business rule violations.
    /// Normal idempotency flow uses proactive checks (ExistsAsync) and should NOT rely on catching this exception.
    /// </summary>
    public class UniqueConstraintViolationException : DbUpdateException
    {
        public UniqueConstraintViolationException() { }
        public UniqueConstraintViolationException(string message) : base(message)
        { }

        public UniqueConstraintViolationException(string message, Exception innerException) : base(message, innerException)
        { }
    }
}
