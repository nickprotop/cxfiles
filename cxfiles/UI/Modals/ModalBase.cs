using SharpConsoleUI;
using SharpConsoleUI.Builders;

namespace CXFiles.UI.Modals;

public enum ModalCloseReason { Escape, CloseButton, Explicit }

public abstract class ModalBase<TResult>
{
    protected Window? Modal { get; private set; }
    protected ConsoleWindowSystem WindowSystem { get; }
    protected Window? ParentWindow { get; }
    private TaskCompletionSource<TResult>? _tcs;
    private bool _closedExplicitly;

    protected ModalBase(ConsoleWindowSystem ws, Window? parentWindow = null)
    {
        WindowSystem = ws;
        ParentWindow = parentWindow;
    }

    public Task<TResult> ShowAsync()
    {
        _tcs = new TaskCompletionSource<TResult>();
        _closedExplicitly = false;
        CreateModal();
        BuildContent();

        Modal!.OnClosed += (_, _) =>
        {
            if (!_closedExplicitly)
                _tcs?.TrySetResult(OnClosedByButton());
        };

        WindowSystem.AddWindow(Modal);
        WindowSystem.SetActiveWindow(Modal);
        return _tcs.Task;
    }

    protected void CloseWithResult(TResult result)
    {
        _closedExplicitly = true;
        if (Modal != null)
        {
            WindowSystem.CloseWindow(Modal);
            Modal = null;
        }
        _tcs?.TrySetResult(result);
    }

    protected abstract void BuildContent();

    /// <summary>Result when Escape is pressed. Override to customize.</summary>
    protected abstract TResult GetDefaultResult();

    /// <summary>Result when the close button [X] is clicked. Defaults to GetDefaultResult().</summary>
    protected virtual TResult OnClosedByButton() => GetDefaultResult();

    protected virtual void CreateModal()
    {
        Modal = new WindowBuilder(WindowSystem)
            .WithTitle(GetTitle())
            .WithSize(GetWidth(), GetHeight())
            .Centered()
            .AsModal()
            .WithBorderStyle(BorderStyle.Rounded)
            .WithBorderColor(Color.Grey50)
            .OnKeyPressed((s, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    CloseWithResult(GetDefaultResult());
                    e.Handled = true;
                }
            })
            .Build();
    }

    protected virtual string GetTitle() => "";
    protected virtual int GetWidth() => 50;
    protected virtual int GetHeight() => 12;
}
