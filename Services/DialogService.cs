namespace MyActivity.Services;

public interface IDialogService
{
    Task AlertAsync(string title, string message, string ok = "OK");
    Task<bool> ConfirmAsync(string title, string message, string accept = "Yes", string cancel = "Cancel");
}

public class DialogService : IDialogService
{
    private static Page? CurrentPage => Application.Current?.Windows.FirstOrDefault()?.Page;

    public Task AlertAsync(string title, string message, string ok = "OK") =>
        CurrentPage?.DisplayAlertAsync(title, message, ok) ?? Task.CompletedTask;

    public Task<bool> ConfirmAsync(string title, string message, string accept = "Yes", string cancel = "Cancel") =>
        CurrentPage?.DisplayAlertAsync(title, message, accept, cancel) ?? Task.FromResult(false);
}
