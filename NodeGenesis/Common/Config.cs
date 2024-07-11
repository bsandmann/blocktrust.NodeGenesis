namespace NodeGenesis.Common;

public class Config
{
    public string IndyscanBasePath { get; set; }
    public string IndyscanDaemonImage { get; set; } 
    public string IndyscanApiImage { get; set; } 
    public string IndyscanWebappImage { get; set; } 
    public string IndyscanDaemonUiImage { get; set; } 
    public string ElasticSearchImage { get; set; } 
    public List<Network> Networks { get; set; }
}