using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace Client.Controls.Templated;

public class OverlayPopup : TemplatedControl
{
	public static readonly StyledProperty<object?> ContentProperty = 
		AvaloniaProperty.Register<OverlayPopup, object?>(nameof(Content));
	
	public static readonly StyledProperty<bool> IsOpenProperty =
		AvaloniaProperty.Register<OverlayPopup, bool>(nameof(IsOpen));

	public static readonly StyledProperty<PlacementMode> PlacementProperty =
		AvaloniaProperty.Register<OverlayPopup, PlacementMode>(nameof(Placement));

	public static readonly StyledProperty<Control?> PlacementTargetProperty =
		AvaloniaProperty.Register<OverlayPopup, Control?>(nameof(PlacementTarget));

	public static readonly StyledProperty<string?> TitleProperty =
		AvaloniaProperty.Register<OverlayPopup, string?>(nameof(Title));

	public static readonly StyledProperty<ICommand?> ClosedCommandProperty =
		AvaloniaProperty.Register<OverlayPopup, ICommand?>(nameof(ClosedCommand));
	
	[Content]
	public object? Content
	{
		get => GetValue(ContentProperty);
		set => SetValue(ContentProperty, value);
	}

	public bool IsOpen
	{
		get => GetValue(IsOpenProperty);
		set => SetValue(IsOpenProperty, value);
	}

	public PlacementMode Placement
	{
		get => GetValue(PlacementProperty);
		set => SetValue(PlacementProperty, value);
	}

	[ResolveByName]
	public Control? PlacementTarget
	{
		get => GetValue(PlacementTargetProperty);
		set => SetValue(PlacementTargetProperty, value);
	}

	public string? Title
	{
		get => GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	public ICommand? ClosedCommand
	{
		get => GetValue(ClosedCommandProperty);
		set => SetValue(ClosedCommandProperty, value);
	}

	public OverlayPopup()
	{
		if (double.IsNaN(Width)) Width = 400;	/* Default width */
	}

	/// <summary>
	/// Called just before the templated control is shown.
	/// </summary>
	/// <param name="e">The event arguments. e != null</param>
	/// <remarks>
	/// Precondition: Templated control applied. (just before its shown) e != null. <br/>
	/// Postcondition: Subscribed for needed events in controls.
	/// </remarks>
	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
	{
		base.OnApplyTemplate(e);
		Button? closeButton = e.NameScope.Find<Button>("CloseButton");
		if (closeButton == null) return;
		
		closeButton.Click += OnPopupClosed;
		
		Popup? popup = e.NameScope.Find<Popup>("Popup");
		if (popup == null) return;
		
		popup.Closed += OnPopupClosed;
	}

	/// <summary>
	/// Handles a popup close event.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="e">Unused.</param>
	/// <remarks>
	/// Precondition: Popup was closed. <br/>
	/// Postcondition: ClosedCommand is executed if possible.
	/// </remarks>
	private void OnPopupClosed(object? sender, EventArgs e)
	{
		IsOpen = false;
		
		if (ClosedCommand != null && ClosedCommand.CanExecute(null))
		{
			ClosedCommand.Execute(null);
		}
	}
}