namespace TelegrammPublishGV;

public class RedisSettings
{
    public const string SectionName = "RedisSettings";

    public string ConnectionString { get; set; } = string.Empty;
}
