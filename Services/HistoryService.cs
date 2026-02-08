using System.IO;
using System.Text.Json;
using Evaluator.Models;

namespace Evaluator.Services;

public class HistoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _basePath;

    public HistoryService()
    {
        _basePath = AppDomain.CurrentDomain.BaseDirectory;
    }

    /// <summary>
    /// Gets the history file path for a test (based on test definition file path).
    /// </summary>
    public string GetHistoryFilePath(string testFilePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(testFilePath) + ".json";
        var historyDir = Path.Combine(_basePath, "History");
        return Path.Combine(historyDir, fileName);
    }

    /// <summary>
    /// Aggregates per-question attempt counts from all test history files (total and correct).
    /// Key is question id from QuestionStatsService.GetQuestionIdFromSnapshot.
    /// </summary>
    public IReadOnlyDictionary<string, (int Total, int Correct)> GetPerQuestionAttemptStats()
    {
        var historyDir = Path.Combine(_basePath, "History");
        if (!Directory.Exists(historyDir))
            return new Dictionary<string, (int, int)>();

        var agg = new Dictionary<string, (int Total, int Correct)>();
        foreach (var file in Directory.EnumerateFiles(historyDir, "*.json"))
        {
            if (Path.GetFileName(file).Equals("QuestionStats.json", StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                var json = File.ReadAllText(file);
                var attempts = JsonSerializer.Deserialize<List<TestAttemptRecord>>(json, JsonOptions);
                if (attempts == null) continue;
                foreach (var record in attempts)
                {
                    for (int i = 0; i < record.Questions.Count; i++)
                    {
                        var snapshot = record.Questions[i];
                        var id = QuestionStatsService.GetQuestionIdFromSnapshot(snapshot);
                        var correctIndices = new HashSet<int>();
                        for (int j = 0; j < snapshot.Answers.Count; j++)
                            if (snapshot.Answers[j].IsCorrect)
                                correctIndices.Add(j);
                        var selected = i < record.UserSelections.Count
                            ? record.UserSelections[i].ToHashSet()
                            : new HashSet<int>();
                        var correct = correctIndices.SetEquals(selected);
                        if (!agg.TryGetValue(id, out var t))
                            t = (0, 0);
                        agg[id] = (t.Total + 1, t.Correct + (correct ? 1 : 0));
                    }
                }
            }
            catch { /* skip invalid files */ }
        }
        return agg;
    }

    /// <summary>
    /// Loads all attempt records for a test. Returns empty list if file missing or invalid.
    /// </summary>
    public IReadOnlyList<TestAttemptRecord> LoadAttempts(string testFilePath)
    {
        var path = GetHistoryFilePath(testFilePath);
        if (!File.Exists(path))
            return Array.Empty<TestAttemptRecord>();
        try
        {
            var json = File.ReadAllText(path);
            var list = JsonSerializer.Deserialize<List<TestAttemptRecord>>(json, JsonOptions);
            return list ?? new List<TestAttemptRecord>();
        }
        catch
        {
            return Array.Empty<TestAttemptRecord>();
        }
    }

    /// <summary>
    /// Appends one attempt and saves. Creates History folder and file if needed.
    /// </summary>
    public void SaveAttempt(string testFilePath, TestSession session, int correctCount, bool passed)
    {
        var record = BuildRecord(session, correctCount, passed);
        var path = GetHistoryFilePath(testFilePath);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var existing = LoadAttempts(testFilePath).ToList();
        existing.Add(record);
        var json = JsonSerializer.Serialize(existing, JsonOptions);
        File.WriteAllText(path, json);

        var questionStats = new QuestionStatsService();
        questionStats.UpdateFromAttemptRecord(record);
    }

    private static TestAttemptRecord BuildRecord(TestSession session, int correctCount, bool passed)
    {
        var finished = session.FinishedAt ?? DateTime.Now;
        var duration = (finished - session.StartedAt).TotalSeconds;
        var questions = session.Questions.Select(q => new QuestionSnapshot
        {
            Statement = q.Statement,
            Answers = q.Answers.Select(a => new QuestionAnswer { Text = a.Text, IsCorrect = a.IsCorrect }).ToList()
        }).ToList();
        var selections = session.UserSelections.Select(s => s.OrderBy(x => x).ToList()).ToList();

        return new TestAttemptRecord
        {
            StartedAt = session.StartedAt,
            FinishedAt = finished,
            DurationSeconds = duration,
            Passed = passed,
            CorrectCount = correctCount,
            TotalCount = session.Questions.Count,
            RequiredToPass = session.Definition.RequiredCorrectToPass,
            Questions = questions,
            UserSelections = selections
        };
    }
}
