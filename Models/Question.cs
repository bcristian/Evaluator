namespace Evaluator.Models;

/// <summary>
/// A single question with a statement and multiple possible answers (one or more may be correct).
/// </summary>
public class Question
{
    public string Statement { get; set; } = string.Empty;
    public List<QuestionAnswer> Answers { get; set; } = new();
}

public class QuestionAnswer
{
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
}
