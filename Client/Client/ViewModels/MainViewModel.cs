using Avalonia.Controls;
using Client.Services;
using Client.Views;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Client.ViewModels;

public partial class MainViewModel : ViewModelBase
{
	[ObservableProperty] 
	private ViewModelBase _currentPageViewModel;

	public MainViewModel(NavigationService navigationService, ClientService clientService)
		: base(navigationService, clientService)
	{
		CurrentPageViewModel = new HomeViewModel(navigationService, clientService);
	}
}