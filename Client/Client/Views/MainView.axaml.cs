using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Client.ViewModels;

namespace Client.Views;

public partial class MainView : UserControl
{
	public MainView()
	{
		InitializeComponent();
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);

		if (DataContext is MainViewModel viewModel)
		{
			foreach (SideMenuItemTemplate sideMenuItem in viewModel.SideMenuItems)
			{
				if (this.TryFindResource(sideMenuItem.IconKey, out object? resource) && resource != null)
				{
					if (resource is Geometry geometry)
					{
						sideMenuItem.Icon = geometry;
					}
				}
			}
		}
	}
}