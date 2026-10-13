using System.ComponentModel.DataAnnotations;

namespace Miqa.BackOfficeApp.Models;

public class StartOnboardingRequest
{
    public CompanyInput Company { get; set; } = new();
    public SubscriptionInput Subscription { get; set; } = new();
    public AdminUserInput AdminUser { get; set; } = new();
}

public class CompanyInput
{
    [Required(ErrorMessage = "Legal name is required.")]
    [MaxLength(250, ErrorMessage = "Legal name cannot exceed 250 characters.")]
    public string LegalName { get; set; } = string.Empty;

    [MaxLength(250, ErrorMessage = "Trade name cannot exceed 250 characters.")]
    public string? TradeName { get; set; }

    [Required(ErrorMessage = "Tax ID is required.")]
    [MaxLength(50, ErrorMessage = "Tax ID cannot exceed 50 characters.")]
    public string? TaxId { get; set; }

    [Required(ErrorMessage = "Country code is required.")]
    [MaxLength(10, ErrorMessage = "Country code cannot exceed 10 characters.")]
    public string? CountryCode { get; set; }
}

public class SubscriptionInput
{
    [Required(ErrorMessage = "Subscription plan code is required.")]
    [MaxLength(100, ErrorMessage = "Subscription plan code cannot exceed 100 characters.")]
    public string SubscriptionPlanCode { get; set; } = string.Empty;
}

public class AdminUserInput
{
    [Required(ErrorMessage = "First name is required.")]
    [MaxLength(100, ErrorMessage = "First name cannot exceed 100 characters.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required.")]
    [MaxLength(150, ErrorMessage = "Last name cannot exceed 150 characters.")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email format is invalid.")]
    public string Email { get; set; } = string.Empty;

    [MaxLength(10, ErrorMessage = "Preferred language cannot exceed 10 characters.")]
    public string? PreferredLanguage { get; set; }
}

public class RetryOnboardingRequest
{
    [MaxLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
    public string? Reason { get; set; }
}
