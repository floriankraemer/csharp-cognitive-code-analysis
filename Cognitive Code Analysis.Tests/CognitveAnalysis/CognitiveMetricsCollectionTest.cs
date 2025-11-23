using CognitiveCodeAnalysis.CognitiveAnalysis;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis;

public class CognitiveMetricsCollectionTest
{
    [Fact]
    public void GetTotalClasses_WithMultipleClasses_ReturnsCorrectCount()
    {
        // Arrange
        var collection = new CognitiveMetricsCollection
        {
            CreateMetrics("Method1", "Class1", "File1.cs", 5.0),
            CreateMetrics("Method2", "Class1", "File1.cs", 3.0),
            CreateMetrics("Method3", "Class2", "File1.cs", 2.0),
            CreateMetrics("Method4", "Class2", "File1.cs", 4.0),
            CreateMetrics("Method5", "Class3", "File2.cs", 1.0)
        };

        // Act
        int result = collection.GetTotalClasses();

        // Assert
        Assert.Equal(3, result);
    }

    [Fact]
    public void GetTotalClasses_WithEmptyCollection_ReturnsZero()
    {
        // Arrange
        var collection = new CognitiveMetricsCollection();

        // Act
        int result = collection.GetTotalClasses();

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void GetTotalClasses_WithSameClassInDifferentFiles_CountsAsSeparateClasses()
    {
        // Arrange
        var collection = new CognitiveMetricsCollection
        {
            CreateMetrics("Method1", "Class1", "File1.cs", 5.0),
            CreateMetrics("Method2", "Class1", "File2.cs", 3.0)
        };

        // Act
        int result = collection.GetTotalClasses();

        // Assert
        Assert.Equal(2, result);
    }

    [Fact]
    public void GetTotalMethods_WithMultipleMethods_ReturnsCorrectCount()
    {
        // Arrange
        var collection = new CognitiveMetricsCollection
        {
            CreateMetrics("Method1", "Class1", "File1.cs", 5.0),
            CreateMetrics("Method2", "Class1", "File1.cs", 3.0),
            CreateMetrics("Method3", "Class2", "File2.cs", 2.0)
        };

        // Act
        int result = collection.GetTotalMethods();

        // Assert
        Assert.Equal(3, result);
    }

    [Fact]
    public void GetTotalMethods_WithEmptyCollection_ReturnsZero()
    {
        // Arrange
        var collection = new CognitiveMetricsCollection();

        // Act
        int result = collection.GetTotalMethods();

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void GetMethodsExceedingThreshold_WithVariousScores_ReturnsCorrectCount()
    {
        // Arrange
        var collection = new CognitiveMetricsCollection
        {
            CreateMetrics("Method1", "Class1", "File1.cs", 5.0),
            CreateMetrics("Method2", "Class1", "File1.cs", 3.0),
            CreateMetrics("Method3", "Class2", "File2.cs", 7.0),
            CreateMetrics("Method4", "Class2", "File2.cs", 2.0),
            CreateMetrics("Method5", "Class3", "File3.cs", 4.0)
        };

        // Act
        int result = collection.GetMethodsExceedingThreshold(4.0);

        // Assert
        Assert.Equal(2, result);
    }

    [Fact]
    public void GetMethodsExceedingThreshold_WithNoMethodsExceeding_ReturnsZero()
    {
        // Arrange
        var collection = new CognitiveMetricsCollection
        {
            CreateMetrics("Method1", "Class1", "File1.cs", 1.0),
            CreateMetrics("Method2", "Class1", "File1.cs", 2.0),
            CreateMetrics("Method3", "Class2", "File2.cs", 3.0)
        };

        // Act
        int result = collection.GetMethodsExceedingThreshold(5.0);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void GetMethodsExceedingThreshold_WithExactThreshold_DoesNotCountExactMatches()
    {
        // Arrange
        var collection = new CognitiveMetricsCollection
        {
            CreateMetrics("Method1", "Class1", "File1.cs", 5.0),
            CreateMetrics("Method2", "Class1", "File1.cs", 5.0),
            CreateMetrics("Method3", "Class2", "File2.cs", 6.0)
        };

        // Act
        int result = collection.GetMethodsExceedingThreshold(5.0);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void GetClassesWithExceedingMethods_WithMultipleClasses_ReturnsCorrectCount()
    {
        // Arrange
        var collection = new CognitiveMetricsCollection
        {
            CreateMetrics("Method1", "Class1", "File1.cs", 5.0),
            CreateMetrics("Method2", "Class1", "File1.cs", 3.0),
            CreateMetrics("Method3", "Class2", "File2.cs", 7.0),
            CreateMetrics("Method4", "Class2", "File2.cs", 2.0),
            CreateMetrics("Method5", "Class3", "File3.cs", 1.0)
        };

        // Act
        int result = collection.GetClassesWithExceedingMethods(4.0);

        // Assert
        Assert.Equal(2, result);
    }

    [Fact]
    public void GetClassesWithExceedingMethods_WithNoClassesExceeding_ReturnsZero()
    {
        // Arrange
        var collection = new CognitiveMetricsCollection
        {
            CreateMetrics("Method1", "Class1", "File1.cs", 1.0),
            CreateMetrics("Method2", "Class1", "File1.cs", 2.0),
            CreateMetrics("Method3", "Class2", "File2.cs", 3.0)
        };

        // Act
        int result = collection.GetClassesWithExceedingMethods(5.0);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void GetClassesWithExceedingMethods_WithSameClassInDifferentFiles_CountsAsSeparateClasses()
    {
        // Arrange
        var collection = new CognitiveMetricsCollection
        {
            CreateMetrics("Method1", "Class1", "File1.cs", 5.0),
            CreateMetrics("Method2", "Class1", "File2.cs", 5.0)
        };

        // Act
        int result = collection.GetClassesWithExceedingMethods(4.0);

        // Assert
        Assert.Equal(2, result);
    }

    [Fact]
    public void GetMethodsPercentage_WithVariousScores_ReturnsCorrectPercentage()
    {
        // Arrange
        var collection = new CognitiveMetricsCollection
        {
            CreateMetrics("Method1", "Class1", "File1.cs", 5.0),
            CreateMetrics("Method2", "Class1", "File1.cs", 3.0),
            CreateMetrics("Method3", "Class2", "File2.cs", 7.0),
            CreateMetrics("Method4", "Class2", "File2.cs", 2.0)
        };

        // Act
        double result = collection.GetMethodsPercentage(4.0);

        // Assert
        Assert.Equal(50.0, result);
    }

    [Fact]
    public void GetMethodsPercentage_WithEmptyCollection_ReturnsZero()
    {
        // Arrange
        var collection = new CognitiveMetricsCollection();

        // Act
        double result = collection.GetMethodsPercentage(5.0);

        // Assert
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void GetMethodsPercentage_WithAllMethodsExceeding_ReturnsOneHundred()
    {
        // Arrange
        var collection = new CognitiveMetricsCollection
        {
            CreateMetrics("Method1", "Class1", "File1.cs", 5.0),
            CreateMetrics("Method2", "Class1", "File1.cs", 6.0),
            CreateMetrics("Method3", "Class2", "File2.cs", 7.0)
        };

        // Act
        double result = collection.GetMethodsPercentage(4.0);

        // Assert
        Assert.Equal(100.0, result);
    }

    [Fact]
    public void GetMethodsPercentage_WithNoMethodsExceeding_ReturnsZero()
    {
        // Arrange
        var collection = new CognitiveMetricsCollection
        {
            CreateMetrics("Method1", "Class1", "File1.cs", 1.0),
            CreateMetrics("Method2", "Class1", "File1.cs", 2.0),
            CreateMetrics("Method3", "Class2", "File2.cs", 3.0)
        };

        // Act
        double result = collection.GetMethodsPercentage(5.0);

        // Assert
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void GetClassesPercentage_WithVariousScores_ReturnsCorrectPercentage()
    {
        // Arrange
        var collection = new CognitiveMetricsCollection
        {
            CreateMetrics("Method1", "Class1", "File1.cs", 5.0),
            CreateMetrics("Method2", "Class1", "File1.cs", 3.0),
            CreateMetrics("Method3", "Class2", "File2.cs", 7.0),
            CreateMetrics("Method4", "Class2", "File2.cs", 2.0),
            CreateMetrics("Method5", "Class3", "File3.cs", 1.0)
        };

        // Act
        double result = collection.GetClassesPercentage(4.0);

        // Assert
        Assert.Equal(66.66666666666667, result, 10);
    }

    [Fact]
    public void GetClassesPercentage_WithEmptyCollection_ReturnsZero()
    {
        // Arrange
        var collection = new CognitiveMetricsCollection();

        // Act
        double result = collection.GetClassesPercentage(5.0);

        // Assert
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void GetClassesPercentage_WithAllClassesExceeding_ReturnsOneHundred()
    {
        // Arrange
        var collection = new CognitiveMetricsCollection
        {
            CreateMetrics("Method1", "Class1", "File1.cs", 5.0),
            CreateMetrics("Method2", "Class2", "File2.cs", 6.0),
            CreateMetrics("Method3", "Class3", "File3.cs", 7.0)
        };

        // Act
        double result = collection.GetClassesPercentage(4.0);

        // Assert
        Assert.Equal(100.0, result);
    }

    [Fact]
    public void GetClassesPercentage_WithNoClassesExceeding_ReturnsZero()
    {
        // Arrange
        var collection = new CognitiveMetricsCollection
        {
            CreateMetrics("Method1", "Class1", "File1.cs", 1.0),
            CreateMetrics("Method2", "Class2", "File2.cs", 2.0),
            CreateMetrics("Method3", "Class3", "File3.cs", 3.0)
        };

        // Act
        double result = collection.GetClassesPercentage(5.0);

        // Assert
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void GetClassesPercentage_WithSameClassInDifferentFiles_CountsAsSeparateClasses()
    {
        // Arrange
        var collection = new CognitiveMetricsCollection
        {
            CreateMetrics("Method1", "Class1", "File1.cs", 5.0),
            CreateMetrics("Method2", "Class1", "File2.cs", 5.0)
        };

        // Act
        double result = collection.GetClassesPercentage(4.0);

        // Assert
        Assert.Equal(100.0, result);
    }

    private static CognitiveMetrics CreateMetrics(string methodName, string className, string filePath, double totalScore)
    {
        var metrics = new CognitiveMetrics(
            methodName: methodName,
            className: className,
            filePath: filePath,
            signature: $"public void {methodName}()",
            methodLineNumber: 1
        );
        metrics.TotalScore = totalScore;
        return metrics;
    }
}

