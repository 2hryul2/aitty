using System.Windows;
using System.Windows.Input;
using Aitty.Services;

namespace Aitty;

public partial class MainWindow : Window
{
    private readonly SshService _sshService;
    private readonly ConfigService _configService;
    private readonly KeyManagerService _keyManagerService;
    private readonly ClaudeApiService _claudeApiService;

    public MainWindow()
    {
        InitializeComponent();

        _sshService        = new SshService();
        _configService     = new ConfigService();
        _keyManagerService = new KeyManagerService();
        _claudeApiService  = new ClaudeApiService();

        Loaded += MainWindow_Loaded;

        // Ctrl+Q
        InputBindings.Add(new KeyBinding(
            new RelayCommand(_ => Close()),
            new KeyGesture(Key.Q, ModifierKeys.Control)));
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Inject services into UserControls
        SshTerminal.Initialize(_sshService, _configService, _keyManagerService);
        AiTerminal.Initialize(_claudeApiService, _configService);
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e) => Close();

    private void MenuResetLayout_Click(object sender, RoutedEventArgs e)
    {
        MainGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
        MainGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
    }

    protected override void OnClosed(EventArgs e)
    {
        _sshService.Dispose();
        _claudeApiService.Dispose();
        base.OnClosed(e);
    }
}

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    public RelayCommand(Action<object?> execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute(parameter);
}
