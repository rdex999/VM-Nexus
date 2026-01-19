using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Client.Services;
using Client.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared;
using Shared.Networking;
using Shared.VirtualMachines;
using OperatingSystem = System.OperatingSystem;

namespace Client.ViewModels;

public partial class MainViewModel : ViewModelBase
{
	[ObservableProperty]
	private ViewModelBase _currentViewModel;

	public MainViewModel(NavigationService navigationService, ClientService clientService)
		: base(navigationService, clientService)
	{
		CurrentViewModel = null!;
	}

	/* Use for IDE preview only. */
	public MainViewModel()
	{
		_currentViewModel = null!;
	}
}
