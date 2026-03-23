namespace SamaBot.Api.Core.Events;

/// <summary>
/// Represents standard metadata extracted from a user's raw message using an LLM.
/// </summary>
public record MessageAnalyzed(
    string MessageId,
    string PhoneNumber,
    string LanguageCode
);
