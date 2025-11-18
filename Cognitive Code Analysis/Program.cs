using CognitiveCodeAnalysis.Commands;
using Spectre.Console.Cli;

var app = new CommandApp<AnalyseCommand>();
return app.Run(args);
