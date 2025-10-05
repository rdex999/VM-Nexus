using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
}