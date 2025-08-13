using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
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

	private async void CloseVMTab_OnClick(object? sender, RoutedEventArgs e)
	{
		if (sender is not Button button)
		{
			return;
		}

		if (DataContext is not MainViewModel viewModel)
		{
			return;
		}

		if (button.DataContext is not VmTabTemplate tab)
		{
			return;
		}

		ListBoxItem? listBoxItem = button.FindAncestorOfType<ListBoxItem>();
		if (listBoxItem == null)
		{
			return;
		}

		Animation animation = new Animation()
		{
			Duration = TimeSpan.FromSeconds(0.3),
			FillMode = FillMode.Forward,
			Easing = Easing.Parse("CubicEaseInOut"),
			Children =
			{
				new KeyFrame()
				{
					Cue = new Cue(0.0),
					Setters =
					{
						new Setter(OpacityProperty, 1.0),
						new Setter(WidthProperty, listBoxItem.Bounds.Width),
					}
				},
				
				new KeyFrame()
				{
					Cue = new Cue(1.0),
					Setters =
					{
						new Setter(OpacityProperty, 0.0),
						new Setter(WidthProperty, 0.0),
					}
				}
			}
		};

		await animation.RunAsync(listBoxItem);

		viewModel.CloseVmTabCommand.Execute(tab);
	}

}