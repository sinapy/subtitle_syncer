using System.Globalization;
using System.Text.Json;
using SubtitleSyncer.Vad;

var arguments = args.ToList();
var exportIndex = arguments.IndexOf("--export-activities");
if (exportIndex < 0 || exportIndex == arguments.Count - 1)
{
    Console.Error.WriteLine("Usage: SubtitleSyncer.Vad.Console <movie> [analysis-seconds] --export-activities <output.json>");
    return 2;
}

var exportPath = Path.GetFullPath(arguments[exportIndex + 1]);
arguments.RemoveAt(exportIndex + 1);
arguments.RemoveAt(exportIndex);
if (arguments.Count is < 1 or > 2)
{
    Console.Error.WriteLine("Usage: SubtitleSyncer.Vad.Console <movie> [analysis-seconds] --export-activities <output.json>");
    return 2;
}

var moviePath = Path.GetFullPath(arguments[0]);
if (!File.Exists(moviePath))
{
    Console.Error.WriteLine("The movie was not found.");
    return 2;
}

var analysisDuration = arguments.Count == 2
    ? TimeSpan.FromSeconds(double.Parse(arguments[1], CultureInfo.InvariantCulture))
    : TimeSpan.FromHours(1);
if (analysisDuration <= TimeSpan.Zero)
{
    Console.Error.WriteLine("analysis-seconds must be positive.");
    return 2;
}

var analysis = new VoiceActivityDetector(new FfmpegAudioPreparation(), new TenVadEngine())
    .Analyze(moviePath, analysisDuration);
var export = new ActivityExport(analysis.Audio.Duration.TotalSeconds,
    analysis.Activities.Select(activity => new ActivityExportItem(activity.Start.TotalSeconds, activity.End.TotalSeconds)).ToArray());
File.WriteAllText(exportPath, JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Wrote {analysis.Activities.Count} activities to {exportPath}.");
return 0;

file sealed record ActivityExport(double AnalysisSeconds, IReadOnlyList<ActivityExportItem> Activities);
file sealed record ActivityExportItem(double StartSeconds, double EndSeconds);
