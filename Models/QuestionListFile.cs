namespace Evaluator.Models;

/// <summary>
/// JSON file format: array of questions.
/// </summary>
public class QuestionListFile
{
    public List<Question> Questions { get; set; } = new();
}
