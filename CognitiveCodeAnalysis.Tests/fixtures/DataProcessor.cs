/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.Tests.Fixtures;

public class DataProcessor
{
    private List<int> numbers = new List<int>();
    private Dictionary<string, object> cache = new Dictionary<string, object>();

    public int CalculateComplexStatistics(
        int[] values,
        bool includeOutliers,
        bool useWeightedAverage,
        int minValue,
        int maxValue,
        string calculationMethod)
    {
        int result = 0;
        int sum = 0;
        int count = 0;
        int weightedSum = 0;
        int totalWeight = 0;

        if (values == null || values.Length == 0)
        {
            return 0;
        }

        foreach (int value in values)
        {
            if (value < minValue)
            {
                if (includeOutliers)
                {
                    sum += value;
                    count++;
                }
                else
                {
                    continue;
                }
            }
            else if (value > maxValue)
            {
                if (includeOutliers)
                {
                    sum += value;
                    count++;
                }
                else
                {
                    continue;
                }
            }
            else
            {
                sum += value;
                count++;

                if (useWeightedAverage)
                {
                    int weight = value % 10 + 1;
                    weightedSum += value * weight;
                    totalWeight += weight;
                }
            }
        }

        if (count == 0)
        {
            return 0;
        }

        if (calculationMethod == "Average")
        {
            result = sum / count;
        }
        else if (calculationMethod == "WeightedAverage")
        {
            if (totalWeight > 0)
            {
                result = weightedSum / totalWeight;
            }
            else
            {
                result = sum / count;
            }
        }
        else if (calculationMethod == "Sum")
        {
            result = sum;
        }
        else if (calculationMethod == "Max")
        {
            result = values.Max();
        }
        else if (calculationMethod == "Min")
        {
            result = values.Min();
        }
        else
        {
            result = sum / count;
        }

        if (result < 0)
        {
            return 0;
        }
        else if (result > 1000000)
        {
            return 1000000;
        }
        else
        {
            return result;
        }
    }

    public string TransformData(
        string input,
        bool removeWhitespace,
        bool convertToLowercase,
        bool addPrefix,
        bool addSuffix,
        int maxLength)
    {
        string output = input;

        if (string.IsNullOrEmpty(output))
        {
            return "";
        }

        if (removeWhitespace)
        {
            output = output.Replace(" ", "");
            output = output.Replace("\t", "");
            output = output.Replace("\n", "");
        }

        if (convertToLowercase)
        {
            output = output.ToLower();
        }

        if (addPrefix)
        {
            output = "PREFIX_" + output;
        }

        if (addSuffix)
        {
            output = output + "_SUFFIX";
        }

        if (output.Length > maxLength)
        {
            output = output.Substring(0, maxLength);
        }

        return output;
    }
}

