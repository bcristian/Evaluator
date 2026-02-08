# Evaluator

A WPF desktop application for practicing For practicing multiple‑choice tests.
You can define your tests by specifying question lists, time limits, and pass thresholds in JSON files.
Questions are selected randomly for each attempt, with adaptive weighting to focus on areas where you need improvement.

The included tests are for Romanian boat license categories C and D.

## Features

- **Practice tests** — Take official-style exams with time limits and pass/fail thresholds
- **Question lists** — Review questions, see correct answers, and track your performance on each topic
- **Adaptive weighting** — Questions you answer incorrectly get higher weight for future practice
- **Attempt history** — Track attempts, pass rate, average duration, and best scores per test

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Building

```bash
dotnet build
```

## Running

```bash
dotnet run
```

Or run the executable.

## Project structure

```
Evaluator/
├── MainWindow.xaml          # Main window: test/question list selection, details
├── EvaluationWindow.xaml    # Exam UI during a test
├── Models/                  # Data models (Question, TestDefinition, etc.)
├── Services/                # TestService, HistoryService, QuestionStatsService
├── ViewModels/              # View models for data binding
├── QuestionLists/           # JSON question files by topic
│   ├── colreg.json
│   ├── legislatie.json
│   ├── manevra.json
│   ├── marinarie.json
│   ├── navigatie.json
│   ├── prim ajutor.json
│   └── RND.json
├── Tests/                   # Test definitions (time limit, required correct, question counts)
│   ├── categoria C.json
│   └── categoria D.json
```

## Adding questions

Question lists are JSON files in `QuestionLists/` with this format:

```json
[
  {
    "Statement": "Question text?",
    "Answers": [
      { "Text": "Answer A", "IsCorrect": false },
      { "Text": "Answer B", "IsCorrect": true }
    ]
  }
]
```

## Adding tests

Create a JSON file in `Tests/` with this structure:

```json
{
  "Name": "Test display name",
  "MaxTimeSeconds": 1800,
  "RequiredCorrectToPass": 22,
  "QuestionLists": [
    { "Path": "QuestionLists\\colreg.json", "Count": 10 },
    { "Path": "QuestionLists\\marinarie.json", "Count": 4 }
  ]
}
```

- `Path` — Relative to the executable (or project root when running via `dotnet run`)
- `Count` — Number of questions to draw from each list per attempt

## License

This project is open source and licensed under the [MIT License](LICENSE).
