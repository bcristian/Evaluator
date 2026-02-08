namespace Evaluator.Models;

/// <summary>
/// A running or completed test session: selected questions and user's selected answers.
/// </summary>
public class TestSession
{
    public TestDefinition Definition { get; set; } = new();
    public List<Question> Questions { get; set; } = new();
    /// <summary>For each question, indices of answers the user selected (by index in Question.Answers).</summary>
    public List<HashSet<int>> UserSelections { get; set; } = new();
    public TimeSpan MaxTime { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}
