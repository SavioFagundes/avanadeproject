namespace InventoryService.Auth;

public class JwtSettings
{
    public string Issuer { get; set; } = "EcomDemo";
    public string Audience { get; set; } = "EcomClients";
    public string Secret { get; set; } = "super_secret_key_change_me";
}
