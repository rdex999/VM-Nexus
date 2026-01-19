using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Client.ViewModels;

namespace Client.Views;

public partial class MainPageView : UserControl
{
	public MainPageView()
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

		if (DataContext is MainPageViewModel viewModel)
		{
			viewModel.VmTabs.CollectionChanged += VmTabsOnCollectionChangedAsync;
			viewModel.SideMenuItems.CollectionChanged += SideMenuItemsOnCollectionChanged;
			
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
	/// Handles a change in the side menu items - specifically, addition. Loads the icon geometry for the added items.
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="e">The type of change that has occured.</param>
	/// <remarks>
	/// Precondition: User has opened a VM tab or switched to another VM tab. <br/>
	/// Postcondition: Necessary side menu items are added - their icon geometry is loaded.
	/// </remarks>
	private void SideMenuItemsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.Action != NotifyCollectionChangedAction.Add)
		{
			return;
		}
		
		foreach (SideMenuItemTemplate sideMenuItem in e.NewItems!)
		{
			if (sideMenuItem.Icon == null && this.TryFindResource(sideMenuItem.IconKey, out object? resource) && resource != null)
			{
				if (resource is Geometry geometry)
				{
					sideMenuItem.Icon = geometry;
				}
			}
		}
	}

	/// <summary>
	/// Handles a change in the VM tabs. Adds an opening animation when opening a tab.
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="e">The type of change that has occured.</param>
	/// <remarks>
	/// Precondition: User has opened a tab. <br/>
	/// Postcondition: Animation has run, the tab is closed.
	/// </remarks>
	private async void VmTabsOnCollectionChangedAsync(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.Action != NotifyCollectionChangedAction.Add)
		{
			return;
		}

		ListBox? listBox = this.FindControl<ListBox>("VmTabsListBox");
		if (listBox == null)
		{
			return;
		}

		if (DataContext is not MainPageViewModel viewModel)
		{
			return;
		}
	
		/* e.NewItems cannot be null because we know its an Add event. */
		foreach (VmTabTemplate tab in e.NewItems!)
		{
			ListBoxItem? container = listBox.ContainerFromItem(tab) as ListBoxItem;
			if (container == null)
			{
				continue;
			}

			container.Opacity = 0.0;								/* Before awaiting, set opacity to 0 so we dont see the full tab and then an animation. */
		
			/* There needs to be some delay - because the new tab UI needs to initialize and stuff. */
			container.InvalidateMeasure();							/* Force UI to recalculate layout sizes - because changes were just made. */
			await Dispatcher.UIThread.InvokeAsync(() => { });		/* Allow the UI thread to process any changes that were made. */
			
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
							new Setter(OpacityProperty, 0.0),
							new Setter(WidthProperty, 0.0),
							// new Setter(HeightProperty, 0.0)
						}
					},
				
					new KeyFrame()
					{
						Cue = new Cue(1.0),
						Setters =
						{
							new Setter(OpacityProperty, 1.0),
							new Setter(WidthProperty, container.Bounds.Width),
							// new Setter(HeightProperty, container.Bounds.Height)
						}
					}
				}
			};

			await animation.RunAsync(container);
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

		if (DataContext is not MainPageViewModel viewModel)
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
						// new Setter(HeightProperty, listBoxItem.Bounds.Height),
					}
				},
				
				new KeyFrame()
				{
					Cue = new Cue(1.0),
					Setters =
					{
						new Setter(OpacityProperty, 0.0),
						new Setter(WidthProperty, 0.0),
						// new Setter(HeightProperty, 0.0),
					}
				}
			}
		};

		await animation.RunAsync(listBoxItem);

		viewModel.CloseVmTab(tab);
	}
}