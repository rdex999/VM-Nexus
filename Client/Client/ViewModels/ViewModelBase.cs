using System;
using Avalonia;
using Avalonia.Platform;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Shared;

namespace Client.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
	protected readonly NavigationService _navigationService;
	protected ClientService _clientService;

	[ObservableProperty] 
	private string _connectionErrorMessage;
	
	public ViewModelBase(NavigationService navigationService, ClientService clientService)
	{
		_navigationService = navigationService;
		_clientService = clientService;

		_clientService.Reconnected += OnReconnection;
		_clientService.FailEvent += OnFailEvent;
	}

	private void OnReconnection(object? sender, EventArgs e)
	{
		ConnectionErrorMessage = string.Empty;
	}
	
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
				break;
			}

			default:
			{
				break;
			}
		}
	}
}