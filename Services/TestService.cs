using System.IO;
using System.Text.Json;
using Evaluator.Models;

namespace Evaluator.Services;

/// <summary>
/// Info for one available test (path + definition for display and starting).
/// </summary>
public record AvailableTest(string FilePath, TestDefinition Definition)
{
    public string DisplayName => Definition.Name ?? Path.GetFileNameWithoutExtension(FilePath);
}

/// <summary>
/// Info for one question list file (path for display and loading).
/// </summary>
public record AvailableQuestionList(string FilePath)
{
    public string DisplayName => Path.GetFileNameWithoutExtension(FilePath);
}

/// <summary>
/// One question row for the question list details panel (statement, answers with correct flag, stats).
/// </summary>
public record QuestionListQuestionDetail(string Statement, List<QuestionAnswer> Answers, int TimesAnswered, int TimesCorrect, int Weight);

public class TestService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _basePath;

    public TestService()
    {
        _basePath = AppDomain.CurrentDomain.BaseDirectory;
    }

    /// <summary>
    /// Discovers test definition files in the Tests folder (default: Tests/*.json).
    /// </summary>
    public IReadOnlyList<AvailableTest> GetAvailableTests()
    {
        var testsDir = Path.Combine(_basePath, "Tests");
        if (!Directory.Exists(testsDir))
            return Array.Empty<AvailableTest>();

        var list = new List<AvailableTest>();
        foreach (var file in Directory.EnumerateFiles(testsDir, "*.json"))
        {
            try
            {
                var def = LoadTestDefinition(file);
                if (def != null)
                    list.Add(new AvailableTest(file, def));
            }
            catch
            {
                // Skip invalid test files
            }
        }
        return list;
    }

    public TestDefinition? LoadTestDefinition(string path)
    {
        var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(_basePath, path);
        if (!File.Exists(fullPath))
            return null;
        var json = File.ReadAllText(fullPath);
        return JsonSerializer.Deserialize<TestDefinition>(json, JsonOptions);
    }

    /// <summary>
    /// Loads a question list from JSON (root is an array of question objects).
    /// </summary>
    /// <summary>
    /// Discovers question list files in the QuestionLists folder.
    /// </summary>
    public IReadOnlyList<AvailableQuestionList> GetAvailableQuestionLists()
    {
        var dir = Path.Combine(_basePath, "QuestionLists");
        if (!Directory.Exists(dir))
            return Array.Empty<AvailableQuestionList>();
        var list = new List<AvailableQuestionList>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
            list.Add(new AvailableQuestionList(file));
        return list;
    }

    public QuestionListFile? LoadQuestionList(string path)
    {
        var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(_basePath, path);
        if (!File.Exists(fullPath))
            return null;
        var json = File.ReadAllText(fullPath);
        var list = JsonSerializer.Deserialize<List<Question>>(json, JsonOptions);
        return list == null ? null : new QuestionListFile { Questions = list };
    }

    /// <summary>
    /// Loads a question list and returns each question with its stats (times answered, correct, weight).
    /// </summary>
    public IReadOnlyList<QuestionListQuestionDetail> GetQuestionListDetails(string questionListPath)
    {
        var file = LoadQuestionList(questionListPath);
        if (file == null || file.Questions.Count == 0)
            return Array.Empty<QuestionListQuestionDetail>();

        var stats = new QuestionStatsService();
        var weights = stats.GetWeightsSnapshot();
        var history = new HistoryService();
        var attemptStats = history.GetPerQuestionAttemptStats();

        var result = new List<QuestionListQuestionDetail>();
        foreach (var q in file.Questions)
        {
            var id = QuestionStatsService.GetQuestionId(q);
            var weight = QuestionStatsService.GetWeightFromSnapshot(weights, id);
            var (total, correct) = attemptStats.TryGetValue(id, out var t) ? t : (0, 0);
            result.Add(new QuestionListQuestionDetail(q.Statement, q.Answers, total, correct, weight));
        }
        return result;
    }

    /// <summary>
    /// Builds a new test session by loading question lists and picking questions by weighted random (past performance).
    /// </summary>
    public TestSession? CreateSession(TestDefinition definition)
    {
        var allPicked = new List<Question>();
        var rnd = new Random();
        var stats = new QuestionStatsService();
        var weightsSnapshot = stats.GetWeightsSnapshot();

        foreach (var source in definition.QuestionLists)
        {
            var list = LoadQuestionList(source.Path);
            if (list == null || list.Questions.Count == 0)
                continue;
            var take = Math.Min(source.Count, list.Questions.Count);
            var weighted = list.Questions
                .Select(q => (Question: q, Weight: QuestionStatsService.GetWeightFromSnapshot(weightsSnapshot, QuestionStatsService.GetQuestionId(q))))
                .ToList();
            var picked = WeightedSampleWithoutReplacement(weighted, take, rnd);
            allPicked.AddRange(picked);
        }

        if (allPicked.Count == 0)
            return null;

        allPicked = allPicked.OrderBy(_ => rnd.Next()).ToList();

        var session = new TestSession
        {
            Definition = definition,
            Questions = allPicked,
            UserSelections = allPicked.Select(_ => new HashSet<int>()).ToList(),
            MaxTime = TimeSpan.FromSeconds(definition.MaxTimeSeconds),
            StartedAt = DateTime.Now
        };
        return session;
    }

    /// <summary>
    /// Picks <paramref name="count"/> items from the list with probability proportional to weight (without replacement).
    /// </summary>
    private static List<Question> WeightedSampleWithoutReplacement(
        List<(Question Question, int Weight)> items,
        int count,
        Random rnd)
    {
        if (count <= 0) return new List<Question>();
        var remaining = items.Select(x => (Question: x.Question, Weight: Math.Max(1, x.Weight))).ToList();
        var result = new List<Question>();
        for (int k = 0; k < count && remaining.Count > 0; k++)
        {
            var total = remaining.Sum(x => x.Weight);
            var r = rnd.NextDouble() * total;
            double sum = 0;
            var idx = 0;
            for (var i = 0; i < remaining.Count; i++)
            {
                sum += remaining[i].Weight;
                if (r < sum) { idx = i; break; }
                idx = i;
            }
            result.Add(remaining[idx].Question);
            remaining.RemoveAt(idx);
        }
        return result;
    }

    /// <summary>
    /// Returns for each question whether the user's selection exactly matches the correct answers.
    /// </summary>
    public bool IsQuestionCorrect(Question q, HashSet<int> selectedIndices)
    {
        var correctIndices = new HashSet<int>();
        for (int i = 0; i < q.Answers.Count; i++)
            if (q.Answers[i].IsCorrect)
                correctIndices.Add(i);
        return correctIndices.SetEquals(selectedIndices);
    }

    public int CountCorrectAnswers(TestSession session)
    {
        int correct = 0;
        for (int i = 0; i < session.Questions.Count; i++)
            if (IsQuestionCorrect(session.Questions[i], session.UserSelections[i]))
                correct++;
        return correct;
    }

    public bool DidPass(TestSession session)
    {
        return CountCorrectAnswers(session) >= session.Definition.RequiredCorrectToPass;
    }
}
