using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CustomAssetsInjector.Services;

namespace CustomAssetsInjector.Views;

public partial class MessageBox : Window
{
    public const string EXIT_STRING = "EXIT";

    public const string OPT_OUT_STRING = "OPTOUT";
    
    private string? m_Result;
    
    public MessageBox()
    {
        InitializeComponent();
    }
    
    /// <summary>
    /// Spawns a new message box dialog.
    /// </summary>
    /// <param name="owner">The owner of the created message box.</param>
    /// <param name="message">The main text to be displayed to the user.</param>
    /// <param name="title">The title of the window.</param>
    /// <param name="buttons">An array of options for the user to pick</param>
    /// <returns>The option picked from the buttons array, or "EXIT" if the message box was closed without a button being selected.</returns>
    public static async Task<string> ShowMessageBox(Window owner, string message, string title, params string[] buttons)
    {
        return await new MessageBox().Show(owner, message, title, null, buttons);
    }
    
    /// <summary>
    /// Spawns a new message box dialog, closing instantly if the user has opted out.
    /// </summary>
    /// <param name="owner">The owner of the created message box.</param>
    /// <param name="id">The ID to determine if the user has opted out of the window.</param>
    /// <param name="message">The main text to be displayed to the user.</param>
    /// <param name="title">The title of the window.</param>
    /// <param name="buttons">An array of options for the user to pick</param>
    /// <returns>The option picked from the buttons array, or "EXIT" if the message box was closed without a button being selected, or "OPTOUT" if the user has opted out</returns>
    public static async Task<string> ShowMessageBox(Window owner, string id, string message, string title, params string[] buttons)
    {
        return await new MessageBox().Show(owner, message, title, id, buttons);
    }

    private async Task<string> Show(Window owner, string message, string title, string? id, params string[] buttons)
    {
        if (!string.IsNullOrEmpty(id))
        {
            if (PreferenceService.GetPrefs().OptedOutPopups.Contains(id))
            {
                return OPT_OUT_STRING;
            }

            MainGrid.Height = 370;
            OptOutCheckBox.IsEnabled = true;
            OptOutCheckBox.IsVisible = true;
        }
        
        Title = title;
        MessageText.Text = message;

        if (buttons.Length != 0)
        {
            InitialButton.Content = buttons[0];

            for (var i = 1; i < buttons.Length; i++)
            {
                var button = new Button
                {
                    Content = buttons[i],
                    Margin = new Thickness(10),
                    CornerRadius = new CornerRadius(5),
                    Height = 50
                };
                button.Click += Button_Click;
                Grid.SetColumn(button, MainGrid.ColumnDefinitions.Count);
                Grid.SetRow(button, 1);

                MainGrid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(6, GridUnitType.Star)
                });
                MainGrid.Children.Add(button);
            }
        }
        else
        {
            InitialButton.Content = "Ok";
        }

        Grid.SetColumnSpan(MessageTextScroller, buttons.Length == 0 ? 1 : buttons.Length);

        await ShowDialog(owner);

        if (!string.IsNullOrEmpty(id) && OptOutCheckBox.IsChecked == true)
        {
            var prefs = PreferenceService.GetPrefs();
            prefs.OptedOutPopups.Add(id);
            PreferenceService.SetPrefs(prefs);
        }

        var result = m_Result ?? EXIT_STRING; // clicking the X button on the menu bar leaves result as null because no button was clicked
        m_Result = null;
        
        return result;
    }

    private void Button_Click(object? sender, RoutedEventArgs e)
    {
        m_Result = (sender as Button)?.Content as string;
        Close();
    } 
}