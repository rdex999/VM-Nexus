using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Client.Services;
using Client.Views;


namespace Client.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
	private readonly NavigationService _navigationService;
	private CommunicationService _communicationService;

	public LoginViewModel(NavigationService navigationService, CommunicationService communicationService)
	{
		_navigationService = navigationService;
		_communicationService = communicationService;
	}

	public async Task Initialize()
	{
		if (IsInitialized())
			return;
		
		await _communicationService.Initialize();
	}

	public bool IsInitialized()
	{
		return _communicationService.IsInitialized();
	}

	[RelayCommand]
	private void NavigateToCreateAccount()
	{
		_navigationService.NavigateToView(new CreateAccountView() { DataContext = new CreateAccountViewModel(_navigationService, _communicationService) });
	}
}