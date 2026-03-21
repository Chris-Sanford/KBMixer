using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace KBMixer;

/// <summary>Shows a ContentDialog for selecting an audio app (replaces the old WPF AppSelectionWindow).</summary>
internal static class AppSelectionDialog
{
    public static async Task<string?> ShowAsync(XamlRoot xamlRoot, AudioApp[] audioApps, string initialFriendlyName)
    {
        var items = audioApps
            .Where(a => !string.IsNullOrWhiteSpace(a.AppFriendlyName))
            .GroupBy(a => a.AppFriendlyName, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var first = g.First();
                return new AppPickItem
                {
                    DisplayName = g.Key,
                    Icon = ProcessIconHelper.TryGetIconForAudioApp(first)
                };
            })
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var radioSelect = new RadioButton
        {
            Content = "Choose from apps currently playing audio on this PC",
            GroupName = "AppMode",
            IsChecked = true
        };
        var radioManual = new RadioButton
        {
            Content = "Enter an executable name manually (e.g. chrome.exe)",
            GroupName = "AppMode"
        };

        var listView = new ListView
        {
            MaxHeight = 280,
            SelectionMode = ListViewSelectionMode.Single,
            ItemsSource = items,
            ItemTemplate = CreateItemTemplate()
        };

        var textBox = new TextBox
        {
            PlaceholderText = "e.g. chrome.exe",
            Visibility = Visibility.Collapsed
        };

        bool isSelectMode = true;

        radioSelect.Checked += (_, _) =>
        {
            isSelectMode = true;
            listView.Visibility = Visibility.Visible;
            textBox.Visibility = Visibility.Collapsed;
        };
        radioManual.Checked += (_, _) =>
        {
            isSelectMode = false;
            listView.Visibility = Visibility.Collapsed;
            textBox.Visibility = Visibility.Visible;
            textBox.Focus(FocusState.Programmatic);
        };

        var match = items.FirstOrDefault(i =>
            string.Equals(i.DisplayName, initialFriendlyName, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            radioSelect.IsChecked = true;
            listView.SelectedItem = match;
        }
        else
        {
            radioManual.IsChecked = true;
            textBox.Text = initialFriendlyName;
            isSelectMode = false;
            listView.Visibility = Visibility.Collapsed;
            textBox.Visibility = Visibility.Visible;
        }

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(radioSelect);
        panel.Children.Add(radioManual);
        panel.Children.Add(listView);
        panel.Children.Add(textBox);

        var dialog = new ContentDialog
        {
            Title = "Select an Application",
            Content = panel,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return null;

        if (isSelectMode)
            return (listView.SelectedItem as AppPickItem)?.DisplayName;

        var text = textBox.Text.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    static DataTemplate CreateItemTemplate()
    {
        // FontIcon behind Image: when Icon is null, Image is transparent and the fallback glyph shows
        const string xaml = """
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <StackPanel Orientation="Horizontal" Spacing="10" Padding="2,4">
                    <Grid Width="24" Height="24">
                        <FontIcon Glyph="&#xE8D6;" FontFamily="{ThemeResource SymbolThemeFontFamily}" FontSize="14"
                                  Foreground="{ThemeResource TextFillColorTertiaryBrush}"
                                  HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        <Image Source="{Binding Icon}" Width="24" Height="24" Stretch="Uniform"/>
                    </Grid>
                    <TextBlock Text="{Binding DisplayName}" VerticalAlignment="Center"/>
                </StackPanel>
            </DataTemplate>
            """;
        return (DataTemplate)XamlReader.Load(xaml);
    }
}
