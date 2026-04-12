using SharpConsoleUI;
using SharpConsoleUI.Builders;

namespace CXFiles.UI.Modals;

public abstract class ModalBase<TResult>
{
    protected Window? Modal { get; private set; }
    protected ConsoleWindowSystem WindowSystem { get; }
    protected Window? ParentWindow { get; }
    private TaskCompletionSource<TResult>? _tcs;

    protected ModalBase(ConsoleWindowSystem ws, Window? parentWindow = null)
    {
        WindowSystem = ws;
        ParentWindow = parentWindow;
    }

    public Task<TResult> ShowAsync()
    {
        _tcs = new TaskCompletionSource<TResult>();
        CreateModal();
        BuildContent();
        WindowSystem.AddWindow(Modal!);
        WindowSystem.SetActiveWindow(Modal!);
        return _tcs.Task;
    }

    protected void CloseWithResult(TResult result)
    {
        if (Modal != null)
        {
            WindowSystem.CloseWindow(Modal);
            Modal = null;
        }
        _tcs?.TrySetResult(result);
    }

    protected abstract void BuildContent();
    protected abstract TResult GetDefaultResult();

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
