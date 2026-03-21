using Microsoft.UI.Xaml.Media;

namespace KBMixer;

public sealed class AppPickItem
{
    public required string DisplayName { get; init; }
    public ImageSource? Icon { get; init; }
}
