using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace Client.Controls.Templated;

public class OverlayPopup : TemplatedControl
{
	public event EventHandler? Closed;
	
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

	public OverlayPopup()
	{
	}

	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
	{
		base.OnApplyTemplate(e);
		Button? closeButton = e.NameScope.Find<Button>("CloseButton");
		if (closeButton == null) return;
		
		closeButton.Click += OnCloseButtonClick;
	}

	private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
	{
		IsOpen = false;
		Closed?.Invoke(this, EventArgs.Empty);
	}
}