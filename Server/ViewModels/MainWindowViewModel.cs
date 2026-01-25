using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Server.Models;
using Shared;

namespace Server.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
	public readonly MainWindowModel MainWindowModel;
	public ObservableCollection<LogItemTemplate> Logs { get; set; }
	
	[ObservableProperty]
	private bool _serverStateIsChecked;

	private class LoggingSink : ILogEventSink
	{
		private readonly IFormatProvider? _fmt;
		private readonly Action<LogEvent>? _onLog;

		public LoggingSink(Action<LogEvent> onLog, IFormatProvider? fmt = null)
		{
			_onLog = onLog; 
			_fmt = fmt;
		}
		
		public void Emit(LogEvent logEvent) => _onLog?.Invoke(logEvent);
	}

	/// <summary>
	/// Creates an instance of MainWindowViewModel.
	/// </summary>
	/// <remarks>
	/// Precondition: No specific precondition. <br/>
	/// Postcondition: An instance of MainWindowViewModel is returned.
	/// </remarks>
	public MainWindowViewModel()
	{
		Logger logger = new LoggerConfiguration()
			.WriteTo.Console()
			.WriteTo.File($"../../../Logs/{DateTime.Now:yyyy-MM-dd_HH:mm:ss}.log", rollingInterval: RollingInterval.Infinite)
			.WriteTo.Sink(new LoggingSink(OnLog))
			.CreateLogger();

		Logs = new ObservableCollection<LogItemTemplate>();
		MainWindowModel = new MainWindowModel(logger);
	}

	/// <summary>
	/// Handles a log. Called each time a new log was logged.
	/// </summary>
	/// <param name="log">The log. log != null.</param>
	/// <remarks>
	/// Precondition: A log was logged. log != null. <br/>
	/// Postcondition: The log is displayed.
	/// </remarks>
	private void OnLog(LogEvent log)
	{
		Logs.Add(new LogItemTemplate(log.MessageTemplate.Text));
	}
	
	/// <summary>
	/// Handles a toggle of the server on/off button.
	/// </summary>
	/// <param name="isToggled">
	/// Indicates whether the toggle event is for toggling on or off.
	/// </param>
	/// <remarks>
	/// Precondition: User has toggled the server on/off toggle button. <br/>
	/// Postcondition: The server will attempt startup/shutdown. (based on if its a toggle on or off respectively)
	/// If the server fails to start/ shutdown, the server on/off toggle button will not change its toggle state.
	/// </remarks>
	[RelayCommand]
	private async Task ServerStateToggleChangedAsync(bool isToggled)
	{
		if (isToggled)
		{
			ExitCode result = await MainWindowModel.ServerStartAsync();
			if (result != ExitCode.Success)
			{
				/* TODO: Add logic to display error message */
				ServerStateIsChecked = false;
			}
		}
		else
		{
			ExitCode result = await MainWindowModel.ServerStopAsync();
			if (result != ExitCode.Success)
			{
				/* TODO: Add logic to display error message */
				ServerStateIsChecked = true;
			}
		}
	}
}

public class LogItemTemplate
{
	public string Message { get; }
	public LogItemTemplate(string message)
	{
		Message = message;
	}
}