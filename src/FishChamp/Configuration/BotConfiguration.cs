namespace FishChamp.Configuration;

public class DiscordConfiguration
{
    public string Token { get; set; } = string.Empty;
}

public class DatabaseConfiguration
{
    public string ConnectionString { get; set; } = string.Empty;
    public string Type { get; set; } = "Json";
}