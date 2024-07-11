namespace NodeGenesis.Common;

public class Network
{
    public string NameAndFixedId { get; set; }
    public List<string>? AliasIds { get; set; }
    public string DisplayNameShort { get; set; }
    public string DisplayNameLong { get; set; }
    public string DisplayDescription { get; set; }
    public string ElasticSearchIndex { get; set; }
    public bool Add { get; set; }
    public string SeralizationSpeedSlowMediumFast { get; set; }
    public string ExpansionSpeedSlowMediumFast { get; set; }
}