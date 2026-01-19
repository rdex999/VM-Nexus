using Client.ViewModels;

namespace Client.Services;

public class NavigationService
{
	private MainViewModel _mainViewModel;
	private ClientService _clientService;
	
	public NavigationService(ClientService clientService)
	{
		_mainViewModel = null!;
		_clientService = clientService;
	}

	/// <summary>
	/// Initialize the service.
	/// </summary>
	/// <param name="mainViewModel">The main view model. Used to switch view models. mainViewModel != null.</param>
	/// <remarks>
	/// Precondition: Application loaded and the MainWindow was created. mainViewModel != null. <br/>
	/// Postcondition: Service initialized and ready for operation.
	/// </remarks>
	public void Initialize(MainViewModel mainViewModel)
	{
		_mainViewModel = mainViewModel;
		NavigateToLogin();
	}

	/// <summary>
	/// Navigates to the login page.
	/// </summary>
	/// <remarks>
	/// Precondition: Service initialized. <br/>
	/// Postcondition: The login page is shown.
	/// </remarks>
	public void NavigateToLogin() => NavigateTo(new LoginViewModel(this, _clientService));
	
	/// <summary>
	/// Navigates to the create account page.
	/// </summary>
	/// <remarks>
	/// Precondition: Service initialized. <br/>
	/// Postcondition: The create account page is shown.
	/// </remarks>
	public void NavigateToCreateAccount() => NavigateTo(new CreateAccountViewModel(this, _clientService));

	/// <summary>
	/// Navigates to the main page.
	/// </summary>
	/// <remarks>
	/// Precondition: Service initialized. <br/>
	/// Postcondition: The main page is shown.
	/// </remarks>
	public void NavigateToMainPage() => NavigateTo(new MainPageViewModel(this, _clientService));
	
	/// <summary>
	/// Navigates to the given view model.
	/// </summary>
	/// <param name="viewModel">The view model to navigate to. viewModel != null.</param>
	/// <remarks>
	/// Precondition: Service initialized.  viewModel != null. <br/>
	/// Postcondition: The given view model is set as the current view. Meaning, the user now sees the given page. (view model)
	/// </remarks>
	private void NavigateTo(ViewModelBase viewModel) => _mainViewModel.CurrentViewModel = viewModel;
}