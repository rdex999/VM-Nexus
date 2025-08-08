using System;
using System.Threading.Tasks;
using Client.Services;
using Client.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Client.ViewModels;

public partial class CreateAccountViewModel : ViewModelBase
{
	private readonly NavigationService _navigationService;
	private ClientService _clientService;

	[ObservableProperty] 
	private string _username;
	
	[ObservableProperty]
	private string _password;
	
	[ObservableProperty]
	private string _passwordConfirm;

	[ObservableProperty]
	private bool _passwordNoteIsVisible = false;

	[ObservableProperty] 
	private bool _passwordClassError = false;

	[ObservableProperty] 
	private bool _passwordClassSuccess = false;
	
	public CreateAccountViewModel(NavigationService navigationService,  ClientService clientService)
	{
		_navigationService = navigationService;
		_clientService = clientService;
	}


	[RelayCommand]
	private void NavigateToLogin()
	{
		_navigationService.NavigateToView(new LoginView() {  DataContext = new LoginViewModel(_navigationService, _clientService) });
	}

	[RelayCommand]
	private async Task CreateAccountAsync()
	{
		throw new NotImplementedException();
	}

	public async Task UsernameTextChangedAsync()
	{
		/* TODO: Search database for the username, to display a message if the username is available or not */
		throw new NotImplementedException();
	}
	
	public void PasswordTextChanged()
	{
		if (Password == PasswordConfirm && Password.Length == 0)
		{
			PasswordNoteIsVisible = false;
			PasswordClassError = false;	
			PasswordClassSuccess = false;
		}
		else if (Password == PasswordConfirm)
		{
			PasswordNoteIsVisible = false;
			PasswordClassError = false;
			PasswordClassSuccess = true;
		}
		else
		{
			PasswordNoteIsVisible = true;
			PasswordClassError = true;
			PasswordClassSuccess = false;
		}
	}
}