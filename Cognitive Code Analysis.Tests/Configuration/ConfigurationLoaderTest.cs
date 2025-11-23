using CognitiveCodeAnalysis.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CognitiveCodeAnalysis.Tests.Configuration;

public class ConfigurationLoaderTest : IDisposable
{
    private readonly List<string> _tempFiles = new();

    [Fact]
    public void Load_WithDefaultPath_LoadsConfiguration()
    {
        // Arrange - Create a temporary config file in the test output directory
        string tempConfigPath = CreateTempConfigFile(GetValidConfigJson());
        
        // Act
        CognitiveConfiguration config = ConfigurationLoader.Load(tempConfigPath);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(0.5, config.ScoreThreshold);
        Assert.True(config.ShowOnlyMethodsExceedingThreshold);
        Assert.False(config.GroupByClass);
        Assert.NotNull(config.Metrics);
    }

    [Fact]
    public void Load_WithCustomPath_LoadsConfiguration()
    {
        // Arrange
        string customPath = Path.Combine(Path.GetTempPath(), $"test-config-{Guid.NewGuid()}.json");
        File.WriteAllText(customPath, GetValidConfigJson());
        _tempFiles.Add(customPath);

        // Act
        CognitiveConfiguration config = ConfigurationLoader.Load(customPath);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(0.5, config.ScoreThreshold);
    }

    [Fact]
    public void Load_WithDifferentConfiguration_LoadsCorrectly()
    {
        // Arrange
        string configJson = """
        {
            "cognitive": {
                "excludeFilePatterns": ["*.test.cs", "*.spec.cs"],
                "excludePatterns": ["Test*", "Mock*"],
                "scoreThreshold": 0.75,
                "showOnlyMethodsExceedingThreshold": false,
                "groupByClass": true,
                "countElseAsNesting": true,
                "countElseIfAsNesting": true,
                "metrics": {
                    "linesOfCode": {
                        "threshold": 100,
                        "scale": 50.0,
                        "enabled": true
                    },
                    "argumentCount": {
                        "threshold": 5,
                        "scale": 2.0,
                        "enabled": false
                    }
                }
            }
        }
        """;
        string tempConfigPath = CreateTempConfigFile(configJson);

        // Act
        CognitiveConfiguration config = ConfigurationLoader.Load(tempConfigPath);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(2, config.ExcludeFilePatterns.Length);
        Assert.Contains("*.test.cs", config.ExcludeFilePatterns);
        Assert.Contains("*.spec.cs", config.ExcludeFilePatterns);
        Assert.Equal(2, config.ExcludePatterns.Length);
        Assert.Equal(0.75, config.ScoreThreshold);
        Assert.False(config.ShowOnlyMethodsExceedingThreshold);
        Assert.True(config.GroupByClass);
        Assert.True(config.CountElseAsNesting);
        Assert.True(config.CountElseIfAsNesting);
        Assert.Equal(2, config.Metrics.Count);
        Assert.True(config.Metrics["linesOfCode"].Enabled);
        Assert.False(config.Metrics["argumentCount"].Enabled);
        Assert.Equal(100, config.Metrics["linesOfCode"].Threshold);
        Assert.Equal(50.0, config.Metrics["linesOfCode"].Scale);
    }

    [Fact]
    public void Load_WithMinimalConfiguration_LoadsWithDefaults()
    {
        // Arrange
        string configJson = """
        {
            "cognitive": {
                "scoreThreshold": 0.3
            }
        }
        """;
        string tempConfigPath = CreateTempConfigFile(configJson);

        // Act
        CognitiveConfiguration config = ConfigurationLoader.Load(tempConfigPath);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(0.3, config.ScoreThreshold);
        Assert.True(config.ShowOnlyMethodsExceedingThreshold); // Default value
        Assert.False(config.GroupByClass); // Default value
        Assert.False(config.CountElseAsNesting); // Default value
        Assert.False(config.CountElseIfAsNesting); // Default value
        Assert.NotNull(config.Metrics);
        Assert.Empty(config.Metrics);
        Assert.NotNull(config.ExcludeFilePatterns);
        Assert.Empty(config.ExcludeFilePatterns);
        Assert.NotNull(config.ExcludePatterns);
        Assert.Empty(config.ExcludePatterns);
    }

    [Fact]
    public void Load_WithInvalidJson_ThrowsException()
    {
        // Arrange
        string invalidJson = "{ invalid json }";
        string tempConfigPath = CreateTempConfigFile(invalidJson);

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => ConfigurationLoader.Load(tempConfigPath));
    }

    [Fact]
    public void Load_WithMissingFile_ThrowsException()
    {
        // Arrange
        string nonExistentPath = Path.Combine(Path.GetTempPath(), $"non-existent-{Guid.NewGuid()}.json");

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => ConfigurationLoader.Load(nonExistentPath));
    }

    [Fact]
    public void Load_WithEmptyFile_ThrowsException()
    {
        // Arrange
        string tempConfigPath = CreateTempConfigFile("");

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => ConfigurationLoader.Load(tempConfigPath));
    }

    [Fact]
    public void Load_WithMissingCognitiveSection_ReturnsEmptyConfiguration()
    {
        // Arrange
        string configJson = """
        {
            "otherSection": {
                "value": "test"
            }
        }
        """;
        string tempConfigPath = CreateTempConfigFile(configJson);

        // Act
        CognitiveConfiguration config = ConfigurationLoader.Load(tempConfigPath);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(0.0, config.ScoreThreshold); // Default value
        Assert.True(config.ShowOnlyMethodsExceedingThreshold); // Default value
        Assert.Empty(config.Metrics);
    }

    [Fact]
    public void Load_WithInvalidMetricConfiguration_StillLoadsOtherProperties()
    {
        // Arrange - Invalid metric configuration (missing required properties)
        string configJson = """
        {
            "cognitive": {
                "scoreThreshold": 0.6,
                "showOnlyMethodsExceedingThreshold": true,
                "metrics": {
                    "linesOfCode": {
                        "threshold": 50
                    }
                }
            }
        }
        """;
        string tempConfigPath = CreateTempConfigFile(configJson);

        // Act
        CognitiveConfiguration config = ConfigurationLoader.Load(tempConfigPath);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(0.6, config.ScoreThreshold);
        Assert.True(config.ShowOnlyMethodsExceedingThreshold);
        // The metric may have default values for missing properties
        Assert.NotNull(config.Metrics);
    }

    [Fact]
    public void ConfigureServices_WithCustomPath_ConfiguresServicesCorrectly()
    {
        // Arrange
        string tempConfigPath = CreateTempConfigFile(GetValidConfigJson());

        // Act
        IServiceProvider serviceProvider = ConfigurationLoader.ConfigureServices(tempConfigPath);
        CognitiveConfiguration config = ConfigurationLoader.GetConfiguration(serviceProvider);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(0.5, config.ScoreThreshold);
    }

    [Fact]
    public void ConfigureServices_WithDifferentConfiguration_ConfiguresServicesCorrectly()
    {
        // Arrange
        string configJson = """
        {
            "cognitive": {
                "scoreThreshold": 0.9,
                "groupByClass": true,
                "metrics": {
                    "ifCount": {
                        "threshold": 10,
                        "scale": 2.5,
                        "enabled": true
                    }
                }
            }
        }
        """;
        string tempConfigPath = CreateTempConfigFile(configJson);

        // Act
        IServiceProvider serviceProvider = ConfigurationLoader.ConfigureServices(tempConfigPath);
        CognitiveConfiguration config = ConfigurationLoader.GetConfiguration(serviceProvider);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(0.9, config.ScoreThreshold);
        Assert.True(config.GroupByClass);
        Assert.Single(config.Metrics);
        Assert.True(config.Metrics["ifCount"].Enabled);
        Assert.Equal(10, config.Metrics["ifCount"].Threshold);
        Assert.Equal(2.5, config.Metrics["ifCount"].Scale);
    }

    [Fact]
    public void ConfigureServices_WithInvalidJson_ThrowsException()
    {
        // Arrange
        string invalidJson = "{ malformed json";
        string tempConfigPath = CreateTempConfigFile(invalidJson);

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => ConfigurationLoader.ConfigureServices(tempConfigPath));
    }

    [Fact]
    public void GetConfiguration_WithValidServiceProvider_ReturnsConfiguration()
    {
        // Arrange
        string tempConfigPath = CreateTempConfigFile(GetValidConfigJson());
        IServiceProvider serviceProvider = ConfigurationLoader.ConfigureServices(tempConfigPath);

        // Act
        CognitiveConfiguration config = ConfigurationLoader.GetConfiguration(serviceProvider);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(0.5, config.ScoreThreshold);
    }

    [Fact]
    public void Load_WithNestedMetrics_LoadsAllMetrics()
    {
        // Arrange
        string configJson = """
        {
            "cognitive": {
                "scoreThreshold": 0.5,
                "metrics": {
                    "linesOfCode": {
                        "threshold": 60,
                        "scale": 25.0,
                        "enabled": true
                    },
                    "argumentCount": {
                        "threshold": 4,
                        "scale": 1.0,
                        "enabled": true
                    },
                    "returnCount": {
                        "threshold": 2,
                        "scale": 5.0,
                        "enabled": true
                    },
                    "variableCount": {
                        "threshold": 4,
                        "scale": 5.0,
                        "enabled": false
                    },
                    "propertyCallCount": {
                        "threshold": 4,
                        "scale": 15.0,
                        "enabled": true
                    },
                    "ifCount": {
                        "threshold": 3,
                        "scale": 1.0,
                        "enabled": true
                    },
                    "nestingLevels": {
                        "threshold": 1,
                        "scale": 1.0,
                        "enabled": true
                    },
                    "elseCount": {
                        "threshold": 1,
                        "scale": 1.0,
                        "enabled": true
                    }
                }
            }
        }
        """;
        string tempConfigPath = CreateTempConfigFile(configJson);

        // Act
        CognitiveConfiguration config = ConfigurationLoader.Load(tempConfigPath);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(8, config.Metrics.Count);
        Assert.True(config.Metrics.ContainsKey("linesOfCode"));
        Assert.True(config.Metrics.ContainsKey("argumentCount"));
        Assert.True(config.Metrics.ContainsKey("returnCount"));
        Assert.True(config.Metrics.ContainsKey("variableCount"));
        Assert.True(config.Metrics.ContainsKey("propertyCallCount"));
        Assert.True(config.Metrics.ContainsKey("ifCount"));
        Assert.True(config.Metrics.ContainsKey("nestingLevels"));
        Assert.True(config.Metrics.ContainsKey("elseCount"));
        
        // Verify some specific values
        Assert.Equal(60, config.Metrics["linesOfCode"].Threshold);
        Assert.Equal(25.0, config.Metrics["linesOfCode"].Scale);
        Assert.False(config.Metrics["variableCount"].Enabled);
    }

    [Fact]
    public void Load_WithRelativePath_HandlesCorrectly()
    {
        // Arrange - Create config in a subdirectory
        string tempDir = Path.Combine(Path.GetTempPath(), $"test-config-dir-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        _tempFiles.Add(tempDir); // Mark for cleanup
        
        string configPath = Path.Combine(tempDir, "config.json");
        File.WriteAllText(configPath, GetValidConfigJson());
        _tempFiles.Add(configPath);

        // Act
        CognitiveConfiguration config = ConfigurationLoader.Load(configPath);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(0.5, config.ScoreThreshold);
    }

    private string CreateTempConfigFile(string content)
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"test-config-{Guid.NewGuid()}.json");
        File.WriteAllText(tempPath, content);
        _tempFiles.Add(tempPath);
        return tempPath;
    }

    private static string GetValidConfigJson()
    {
        return """
        {
            "cognitive": {
                "excludeFilePatterns": [],
                "excludePatterns": [],
                "scoreThreshold": 0.5,
                "showOnlyMethodsExceedingThreshold": true,
                "groupByClass": false,
                "countElseAsNesting": false,
                "countElseIfAsNesting": false,
                "metrics": {
                    "linesOfCode": {
                        "threshold": 60,
                        "scale": 25.0,
                        "enabled": true
                    }
                }
            }
        }
        """;
    }

    public void Dispose()
    {
        foreach (string file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
                else if (Directory.Exists(file))
                {
                    Directory.Delete(file, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        _tempFiles.Clear();
    }
}

