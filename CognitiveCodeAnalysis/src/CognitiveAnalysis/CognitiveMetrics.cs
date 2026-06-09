/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.HalsteadAnalysis;

namespace CognitiveCodeAnalysis.CognitiveAnalysis;

/// <summary>
/// <![CDATA[Cognitive Metrics Data Object]]>
/// </summary>
public class CognitiveMetrics
{
    // File and Class Info
    public string MethodName { get; set; }
    public string ClassName { get; set; }
    public string FilePath { get; set; }
    public bool HasCoverageData => lineCoveragePercentage.HasValue || branchCoveragePercentage.HasValue;

    public string methodSignature;
    public int methodLineNumber;
    public int linesOfCode = 0;
    public double linesOfCodeScore = 0;

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

    public int localVariableCount = 0;
    public double localVariableScore = 0;

    public int fieldAccessCount = 0;
    public double fieldAccessScore = 0;

    public int propertyAccessCount = 0;
    public double propertyAccessScore = 0;

    public double totalScore = 0;

    public double cyclomaticComplexity = 0;

    public HalsteadMetrics? Halstead { get; set; }

    public double? lineCoveragePercentage = null;
    public double? branchCoveragePercentage = null;

    public double? churnScore = null;

    public CognitiveMetrics(
        string methodName,
        string className,
        string filePath,
        string methodSignature,
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
        double cyclomaticComplexity = 0,
        int localVariableCount = 0,
        int fieldAccessCount = 0,
        int propertyAccessCount = 0,
        double? lineCoveragePercentage = null,
        double? branchCoveragePercentage = null,
        double? churnScore = null,
        HalsteadMetrics? halstead = null
    )
    {
        MethodName = methodName;
        ClassName = className;
        FilePath = filePath;
        this.methodSignature = methodSignature;
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
        this.cyclomaticComplexity = cyclomaticComplexity;
        this.localVariableCount = localVariableCount;
        this.fieldAccessCount = fieldAccessCount;
        this.propertyAccessCount = propertyAccessCount;
        this.lineCoveragePercentage = lineCoveragePercentage;
        this.branchCoveragePercentage = branchCoveragePercentage;
        this.churnScore = churnScore;
        Halstead = halstead;
    }
}
