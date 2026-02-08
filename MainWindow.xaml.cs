using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Evaluator.Services;

namespace Evaluator
{
    public partial class MainWindow : Window
    {
        private readonly TestService _testService = new();
        private IReadOnlyList<AvailableTest> _tests = Array.Empty<AvailableTest>();
        private IReadOnlyList<AvailableQuestionList> _questionLists = Array.Empty<AvailableQuestionList>();
        private bool _updatingSelection;
        private List<QuestionListQuestionDetail>? _currentQuestionListDetails;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _tests = _testService.GetAvailableTests();
            _questionLists = _testService.GetAvailableQuestionLists();
            TestsListBox.ItemsSource = _tests;
            QuestionListsListBox.ItemsSource = _questionLists;
            StartButton.IsEnabled = false;
        }

        private void TestsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingSelection) return;
            if (TestsListBox.SelectedItem != null)
            {
                _updatingSelection = true;
                QuestionListsListBox.SelectedItem = null;
                _updatingSelection = false;
            }
            StartButton.IsEnabled = TestsListBox.SelectedItem != null;
            UpdateDetails();
        }

        private void QuestionListsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingSelection) return;
            if (QuestionListsListBox.SelectedItem != null)
            {
                _updatingSelection = true;
                TestsListBox.SelectedItem = null;
                _updatingSelection = false;
            }
            StartButton.IsEnabled = TestsListBox.SelectedItem != null;
            UpdateDetails();
        }

        private void UpdateDetails()
        {
            if (TestsListBox.SelectedItem is AvailableTest test)
            {
                DetailsTitle.Text = "Test info";
                UpdateTestInfo(test);
                return;
            }
            if (QuestionListsListBox.SelectedItem is AvailableQuestionList qlist)
            {
                DetailsTitle.Text = "Question list";
                UpdateQuestionListDetails(qlist);
                return;
            }
            DetailsTitle.Text = "Details";
            InfoPlaceholder.Visibility = Visibility.Visible;
            TestInfoPanel.Visibility = Visibility.Collapsed;
            QuestionListDetailPanel.Visibility = Visibility.Collapsed;
        }

        private void UpdateTestInfo(AvailableTest? available)
        {
            if (available == null)
            {
                InfoPlaceholder.Visibility = Visibility.Visible;
                TestInfoPanel.Visibility = Visibility.Collapsed;
                QuestionListDetailPanel.Visibility = Visibility.Collapsed;
                return;
            }
            var def = available.Definition;
            InfoPlaceholder.Visibility = Visibility.Collapsed;
            QuestionListDetailPanel.Visibility = Visibility.Collapsed;
            TestInfoPanel.Visibility = Visibility.Visible;

            var total = def.QuestionLists.Sum(q => q.Count);
            TotalQuestionsText.Text = $"Total questions: {total}";

            QuestionListBreakdown.ItemsSource = def.QuestionLists
                .Select(s => $"{s.Count} from {Path.GetFileName(s.Path)}")
                .ToList();

            RequiredCorrectText.Text = $"Required correct to pass: {def.RequiredCorrectToPass}";
            TimeLimitText.Text = $"Available time: {FormatTimeLimit(def.MaxTimeSeconds)}";

            var history = new HistoryService();
            var attempts = history.LoadAttempts(available.FilePath).ToList();
            if (attempts.Count == 0)
            {
                HistoryStatsText.Text = "No attempts yet.";
            }
            else
            {
                var passed = attempts.Count(a => a.Passed);
                var pct = attempts.Count == 0 ? 0 : (int)Math.Round(100.0 * passed / attempts.Count);
                var last = attempts.MaxBy(a => a.FinishedAt);
                var lastStr = last != null ? last.FinishedAt.ToString("g") : "—";
                var lastDurationStr = last != null ? FormatDuration(last.DurationSeconds) : "—";
                var avgSeconds = attempts.Average(a => a.DurationSeconds);
                var avgDurationStr = FormatDuration(avgSeconds);
                var best = attempts.MaxBy(a => a.CorrectCount);
                var bestStr = best != null ? $"{best.CorrectCount}/{best.TotalCount}" : "—";
                HistoryStatsText.Text = $"Attempts: {attempts.Count}\nPassed: {passed} ({pct}%)\nLast taken: {lastStr}\nLast duration: {lastDurationStr}\nAverage duration: {avgDurationStr}\nBest score: {bestStr}";
            }
        }

        private void UpdateQuestionListDetails(AvailableQuestionList? qlist)
        {
            if (qlist == null)
            {
                _currentQuestionListDetails = null;
                InfoPlaceholder.Visibility = Visibility.Visible;
                TestInfoPanel.Visibility = Visibility.Collapsed;
                QuestionListDetailPanel.Visibility = Visibility.Collapsed;
                return;
            }
            InfoPlaceholder.Visibility = Visibility.Collapsed;
            TestInfoPanel.Visibility = Visibility.Collapsed;
            QuestionListDetailPanel.Visibility = Visibility.Visible;

            _currentQuestionListDetails = _testService.GetQuestionListDetails(qlist.FilePath).ToList();
            var count = _currentQuestionListDetails.Count;
            QuestionListCountText.Text = count == 1 ? "1 question" : $"{count} questions";
            ApplyQuestionListSort();
        }

        private void ApplyQuestionListSort()
        {
            if (_currentQuestionListDetails == null) return;
            var index = QuestionListSortComboBox.SelectedIndex;
            IEnumerable<QuestionListQuestionDetail> sorted = index switch
            {
                1 => _currentQuestionListDetails.OrderBy(q => q.Weight),
                2 => _currentQuestionListDetails.OrderByDescending(q => q.Weight),
                _ => _currentQuestionListDetails
            };
            QuestionListDetailsItems.ItemsSource = sorted.ToList();
        }

        private void QuestionListSortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyQuestionListSort();
        }

        private static string FormatTimeLimit(int seconds)
        {
            if (seconds < 60) return $"{seconds} s";
            var m = seconds / 60;
            var s = seconds % 60;
            return s == 0 ? $"{m} min" : $"{m} min {s} s";
        }

        private static string FormatDuration(double totalSeconds)
        {
            if (totalSeconds < 60) return $"{(int)Math.Round(totalSeconds)} s";
            var m = (int)(totalSeconds / 60);
            var s = (int)Math.Round(totalSeconds % 60);
            return s == 0 ? $"{m} min" : $"{m} min {s} s";
        }

        private void TestsListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (TestsListBox.SelectedItem is AvailableTest)
                StartTest();
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            StartTest();
        }

        private void StartTest()
        {
            if (TestsListBox.SelectedItem is not AvailableTest available)
                return;
            var session = _testService.CreateSession(available.Definition);
            if (session == null)
            {
                MessageBox.Show("No questions could be loaded for this test. Check that question list paths exist.", "Cannot start", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var evalWindow = new EvaluationWindow(_testService, session, available.FilePath);
            evalWindow.Owner = this;
            evalWindow.ShowDialog();
            UpdateTestInfo(TestsListBox.SelectedItem as AvailableTest);
        }
    }
}
