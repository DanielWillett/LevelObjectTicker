using System.Xml.Serialization;

namespace LevelObjectTicker;
public sealed class LevelObjectTickerConfiguration : IRocketPluginConfiguration
{
    [XmlElement("tickSpeedSeconds")]
    public float TickSpeed { get; set; }

    [XmlElement("resetTimeLimitSeconds")]
    public float ResetTimeLimit { get; set; }

    [XmlElement("disabled")]
    public bool Disabled { get; set; }

    [XmlElement("debugLogging")]
    public bool DebugLogging { get; set; }

    [XmlArray("objectBlacklist")]
    [XmlArrayItem("object")]
    public Guid[]? ObjectBlacklist { get; set; }

    [XmlArray("objectWhitelist")]
    [XmlArrayItem("object")]
    public Guid[]? ObjectWhitelist { get; set; }
    void IDefaultable.LoadDefaults()
    {
        TickSpeed = 0.1f;
        ResetTimeLimit = 60f;
        Disabled = false;
        ObjectBlacklist = new Guid[] { Guid.Empty };
        ObjectWhitelist = new Guid[] { Guid.Empty };
        DebugLogging = false;
    }
}
