using System;
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

	/// <summary>
	/// Called when this view is shown - meaning DataContext is set and can be used.
	/// </summary>
	/// <param name="e"></param>
	/// <remarks>
	/// Precondition: This view is attached to the visual tree - meaning its visible. <br/>
	/// Postcondition: Side menu icons are set.
	/// </remarks>
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

	/// <summary>
	/// Called when the user closes a VM tab. Animates the tab closing, and then calls the view model's handling method for closing a VM tab.
	/// </summary>
	/// <param name="sender">
	/// The "close" button on the tab (its an X)
	/// </param>
	/// <param name="e"></param>
	/// <remarks>
	/// Precondition: The user clicked the close button on a VM tab. sender is a Button, sender != null. <br/>
	/// Postcondition: Tab closed completely.
	/// </remarks>
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

		/* If its the last tab available */
		if (viewModel.VmTabs.Count == 1)
		{
			animation.Children[0].Setters.Add(new Setter(HeightProperty, listBoxItem.Bounds.Height));
			animation.Children[1].Setters.Add(new Setter(HeightProperty, 0.0));
		}

		await animation.RunAsync(listBoxItem);

		viewModel.CloseVmTab(tab);
	}
}