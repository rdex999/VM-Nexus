using System.Collections.ObjectModel;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Client.ViewModels;

public class SubUsersViewModel : ViewModelBase
{
	public ObservableCollection<SubUserItemTemplate> SubUsers { get; set; }
	
	public SubUsersViewModel(NavigationService navigationSvc, ClientService clientSvc) : base(navigationSvc, clientSvc)
	{
	}
}

public class SubUserItemTemplate : ObservableObject
{
	public SubUserItemTemplate()
	{
		
	}
}