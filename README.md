# Cognitive Complexity Analysis

Cognitive Code Analysis is an approach to understanding and improving code by focusing on how human cognition interacts with code. It emphasizes making code more readable, understandable, and maintainable by considering the cognitive processes of the developers who write and work with the code.

> "Human short-term or working memory was estimated to be limited to 7 ± 2 variables in the 1950s. A more current estimate is 4 ± 1 constructs. Decision quality generally becomes degraded once this limit of four constructs is exceeded."

[Source: Human Cognitive Limitations. Broad, Consistent, Clinical Application of Physiological Principles Will Require Decision Support](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC5822395/)

## Running the Analysis 🧑‍💻

The tool analyzes C# files and generates cognitive complexity reports. Run the analysis using:

```powershell
CognitiveCodeAnalysis [searchPath] [options]
```

**Arguments:**

- `[searchPath]` - (Optional) Path to search for C# files. Defaults to the current directory.

**Options:**

- `-c|--config <path>`      - Load a custom configuration file
- `-r|--report-type <type>` - Report type: `ConsoleText` (default) or `Html`
- `-o|--output-file <path>` - Output file path (defaults to `cognitive-analysis-report`)

**Examples:**

```powershell
# Analyze current directory with console output
CognitiveCodeAnalysis

# Analyze a specific directory
CognitiveCodeAnalysis ./src

# Generate an HTML report
CognitiveCodeAnalysis ./src -r Html -o report.html

# Use a custom configuration file
CognitiveCodeAnalysis ./src -c my-config.json
```

## Resources 🔗

These pages and papers provide more information on cognitive limitations and readability and the impact on the business.

- **Cognitive Complexity**
  - [Cognitive Complexity Wikipedia](https://en.wikipedia.org/wiki/Cognitive_complexity)
  - [Cognitive Complexity and Its Effect on the Code](https://www.baeldung.com/java-cognitive-complexity) by Emanuel Trandafir.
  - [Human Cognitive Limitations. Broad, Consistent, Clinical Application of Physiological Principles Will Require Decision Support](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC5822395/) by Alan H. Morris.
  - [The Magical Number 4 in Short-Term Memory: A Reconsideration of Mental Storage Capacity](https://www.researchgate.net/publication/11830840_The_Magical_Number_4_in_Short-Term_Memory_A_Reconsideration_of_Mental_Storage_Capacity) by Nelson Cowan
  - [Neural substrates of cognitive capacity limitations](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC3131328/) by Timothy J. Buschman,a,1 Markus Siegel,a,b Jefferson E. Roy, and Earl K. Millera.
  - [Code Readability Testing, an Empirical Study](https://www.researchgate.net/publication/299412540_Code_Readability_Testing_an_Empirical_Study) by Todd Sedano.
  - [An Empirical Validation of Cognitive Complexity as a Measure of Source Code Understandability](https://arxiv.org/pdf/2007.12520) by Marvin Muñoz Barón, Marvin Wyrich, and Stefan Wagner.
- **Halstead Complexity**
  - [Halstead Complexity](https://en.wikipedia.org/wiki/Halstead_complexity_measures)
- **Cyclomatic Complexity**
  - [Cyclomatic Complexity](https://en.wikipedia.org/wiki/Cyclomatic_complexity)

## License ⚖️

Copyright Florian Krämer

Licensed under the [GPL3 license](LICENSE).
