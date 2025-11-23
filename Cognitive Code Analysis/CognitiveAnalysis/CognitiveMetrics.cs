namespace CognitiveCodeAnalysis.CognitiveAnalysis;

/// <summary>
/// Represents the cognitive metrics for a method.
/// </summary>
public class CognitiveMetrics
{
    // Metadata
    public string MethodName { get; set; }
    public string ClassName { get; set; }
    public string FilePath { get; set; }
    public string methodSignature;
    public int methodLineNumber;
    public int linesOfCode = 0;

    // Metrics
    public int ifCount = 0;
    public double ifScore = 0;

    public int elseCount = 0;
    public double elseScore = 0;

    public int loopCount = 0;
    public double loopScore = 0;

    public int switchCount = 0;
    public double switchScore = 0;

    public int tryCatchCount = 0;
    public double tryCatchScore = 0;

    public int returnCount = 0;
    public double returnScore = 0;

    public int argumentCount = 0;
    public double argumentScore = 0;

    public int nestingLevels = 0;
    public double nestingScore = 0;

    public double TotalScore = 0;

    public double cyclomaticComplexity = 0;

    public double? LineCoveragePercentage = null;

    public double? BranchCoveragePercentage = null;

    public bool IsPure = false;

    public CognitiveMetrics(
        string methodName,
        string className,
        string filePath,
        string signature,
        int methodLineNumber,
        int ifCount = 0,
        int elseCount = 0,
        int loopCount = 0,
        int switchCount = 0,
        int tryCatchCount = 0,
        int returnCount = 0,
        int argumentCount = 0,
        int linesOfCode = 0,
        int nestingLevels = 0,
        bool isPure = false,
        double cyclomaticComplexity = 0
    )
    {
        this.MethodName = methodName;
        this.ClassName = className;
        this.FilePath = filePath;
        this.methodSignature = signature;
        this.methodLineNumber = methodLineNumber;
        this.ifCount = ifCount;
        this.elseCount = elseCount;
        this.loopCount = loopCount;
        this.switchCount = switchCount;
        this.tryCatchCount = tryCatchCount;
        this.returnCount = returnCount;
        this.argumentCount = argumentCount;
        this.linesOfCode = linesOfCode;
        this.nestingLevels = nestingLevels;
        this.IsPure = isPure;
        this.cyclomaticComplexity = cyclomaticComplexity;
    }
}
