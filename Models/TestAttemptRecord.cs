namespace Evaluator.Models;

/// <summary>
/// One completed test attempt, for persistent history (JSON).
/// </summary>
public class TestAttemptRecord
{
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
    /// <summary>Duration in seconds.</summary>
    public double DurationSeconds { get; set; }
    public bool Passed { get; set; }
    public int CorrectCount { get; set; }
    public int TotalCount { get; set; }
    public int RequiredToPass { get; set; }
    /// <summary>Questions as shown in this attempt (statement + answers).</summary>
    public List<QuestionSnapshot> Questions { get; set; } = new();
    /// <summary>For each question, indices of answers the user selected.</summary>
    public List<List<int>> UserSelections { get; set; } = new();
}

public class QuestionSnapshot
{
    public string Statement { get; set; } = string.Empty;
    public List<QuestionAnswer> Answers { get; set; } = new();
}
