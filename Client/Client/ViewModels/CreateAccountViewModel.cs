using System.Threading.Tasks;
using Client.Services;
using CommunityToolkit.Mvvm.Input;

namespace Client.ViewModels;

public partial class CreateAccountViewModel : ViewModelBase
{
	private readonly NavigationService _navigationService;

	public CreateAccountViewModel(NavigationService navigationService)
	{
		_navigationService = navigationService;
	}
}