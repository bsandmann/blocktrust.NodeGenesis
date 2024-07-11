using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NodeGenesis.Common;

class Program
{
    public static Config Config { get; set; }

    static void Main(string[] args)
    {
        try
        {
            string projectDirectory = GetProjectDirectory();
            string configPath = Path.Combine(projectDirectory, "config.json");

            if (!File.Exists(configPath))
            {
                Console.WriteLine($"config.json file not found in {configPath}");
                return;
            }

            string jsonString = File.ReadAllText(configPath);
            Config = JsonSerializer.Deserialize<Config>(jsonString);

            if (Config == null)
            {
                Console.WriteLine("Failed to deserialize config.json");
                return;
            }

            Console.WriteLine("Configuration loaded successfully.");

            // Resolve IndyscanBasePath
            Config.IndyscanBasePath = ResolvePath(Config.IndyscanBasePath);
            Console.WriteLine($"Resolved IndyscanBasePath: {Config.IndyscanBasePath}");

            // Create docker-compose.es.yml
            CreateDockerComposeEs(projectDirectory, Config);
            // Create docker-compose.yml
            CreateDockerCompose(projectDirectory, Config);
            // Generate allNetworks.json
            GenerateAllNetworksJson(Config);
            // Generate network-specific configuration files
            GenerateNetworkConfigs(Config);
            // Copy Genesis files
            CopyGenesisFiles(Config);

            
            Console.WriteLine("If all operations were successful indysync should now be ablw to run:");
            Console.WriteLine("sudo docker compose -f docker-compose.es.yml -f docker-compose.yml up -d --build");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    static string GetProjectDirectory()
    {
        string currentDirectory = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(currentDirectory, "NodeGenesis.csproj")))
        {
            currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
            if (currentDirectory == null)
            {
                throw new DirectoryNotFoundException("Could not find the project directory.");
            }
        }
        return currentDirectory;
    }

    static string ResolvePath(string path)
    {
        if (path.StartsWith("~"))
        {
            string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(homeDirectory, path.Substring(2));
        }
        return Path.GetFullPath(path);
    }

    static void CreateDockerComposeEs(string projectDirectory, Config config)
    {
        try
        {
            string templatePath = Path.Combine(projectDirectory, "Resources", "ComposeTemplates", "DockerComposeElasticTemplate.yml");
            string outputPath = Path.Combine(config.IndyscanBasePath, "start", "docker-compose.es.yml");

            Console.WriteLine($"Template path: {templatePath}");
            Console.WriteLine($"Output path: {outputPath}");

            // Ensure the Output directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException($"Template file not found: {templatePath}");
            }

            string templateContent = File.ReadAllText(templatePath);
            string outputContent = templateContent.Replace("__ElasticSearchImage__", config.ElasticSearchImage);

            File.WriteAllText(outputPath, outputContent);

            Console.WriteLine($"Created docker-compose.es.yml at {outputPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in CreateDockerComposeEs: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    static void CreateDockerCompose(string projectDirectory, Config config)
    {
        try
        {
            string templatePath = Path.Combine(projectDirectory, "Resources", "ComposeTemplates", "DockerComposeTemplate.yml");
            string outputPath = Path.Combine(config.IndyscanBasePath, "start", "docker-compose.yml");

            Console.WriteLine($"Template path: {templatePath}");
            Console.WriteLine($"Output path: {outputPath}");

            // Ensure the Output directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException($"Template file not found: {templatePath}");
            }

            string templateContent = File.ReadAllText(templatePath);
            string outputContent = templateContent.Replace("__IndyscanBasePath__", config.IndyscanBasePath);
            outputContent = outputContent.Replace("__IndyscanDaemonImage__", config.IndyscanDaemonImage);
            outputContent = outputContent.Replace("__IndyscanApiImage__", config.IndyscanApiImage);
            outputContent = outputContent.Replace("__IndyscanWebappImage__", config.IndyscanWebappImage);
            outputContent = outputContent.Replace("__IndyscanDaemonUiImage__", config.IndyscanDaemonUiImage);

            var workerPaths = config.Networks.Where(p => p.Add)
                                             .Select(network =>$"/home/indyscan/indyscan-daemon/app-configs-docker/{network.NameAndFixedId}.json");
            var workerPathString = string.Join(",", workerPaths);
            outputContent = outputContent.Replace("__WorkerConfigs__", workerPathString);

            File.WriteAllText(outputPath, outputContent);
            Console.WriteLine($"Created docker-compose.yml at {outputPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in CreateDockerCompose: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
    
    static void GenerateAllNetworksJson(Config config)
    {
        string projectDirectory = GetProjectDirectory();
        string templatePath = Path.Combine(projectDirectory, "Resources", "ApiTemplate", "ApiItemTemplate.json");
        string outputPath = Path.Combine(config.IndyscanBasePath, "start", "app-config-api", "allNetworks.json");

        if (!File.Exists(templatePath))
        {
            Console.WriteLine($"ApiItemTemplate.json not found at {templatePath}");
            return;
        }

        string template = File.ReadAllText(templatePath);
        var networkItems = config.Networks.Where(n => n.Add)
            .Select((network, index) =>
            {
                string item = template;
                item = item.Replace("__NAME__", network.NameAndFixedId);
                item = item.Replace("__DisplayNameShort__", network.DisplayNameShort);
                item = item.Replace("__DisplayNameLong__", network.DisplayNameLong);
                item = item.Replace("__DisplayDescription__", network.DisplayDescription);
                item = item.Replace("__ElasticSearchIndex__", network.ElasticSearchIndex);

                // Replace priority number
                item = Regex.Replace(item, @"""priority"": \d+", $"\"priority\": {index + 1}");

                // Handle AliasIds array
                if (network.AliasIds != null && network.AliasIds.Any())
                {
                    string aliasesJson = string.Join(", ", network.AliasIds.Select(alias => $"\"{alias}\""));
                    item = Regex.Replace(item, @"""aliases"": \[\],", $"\"aliases\": [{aliasesJson}],");
                }

                return item;
            });

        string allNetworksContent = "[" + string.Join(",", networkItems) + "]";

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        File.WriteAllText(outputPath, allNetworksContent);

        Console.WriteLine($"allNetworks.json generated successfully at {outputPath}");
    }
    
     static void GenerateNetworkConfigs(Config config)
    {
        string projectDirectory = GetProjectDirectory();
        string templatePath = Path.Combine(projectDirectory, "Resources", "DaemonTemplate", "AppConfigsDaemonTemplate.json");
        string outputDirectory = Path.Combine(config.IndyscanBasePath, "start", "app-configs-daemon");

        if (!File.Exists(templatePath))
        {
            Console.WriteLine($"AppConfigsDaemonTemplate.json not found at {templatePath}");
            return;
        }

        string template = File.ReadAllText(templatePath);

        // Process all networks
        foreach (var network in config.Networks)
        {
            string outputPath = Path.Combine(outputDirectory, $"{network.NameAndFixedId}.json");

            if (network.Add)
            {
                string networkConfig = template;
                networkConfig = networkConfig.Replace("__Name__", network.NameAndFixedId);
                networkConfig = networkConfig.Replace("__ElasticSearchIndex__", network.ElasticSearchIndex);
                networkConfig = networkConfig.Replace("__SeralizationSpeedSlowMediumFast__", network.SeralizationSpeedSlowMediumFast);
                networkConfig = networkConfig.Replace("__ExpansionSpeedSlowMediumFast__", network.ExpansionSpeedSlowMediumFast);

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllText(outputPath, networkConfig);

                Console.WriteLine($"Network config generated for {network.NameAndFixedId} at {outputPath}");
            }
            else
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                    Console.WriteLine($"Removed network config for {network.NameAndFixedId} from {outputPath}");
                }
            }
        }

        // Remove any remaining .json files in the output directory that don't correspond to networks in the config
        var configNetworkNames = config.Networks.Select(n => n.NameAndFixedId).ToHashSet();
        foreach (var file in Directory.GetFiles(outputDirectory, "*.json"))
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            if (!configNetworkNames.Contains(fileName))
            {
                File.Delete(file);
                Console.WriteLine($"Removed orphaned network config file: {file}");
            }
        }
    }
    
    static void CopyGenesisFiles(Config config)
    {
        string projectDirectory = GetProjectDirectory();
        string sourceGenesisDir = Path.Combine(projectDirectory, "Resources", "Genesis");
        string targetGenesisDir = Path.Combine(config.IndyscanBasePath, "start", "app-configs-daemon", "genesis");

        // Create the target Genesis directory if it doesn't exist
        Directory.CreateDirectory(targetGenesisDir);

        // Copy files for networks to be added and remove files for networks not to be added
        foreach (var network in config.Networks)
        {
            string targetFile = Path.Combine(targetGenesisDir, $"{network.NameAndFixedId}.txn");

            if (network.Add)
            {
                string sourceFile = Path.Combine(sourceGenesisDir, $"{network.NameAndFixedId}.txn");

                if (File.Exists(sourceFile))
                {
                    File.Copy(sourceFile, targetFile, true);
                    Console.WriteLine($"Copied Genesis file for {network.NameAndFixedId} to {targetFile}");
                }
                else
                {
                    Console.WriteLine($"Warning: Genesis file for {network.NameAndFixedId} not found at {sourceFile}");
                }
            }
            else
            {
                if (File.Exists(targetFile))
                {
                    File.Delete(targetFile);
                    Console.WriteLine($"Removed Genesis file for {network.NameAndFixedId} from {targetFile}");
                }
            }
        }

        // Remove any remaining .txn files in the target directory that don't correspond to networks in the config
        var configNetworkNames = config.Networks.Select(n => n.NameAndFixedId).ToHashSet();
        foreach (var file in Directory.GetFiles(targetGenesisDir, "*.txn"))
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            if (!configNetworkNames.Contains(fileName))
            {
                File.Delete(file);
                Console.WriteLine($"Removed orphaned Genesis file: {file}");
            }
        }
    }
}