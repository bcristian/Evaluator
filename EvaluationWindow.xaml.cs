using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Evaluator.Models;
using Evaluator.Services;
using Evaluator.ViewModels;

namespace Evaluator
{
    public partial class EvaluationWindow : Window
    {
        private readonly TestService _testService;
        private readonly TestSession _session;
        private readonly string _testFilePath;
        private readonly System.Windows.Threading.DispatcherTimer _timer;
        private readonly HashSet<int> _visited = new();
        private int _currentIndex;
        private List<AnswerOptionViewModel>? _currentAnswerViewModels;
        private bool _finished;
        private bool _reviewMode;

        public EvaluationWindow(TestService testService, TestSession session, string testFilePath)
        {
            InitializeComponent();
            _testService = testService;
            _session = session;
            _testFilePath = testFilePath;
            _currentIndex = 0;
            _timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
            Loaded += (_, _) => RefreshTime();
            ShowQuestion(0);
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            RefreshTime();
            var elapsed = DateTime.Now - _session.StartedAt;
            if (elapsed >= _session.MaxTime && !_finished)
                EndEvaluation();
        }

        private void RefreshTime()
        {
            var elapsed = DateTime.Now - _session.StartedAt;
            var remaining = _session.MaxTime - elapsed;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            ElapsedText.Text = FormatTime(elapsed);
            RemainingText.Text = remaining == TimeSpan.Zero ? "0:00" : FormatTime(remaining);
        }

        private static string FormatTime(TimeSpan t)
        {
            var total = (int)t.TotalSeconds;
            var m = total / 60;
            var s = total % 60;
            return $"{m}:{s:D2}";
        }

        private void SaveCurrentSelections()
        {
            if (_currentAnswerViewModels == null || _currentIndex < 0 || _currentIndex >= _session.UserSelections.Count)
                return;
            var set = new HashSet<int>();
            foreach (var vm in _currentAnswerViewModels.Where(vm => vm.IsSelected))
                set.Add(vm.Index);
            _session.UserSelections[_currentIndex] = set;
        }

        private void ShowQuestion(int index)
        {
            SaveCurrentSelections();
            _currentIndex = index;
            if (_session.Questions.Count == 0) return;

            _visited.Add(_currentIndex);
            UpdateFinishButtonState();

            var q = _session.Questions[_currentIndex];
            QuestionNumberText.Text = $"Question {_currentIndex + 1} of {_session.Questions.Count}";
            StatementText.Text = q.Statement;

            _currentAnswerViewModels = new List<AnswerOptionViewModel>();
            var selected = _session.UserSelections[_currentIndex];
            for (int i = 0; i < q.Answers.Count; i++)
                _currentAnswerViewModels.Add(new AnswerOptionViewModel
                {
                    Text = q.Answers[i].Text,
                    Index = i,
                    IsSelected = selected.Contains(i),
                    IsCorrect = _reviewMode && q.Answers[i].IsCorrect,
                    IsReadOnly = _reviewMode
                });
            AnswersItems.ItemsSource = _currentAnswerViewModels;

            PrevButton.IsEnabled = _currentIndex > 0;
            NextButton.IsEnabled = _currentIndex < _session.Questions.Count - 1;
        }

        private void UpdateFinishButtonState()
        {
            if (_reviewMode) return;
            FinishButton.IsEnabled = _visited.Count >= _session.Questions.Count;
        }

        private bool IsQuestionWrong(int index)
        {
            if (index < 0 || index >= _session.Questions.Count) return false;
            var q = _session.Questions[index];
            var correctIndices = new HashSet<int>();
            for (int i = 0; i < q.Answers.Count; i++)
                if (q.Answers[i].IsCorrect)
                    correctIndices.Add(i);
            var selected = _session.UserSelections[index];
            return !correctIndices.SetEquals(selected);
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (_reviewMode && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            {
                for (int i = _currentIndex - 1; i >= 0; i--)
                {
                    if (IsQuestionWrong(i)) { ShowQuestion(i); return; }
                }
                return;
            }
            if (_currentIndex > 0) ShowQuestion(_currentIndex - 1);
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_reviewMode && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            {
                for (int i = _currentIndex + 1; i < _session.Questions.Count; i++)
                {
                    if (IsQuestionWrong(i)) { ShowQuestion(i); return; }
                }
                return;
            }
            if (_currentIndex < _session.Questions.Count - 1) ShowQuestion(_currentIndex + 1);
        }

        private void FinishButton_Click(object sender, RoutedEventArgs e)
        {
            if (_reviewMode)
            {
                Close();
                return;
            }
            EndEvaluation();
        }

        private void EndEvaluation()
        {
            if (_finished) return;
            _finished = true;
            _timer.Stop();
            SaveCurrentSelections();
            _session.FinishedAt = DateTime.Now;

            var correct = _testService.CountCorrectAnswers(_session);
            var passed = _testService.DidPass(_session);
            var history = new HistoryService();
            history.SaveAttempt(_testFilePath, _session, correct, passed);

            _reviewMode = true;
            TimePanel.Visibility = Visibility.Collapsed;
            ResultPanel.Visibility = Visibility.Visible;
            ResultTitleText.Text = passed ? "Passed" : "Not passed";
            ResultScoreText.Text = $"Correct: {correct} of {_session.Questions.Count} (required: {_session.Definition.RequiredCorrectToPass})";
            var duration = (_session.FinishedAt!.Value - _session.StartedAt);
            ResultTimeText.Text = $"Time taken: {FormatTime(duration)}";
            ResultTimeText.Margin = new Thickness(0, 2, 0, 0);

            FinishButton.Content = "Close";
            AnswersLabel.Text = "Your answers (correct answers highlighted):";
            PrevButton.ToolTip = "Previous question (hold Shift: previous wrong answer)";
            NextButton.ToolTip = "Next question (hold Shift: next wrong answer)";

            ShowQuestion(_currentIndex);
        }
    }
}
