using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Evaluator.Models;

namespace Evaluator.Services;

/// <summary>
/// Tracks per-question relative probability (1-20, default 10) for weighted selection.
/// Correct: -1, incorrect: +5. Stored in History/QuestionStats.json.
/// </summary>
public class QuestionStatsService
{
    private const int DefaultWeight = 10;
    private const int MinWeight = 1;
    private const int MaxWeight = 20;
    private const int CorrectDelta = -1;
    private const int IncorrectDelta = 5;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _basePath;
    private readonly string _statsPath;

    public QuestionStatsService()
    {
        _basePath = AppDomain.CurrentDomain.BaseDirectory;
        _statsPath = Path.Combine(_basePath, "History", "QuestionStats.json");
    }

    /// <summary>
    /// Computes a stable id for a question from its content (statement + answers).
    /// </summary>
    public static string GetQuestionId(Question q)
    {
        return GetQuestionIdFromSnapshot(new QuestionSnapshot
        {
            Statement = q.Statement,
            Answers = q.Answers.Select(a => new QuestionAnswer { Text = a.Text, IsCorrect = a.IsCorrect }).ToList()
        });
    }

    /// <summary>
    /// Computes the same id from a snapshot (e.g. from an attempt record).
    /// </summary>
    public static string GetQuestionIdFromSnapshot(QuestionSnapshot snapshot)
    {
        var canonical = snapshot.Statement.Trim() + "|" + string.Join("|",
            snapshot.Answers
                .OrderBy(a => a.Text)
                .Select(a => a.Text + ":" + (a.IsCorrect ? "T" : "F")));
        var bytes = Encoding.UTF8.GetBytes(canonical);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).AsSpan(0, 32).ToString();
    }

    /// <summary>
    /// Returns a snapshot of all weights (one load). Use with <see cref="GetWeightFromSnapshot"/> to avoid loading per question.
    /// </summary>
    public IReadOnlyDictionary<string, int> GetWeightsSnapshot()
    {
        return LoadWeights();
    }

    /// <summary>
    /// Gets weight from a snapshot (1-20). Questions never attempted get max weight so they are selected more often.
    /// </summary>
    public static int GetWeightFromSnapshot(IReadOnlyDictionary<string, int> snapshot, string questionId)
    {
        return snapshot.TryGetValue(questionId, out var w) ? Math.Clamp(w, MinWeight, MaxWeight) : MaxWeight;
    }

    /// <summary>
    /// Updates weights from an attempt record: correct -1, incorrect +5, clamp 1-20.
    /// </summary>
    public void UpdateFromAttemptRecord(TestAttemptRecord record)
    {
        var dict = LoadWeights();
        for (int i = 0; i < record.Questions.Count; i++)
        {
            var snapshot = record.Questions[i];
            var id = GetQuestionIdFromSnapshot(snapshot);
            var correctIndices = new HashSet<int>();
            for (int j = 0; j < snapshot.Answers.Count; j++)
                if (snapshot.Answers[j].IsCorrect)
                    correctIndices.Add(j);
            var selected = (record.UserSelections.Count > i) ? record.UserSelections[i].ToHashSet() : new HashSet<int>();
            var correct = correctIndices.SetEquals(selected);

            var current = dict.TryGetValue(id, out var w) ? w : DefaultWeight;
            var next = correct ? current + CorrectDelta : current + IncorrectDelta;
            dict[id] = Math.Clamp(next, MinWeight, MaxWeight);
        }
        SaveWeights(dict);
    }

    private Dictionary<string, int> LoadWeights()
    {
        var dir = Path.GetDirectoryName(_statsPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            return new Dictionary<string, int>();
        if (!File.Exists(_statsPath))
            return new Dictionary<string, int>();
        try
        {
            var json = File.ReadAllText(_statsPath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
            return dict ?? new Dictionary<string, int>();
        }
        catch
        {
            return new Dictionary<string, int>();
        }
    }

    private void SaveWeights(Dictionary<string, int> dict)
    {
        var dir = Path.GetDirectoryName(_statsPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(dict, JsonOptions);
        File.WriteAllText(_statsPath, json);
    }
}
