using System.Text.Json.Serialization;

namespace AutomaticEnvelopes.Api.Features.Tenants.Sama.CampusPricing;

public record CampusFamilyArguments(
    [property: JsonPropertyName("campusType")] string CampusType,
    [property: JsonPropertyName("isSocio")] bool IsSocio,
    [property: JsonPropertyName("familyDiscountType")] string FamilyDiscountType,
    [property: JsonPropertyName("isAfterMay1st")] bool IsAfterMay1st,
    [property: JsonPropertyName("participants")] List<ParticipantArgs> Participants
);

public record ParticipantArgs(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("campusWeeks")] int CampusWeeks,
    [property: JsonPropertyName("septemberDays")] int SeptemberDays,
    [property: JsonPropertyName("tecnificacioWeeks")] int TecnificacioWeeks,
    [property: JsonPropertyName("menjadorDays")] int MenjadorDays,
    [property: JsonPropertyName("tardaDays")] int TardaDays,
    [property: JsonPropertyName("excursionsCost")] int ExcursionsCost
);

public record CampusFamilyResult(
    [property: JsonPropertyName("familyGrandTotal")] decimal FamilyGrandTotal,
    [property: JsonPropertyName("breakdown")] List<ParticipantPricingResult> Breakdown
);

public record ParticipantPricingResult(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("campusWeeks")] int CampusWeeks,
    [property: JsonPropertyName("septemberDays")] int SeptemberDays,
    [property: JsonPropertyName("tecnificacioWeeks")] int TecnificacioWeeks,
    [property: JsonPropertyName("campusBasePrice")] decimal CampusBasePrice,
    [property: JsonPropertyName("tecnificacioBasePrice")] decimal TecnificacioBasePrice,
    [property: JsonPropertyName("discountApplied")] decimal DiscountApplied,
    [property: JsonPropertyName("servicesCost")] decimal ServicesCost,
    [property: JsonPropertyName("excursionsCost")] decimal ExcursionsCost,
    [property: JsonPropertyName("total")] decimal Total
);