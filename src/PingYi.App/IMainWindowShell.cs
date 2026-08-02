namespace PingYi.App;

public interface IMainWindowShell
{
    bool IsVisible { get; }

    void Show();

    void Hide();

    void Activate();

    void SetGlobalStatus(string message, bool isError);
}
