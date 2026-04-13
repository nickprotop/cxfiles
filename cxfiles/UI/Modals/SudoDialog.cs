using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace CXFiles.UI.Modals;

public static class SudoDialog
{
    public static bool IsOpen { get; private set; }

    public class SudoResult
    {
        public bool Success { get; init; }
        public string? Password { get; init; }
        public bool Cancelled { get; init; }
    }

    public static void Show(
        string operationDescription,
        ConsoleWindowSystem windowSystem,
        Action<SudoResult> onResult)
    {
        var windowStates = new Dictionary<Window, WindowState>();
        foreach (var window in windowSystem.Windows.Values.ToList())
        {
            windowStates[window] = window.State;
            if (window.State != WindowState.Minimized)
                window.Minimize(force: true);
        }

        var modal = new WindowBuilder(windowSystem)
            .WithSize(60, 17)
            .Centered()
            .AsModal()
            .WithBorderStyle(BorderStyle.DoubleLine)
            .WithBorderColor(Color.Orange1)
            .HideTitle()
            .Resizable(false)
            .Movable(false)
            .Minimizable(false)
            .Maximizable(false)
            .WithColors(Color.Grey93, Color.Grey11)
            .Build();

        IsOpen = true;
        var dialogComplete = false;
        SudoResult? result = null;

        modal.AddControl(Controls.Markup()
            .AddLine("")
            .AddLine("[bold orange1]\U0001F512 Authentication Required[/]")
            .WithAlignment(HorizontalAlignment.Center)
            .Build());

        modal.AddControl(Controls.Markup()
            .AddLine("")
            .AddLine($"[grey70]{MarkupParser.Escape(operationDescription)}[/]")
            .AddLine("")
            .WithAlignment(HorizontalAlignment.Center)
            .Build());

        var passwordPrompt = new PromptControl();
        passwordPrompt.Prompt = "  Password: ";
        passwordPrompt.MaskCharacter = '\u2022';
        passwordPrompt.InputWidth = 30;
        passwordPrompt.InputBackgroundColor = Color.Grey19;
        passwordPrompt.InputFocusedBackgroundColor = Color.Grey23;
        passwordPrompt.UnfocusOnEnter = false;
        passwordPrompt.HorizontalAlignment = HorizontalAlignment.Center;
        modal.AddControl(passwordPrompt);

        modal.AddControl(Controls.Markup()
            .AddLine("")
            .AddLine("[grey50]Password is used once and immediately discarded.[/]")
            .WithAlignment(HorizontalAlignment.Center)
            .Build());

        var authBtn = Controls.Button(" Authenticate ")
            .Build();
        var cancelBtn = Controls.Button("   Cancel   ")
            .WithMargin(2, 0, 0, 0)
            .Build();

        var buttonGrid = HorizontalGridControl.ButtonRow(authBtn, cancelBtn);
        modal.AddControl(Controls.Markup().AddLine("").Build());
        modal.AddControl(buttonGrid);

        modal.AddControl(Controls.Markup()
            .AddLine("")
            .AddLine("[grey50]Enter: Authenticate  \u2022  Esc: Cancel[/]")
            .WithAlignment(HorizontalAlignment.Center)
            .StickyBottom()
            .Build());

        void RestoreWindows()
        {
            foreach (var kvp in windowStates)
            {
                if (kvp.Value != WindowState.Minimized)
                    kvp.Key.Restore();
            }
        }

        void CompleteDialog(SudoResult r)
        {
            if (dialogComplete) return;
            dialogComplete = true;
            result = r;
            passwordPrompt.Input = "";
            modal.Close();
        }

        void DoAuthenticate()
        {
            var password = passwordPrompt.Input;
            CompleteDialog(new SudoResult { Success = true, Password = password });
        }

        authBtn.Click += (_, _) => DoAuthenticate();
        cancelBtn.Click += (_, _) => CompleteDialog(new SudoResult { Cancelled = true });

        modal.KeyPressed += (_, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.Enter)
            {
                DoAuthenticate();
                e.Handled = true;
            }
            else if (e.KeyInfo.Key == ConsoleKey.Escape)
            {
                CompleteDialog(new SudoResult { Cancelled = true });
                e.Handled = true;
            }
        };

        modal.OnClosed += (_, _) =>
        {
            IsOpen = false;
            RestoreWindows();
            result ??= new SudoResult { Cancelled = true };
            onResult(result);
        };

        windowSystem.AddWindow(modal);
        windowSystem.SetActiveWindow(modal);
        modal.FocusManager.SetFocus(passwordPrompt, FocusReason.Programmatic);
    }
}
