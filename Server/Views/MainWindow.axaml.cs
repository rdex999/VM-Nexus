using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Server.ViewModels;
using Shared;

namespace Server.Views;

public partial class MainWindow : Window
{
	private bool _isClosing = false;
	
	public MainWindow()
	{
		InitializeComponent();
	}

	/// <summary>
	/// Handles the program exit event.
	/// </summary>
	/// <param name="e">The window closing event arguments. e != null.</param>
	/// <remarks>
	/// Precondition: Application exit has been requested. (user quit the application) e != null. <br/>
	/// Postcondition: Server shut down, application not running.
	/// </remarks>
	protected override void OnClosing(WindowClosingEventArgs e)
	{
		if (_isClosing) return;
		
		if (DataContext is MainWindowViewModel vm)
		{
			if (vm.ServerStateIsChecked)
			{
				e.Cancel = true;

				/* Run on the thread pool, not blocking main/UI thread. */ 
				Task.Run(async () =>
				{
					ExitCode result = await vm.MainWindowModel.ServerStopAsync();
					if (result == ExitCode.Success)
					{
						Dispatcher.UIThread.Post(() =>
						{
							vm.ServerStateIsChecked = false;
							_isClosing = true;
							Close();
						});
					}
				}).Wait();
			}
		}
		base.OnClosing(e);
	}
}