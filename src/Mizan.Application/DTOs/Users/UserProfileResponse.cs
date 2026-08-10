using System.Text.Json.Serialization;

namespace Mizan.Application.DTOs.Users;

public class UserProfileResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("lastName")]
    public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("whatsappNumber")]
    public string WhatsAppNumber { get; set; } = string.Empty;

    [JsonPropertyName("userType")]
    public string UserType { get; set; } = "customer";

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("shop")]
    public ShopDto? Shop { get; set; }
}

public class ShopDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("shopName")]
    public string ShopName { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}
