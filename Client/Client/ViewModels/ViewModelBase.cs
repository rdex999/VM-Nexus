using System;
using Avalonia.Threading;
using Client.Services;
using Client.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using Shared;

namespace Client.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
	protected readonly NavigationService NavigationSvc;
	protected readonly ClientService ClientSvc;

	[ObservableProperty] 
	private string _connectionErrorMessage = string.Empty;

	/// <summary>
	/// Initializes a new instance of ViewModelBase.
	/// </summary>
	/// <param name="navigationSvc">
	/// The navigation service. navigationSvc != null.
	/// </param>
	/// <param name="clientSvc">
	/// The client service. clientSvc != null.
	/// </param>
	/// <remarks>
	/// Precondition: A view model is instantiated. navigationSvc != null &amp;&amp; clientSvc != null. <br/>
	/// Postcondition: A new instance of ViewModelBase is created.
	/// </remarks>
	protected ViewModelBase(NavigationService navigationSvc, ClientService clientSvc)
	{
		NavigationSvc = navigationSvc;
		ClientSvc = clientSvc;

		ClientSvc.Reconnected += OnReconnection;
		ClientSvc.FailEvent += OnFailEvent;
	}

	/// <summary>
	/// Constructor for IDE preview only!! Services are unusable.
	/// </summary>
	/// <remarks>
	/// Precondition: IDE preview usage only. Not used is production. <br/>
	/// Postcondition: Properties set to null, object is created.
	/// </remarks>
	protected ViewModelBase()
	{
		NavigationSvc = null!;
		ClientSvc = null!;
	}

	/// <summary>
	/// Handles the reconnected event from the client server - occurs when the client service is reconnected to the server.
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	/// <remarks>
	/// Precondition: A connection to the server has been established. <br/>
	/// Postcondition: Any disconnection error messages that were visible are now hidden.
	/// </remarks>
	private void OnReconnection(object? sender, EventArgs e)
	{
		ConnectionErrorMessage = string.Empty;
	}

	/// <summary>
	/// Handles' failures. For example, if the client service is suddenly disconnected from the server.
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="status">
	/// The exit code - indicates the type of failure.
	/// </param>
	/// <remarks>
	/// Precondition: Failure of some kind has happened. The status parameter should hold the type of failure. <br/>
	/// Postcondition: Error messages are displayed as needed, user is redirected to different pages if needed.
	/// </remarks>
	private void OnFailEvent(object? sender, ExitCode status)
	{
		switch (status)
		{
			case ExitCode.ConnectionToServerFailed:
			{
				ConnectionErrorMessage = "Connection to server failed. Do you have internet?\nTrying to connect...";
				break;
			}

			case ExitCode.DisconnectedFromServer:
			{
				ConnectionErrorMessage = "Disconnected from the server. Do you have internet?\nTrying to connect...";
				if (this is not LoginViewModel && this is not CreateAccountViewModel)
				{
					Dispatcher.UIThread.Post(() =>
					{
						NavigationSvc.NavigateToLogin();
					});
				}
				break;
			}
		}
	}
}