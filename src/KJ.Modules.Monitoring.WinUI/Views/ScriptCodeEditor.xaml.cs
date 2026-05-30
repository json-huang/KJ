using System.Collections.ObjectModel;
using KJ.Workflows;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace KJ.Modules.Monitoring.Views;

public sealed partial class ScriptCodeEditor : UserControl
{
    private static readonly ScriptIntelliSenseService IntelliSense = new();

    private readonly ObservableCollection<ScriptCompletionItem> _completionItems = new();
    private CancellationTokenSource? _completionCts;
    private bool _suppressTextChanged;
    private int _completionCaretPosition;

    public ScriptCodeEditor()
    {
        InitializeComponent();
        CompletionList.ItemsSource = _completionItems;
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(ScriptCodeEditor),
            new PropertyMetadata(string.Empty, OnTextPropertyChanged));

    public string ReferencesText
    {
        get => (string)GetValue(ReferencesTextProperty);
        set => SetValue(ReferencesTextProperty, value);
    }

    public static readonly DependencyProperty ReferencesTextProperty =
        DependencyProperty.Register(nameof(ReferencesText), typeof(string), typeof(ScriptCodeEditor), new PropertyMetadata(string.Empty));

    private static void OnTextPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScriptCodeEditor editor)
            return;

        if (editor._suppressTextChanged)
            return;

        var text = e.NewValue as string ?? string.Empty;
        if (editor.Editor.Text != text)
            editor.Editor.Text = text;
    }

    private void OnEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressTextChanged)
            return;

        _suppressTextChanged = true;
        Text = Editor.Text;
        _suppressTextChanged = false;

        if (Editor.Text.Length > 0 &&
            Editor.SelectionStart > 0 &&
            Editor.Text[Editor.SelectionStart - 1] == '.')
        {
            _ = RequestCompletionAsync(triggerChar: '.');
        }
    }

    private void OnEditorSelectionChanged(object sender, RoutedEventArgs e)
    {
    }

    private async void OnEditorKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (CompletionHost.Visibility == Visibility.Visible)
        {
            switch (e.Key)
            {
                case VirtualKey.Escape:
                    HideCompletion();
                    e.Handled = true;
                    return;
                case VirtualKey.Up:
                    MoveCompletionSelection(-1);
                    e.Handled = true;
                    return;
                case VirtualKey.Down:
                    MoveCompletionSelection(1);
                    e.Handled = true;
                    return;
                case VirtualKey.Tab:
                case VirtualKey.Enter:
                    await ApplySelectedCompletionAsync();
                    e.Handled = true;
                    return;
            }
        }

        if (e.Key == VirtualKey.Space &&
            Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
        {
            e.Handled = true;
            await RequestCompletionAsync(triggerChar: null);
        }
    }

    private void MoveCompletionSelection(int delta)
    {
        if (_completionItems.Count == 0)
            return;

        var index = CompletionList.SelectedIndex;
        if (index < 0)
            index = 0;
        else
            index = Math.Clamp(index + delta, 0, _completionItems.Count - 1);

        CompletionList.SelectedIndex = index;
        CompletionList.ScrollIntoView(CompletionList.SelectedItem);
    }

    private async Task RequestCompletionAsync(char? triggerChar)
    {
        _completionCts?.Cancel();
        _completionCts = new CancellationTokenSource();
        var token = _completionCts.Token;

        try
        {
            await Task.Delay(triggerChar == '.' ? 120 : 0, token);

            var code = Editor.Text ?? string.Empty;
            var position = Editor.SelectionStart;
            if (position < 0)
                position = code.Length;

            _completionCaretPosition = position;
            var refs = ScriptReferenceBuilder.ParseReferenceLines(ReferencesText);
            var items = await Task.Run(() => IntelliSense.GetCompletions(code, position, refs), token).ConfigureAwait(true);

            if (token.IsCancellationRequested)
                return;

            _completionItems.Clear();
            foreach (var item in items)
                _completionItems.Add(item);

            if (_completionItems.Count == 0)
            {
                HideCompletion();
                return;
            }

            CompletionList.SelectedIndex = 0;
            CompletionHost.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
    }

    private async void OnCompletionItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ScriptCompletionItem item)
            await ApplyCompletionAsync(item);
    }

    private void OnCompletionSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
    }

    private async Task ApplySelectedCompletionAsync()
    {
        if (CompletionList.SelectedItem is ScriptCompletionItem item)
            await ApplyCompletionAsync(item);
    }

    private async Task ApplyCompletionAsync(ScriptCompletionItem item)
    {
        var code = Editor.Text ?? string.Empty;
        var updated = await Task.Run(() => IntelliSense.ApplyCompletion(code, _completionCaretPosition, item)).ConfigureAwait(true);

        _suppressTextChanged = true;
        Editor.Text = updated;
        Text = updated;
        _suppressTextChanged = false;

        var newCaret = Math.Min(updated.Length, _completionCaretPosition + item.InsertText.Length);
        Editor.SelectionStart = newCaret;
        Editor.SelectionLength = 0;
        Editor.Focus(FocusState.Programmatic);

        HideCompletion();
    }

    private void HideCompletion()
    {
        CompletionHost.Visibility = Visibility.Collapsed;
        _completionItems.Clear();
    }
}
