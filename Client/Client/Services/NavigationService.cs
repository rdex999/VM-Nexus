using Client.ViewModels;

namespace Client.Services;

public class NavigationService
{
	private MainViewModel _mainViewModel;

	public NavigationService()
	{
		_mainViewModel = null!;
	}

	/// <summary>
	/// Initialize the service.
	/// </summary>
	/// <param name="mainViewModel">The main view model. Used to switch view models. mainViewModel != null.</param>
	/// <remarks>
	/// Precondition: Application loaded and the MainWindow was created. mainViewModel != null. <br/>
	/// Postcondition: Service initialized and ready for operation.
	/// </remarks>
	public void Initialize(MainViewModel mainViewModel) => _mainViewModel = mainViewModel;

	/// <summary>
	/// Navigates to the given view model.
	/// </summary>
	/// <param name="viewModel">The view model to navigate to. viewModel != null.</param>
	/// <remarks>
	/// Precondition: Service initialized.  viewModel != null. <br/>
	/// Postcondition: The given view model is set as the current view. Meaning, the user now sees the given page. (view model)
	/// </remarks>
	public void NavigateTo(ViewModelBase viewModel)
	{
		_mainViewModel.CurrentViewModel = null!;
		_mainViewModel.CurrentViewModel = viewModel;
	}
}