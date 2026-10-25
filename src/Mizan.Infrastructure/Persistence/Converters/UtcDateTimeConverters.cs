using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mizan.Infrastructure.Persistence.Converters;

/// <summary>
/// Global converter for non-nullable DateTime properties.
/// SQL Server datetime2 does not store DateTime.Kind, so values read back are always
/// DateTimeKind.Unspecified. This converter:
///   - Write (model -> provider): converts to UTC only when Kind is Local; leaves Utc/Unspecified untouched
///     so stored values are never shifted.
///   - Read (provider -> model): stamps the value as DateTimeKind.Utc so System.Text.Json serializes it
///     with a trailing "Z".
/// EF Core never passes null to a value converter, so this class is only ever invoked with real values.
/// </summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            modelValue => modelValue.Kind == DateTimeKind.Local
                ? modelValue.ToUniversalTime()
                : modelValue,
            providerValue => DateTime.SpecifyKind(providerValue, DateTimeKind.Utc))
    {
    }
}

/// <summary>
/// Global converter for nullable DateTime properties (e.g. RevokedAt, PaidAt, LastAttemptAt).
/// Same behaviour as <see cref="UtcDateTimeConverter"/>, but typed for DateTime? because EF Core's
/// HaveConversion&lt;TConverter&gt; requires a ValueConverter&lt;TProperty, TProperty&gt; matching the
/// exact property CLR type. EF Core never passes null to a converter, so the null guards are defensive only.
/// </summary>
public sealed class UtcNullableDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public UtcNullableDateTimeConverter()
        : base(
            modelValue => modelValue.HasValue
                ? (modelValue.Value.Kind == DateTimeKind.Local
                    ? modelValue.Value.ToUniversalTime()
                    : modelValue)
                : modelValue,
            providerValue => providerValue.HasValue
                ? DateTime.SpecifyKind(providerValue.Value, DateTimeKind.Utc)
                : providerValue)
    {
    }
}
