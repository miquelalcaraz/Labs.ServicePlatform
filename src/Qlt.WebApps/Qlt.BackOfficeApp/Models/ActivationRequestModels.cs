using System.ComponentModel.DataAnnotations;

namespace Qlt.BackOfficeApp.Models;

public class RetryActivationRequest
{
    [MaxLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
    public string? Reason { get; set; }
}

public class ResendActivationRequest
{
}
