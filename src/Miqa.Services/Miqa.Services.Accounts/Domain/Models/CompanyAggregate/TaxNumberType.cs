using System.Text.Json.Serialization;

namespace Miqa.Services.Accounts.Domain.Models.CompanyAggregate;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaxNumberType
{
    Unknown = 0,
    // International / generic
    TaxId,
    Vat,

    // United States
    Ein,
    Tin,
    Ssn,

    // Europe
    Nif,
    Cif,
    UstIdNr,

    // United Kingdom
    Utr,

    // Canada
    Bn,

    // Australia / New Zealand
    Abn,
    Tfn,
    Ird,

    // Asia-Pacific
    Gst,
    Pan,

    // Latin America
    Cnpj,
    Cpf,
    Rfc,
    Nit
}
