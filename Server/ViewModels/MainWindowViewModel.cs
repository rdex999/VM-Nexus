using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
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
	
	[ObservableProperty]
	private Vector _logScrollPosition;

	[ObservableProperty] 
	private bool _logsFocused = false;

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
		LogScrollPosition = new Vector(0, 0);
		Directory.CreateDirectory("../../../Logs");
		Logger logger = new LoggerConfiguration()
			.MinimumLevel.Verbose()
			.WriteTo.Console()
			.WriteTo.File($"../../../Logs/{DateTime.Now:yyyy-MM-dd_HH:mm:ss}.log", restrictedToMinimumLevel: LogEventLevel.Information, 
				rollingInterval: RollingInterval.Infinite, fileSizeLimitBytes: 1 * 1024 * 1024 * 1024)
			.WriteTo.Sink(new LoggingSink(OnLog), LogEventLevel.Verbose)
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
		Dispatcher.UIThread.Post(() =>
		{
			Logs.Add(new LogItemTemplate(log));
			if (Logs.Count > 1000)
			{
				for (int i = 0; i < Logs.Count - 1000; ++i)
					Logs.RemoveAt(0);
			}

			if (!LogsFocused)
				LogScrollPosition = new Vector(0, double.PositiveInfinity);
		});
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
				ServerStateIsChecked = false;
		}
		else
		{
			ExitCode result = await MainWindowModel.ServerStopAsync();
			if (result != ExitCode.Success)
				ServerStateIsChecked = true;
		}
	}
}

public class LogItemTemplate
{
	public string Date { get; }
	public string Level { get; }
	public Brush LevelColor { get; }
	public string Source { get; }
	public string Message { get; }
	public LogItemTemplate(LogEvent log)
	{
		Date = log.Timestamp.ToString("dd-MM-yyyyy HH:mm:ss");
		Level = log.Level.ToString();
		Source = log.Properties.TryGetValue("Source", out var source) ? source.ToString().Trim('"') : "Server";
		Message = log.MessageTemplate.Text;

		LevelColor = log.Level switch
		{
			LogEventLevel.Verbose		=> new SolidColorBrush(Color.Parse("#c8c8c8")),
			LogEventLevel.Debug			=> new SolidColorBrush(Color.Parse("#4fe256")),
			LogEventLevel.Information	=> new SolidColorBrush(Color.Parse("#d0d0d0")),
			LogEventLevel.Warning		=> new SolidColorBrush(Color.Parse("#efac37")),
			LogEventLevel.Error			=> new SolidColorBrush(Color.Parse("#f23a3a")),
			LogEventLevel.Fatal			=> new SolidColorBrush(Color.Parse("#ff0000")),
			_ => new SolidColorBrush(Color.Parse("#000000"))
		};
	}
}