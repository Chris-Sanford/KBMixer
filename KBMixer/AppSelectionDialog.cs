using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

namespace KBMixer;

/// <summary>Shows a ContentDialog for selecting an audio app (replaces the old WPF AppSelectionWindow).</summary>
internal static class AppSelectionDialog
{
    public const string DeviceMasterResult = "\x01DEVICE_MASTER";

    public static async Task<string?> ShowAsync(
        XamlRoot xamlRoot, AudioApp[] audioApps, string initialFriendlyName, bool isDeviceMaster)
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
            GroupName = "AppMode"
        };
        var radioManual = new RadioButton
        {
            Content = "Enter an executable name manually (e.g. chrome.exe)",
            GroupName = "AppMode"
        };
        var radioDeviceMaster = new RadioButton
        {
            Content = "Control device master volume (all apps on this output)",
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

        var contentArea = new StackPanel { Spacing = 8 };
        contentArea.Children.Add(listView);
        contentArea.Children.Add(textBox);

        int mode = 0; // 0 = select, 1 = manual, 2 = device master

        radioSelect.Checked += (_, _) =>
        {
            mode = 0;
            listView.Visibility = Visibility.Visible;
            textBox.Visibility = Visibility.Collapsed;
            contentArea.Visibility = Visibility.Visible;
        };
        radioManual.Checked += (_, _) =>
        {
            mode = 1;
            listView.Visibility = Visibility.Collapsed;
            textBox.Visibility = Visibility.Visible;
            contentArea.Visibility = Visibility.Visible;
            textBox.Focus(FocusState.Programmatic);
        };
        radioDeviceMaster.Checked += (_, _) =>
        {
            mode = 2;
            contentArea.Visibility = Visibility.Collapsed;
        };

        if (isDeviceMaster)
        {
            radioDeviceMaster.IsChecked = true;
            mode = 2;
            contentArea.Visibility = Visibility.Collapsed;
        }
        else
        {
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
                mode = 1;
                listView.Visibility = Visibility.Collapsed;
                textBox.Visibility = Visibility.Visible;
            }
        }

        var separator = new Border
        {
            Height = 1,
            Margin = new Thickness(0, 4, 0, 4),
            Background = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"]
        };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(radioSelect);
        panel.Children.Add(radioManual);
        panel.Children.Add(radioDeviceMaster);
        panel.Children.Add(separator);
        panel.Children.Add(contentArea);

        var dialog = new ContentDialog
        {
            Title = "Select a Volume Target",
            Content = panel,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return null;

        if (mode == 2)
            return DeviceMasterResult;

        if (mode == 0)
            return (listView.SelectedItem as AppPickItem)?.DisplayName;

        var text = textBox.Text.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    static DataTemplate CreateItemTemplate()
    {
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
