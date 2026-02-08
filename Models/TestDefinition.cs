namespace Evaluator.Models;

/// <summary>
/// Test definition as stored in JSON: time limit, pass threshold, and which question lists to use.
/// </summary>
public class TestDefinition
{
    /// <summary>Optional display name (defaults to test file name).</summary>
    public string? Name { get; set; }

    /// <summary>Maximum allowed time in seconds.</summary>
    public int MaxTimeSeconds { get; set; }

    /// <summary>Number of correct answers required to pass.</summary>
    public int RequiredCorrectToPass { get; set; }

    /// <summary>Question list path (relative to app or absolute) and how many to pick.</summary>
    public List<QuestionListSource> QuestionLists { get; set; } = new();
}

public class QuestionListSource
{
    /// <summary>Path to the question list JSON file.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Number of questions to randomly pick from this list.</summary>
    public int Count { get; set; }
}
