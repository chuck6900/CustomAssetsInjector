using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CustomAssetsInjector.Views;

public partial class MessageBox : Window
{
    private static string? m_Result;
    
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
        return await new MessageBox().Show(owner, message, title, buttons);
    }

    private async Task<string> Show(Window owner, string message, string title, params string[] buttons)
    {
        Title = title;
        MessageText.Text = message;

        if (buttons.Length == 0)
        {
            InitialButton.Content = "Ok";
        }
        else
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

        Grid.SetColumnSpan(MessageTextScroller, buttons.Length == 0 ? 1 : buttons.Length);

        await ShowDialog(owner);

        var result = m_Result ?? "EXIT"; // clicking the X button on the menu bar leaves result as null because no button was clicked
        m_Result = null;
        
        return result;
    }

    private void Button_Click(object? sender, RoutedEventArgs e)
    {
        m_Result = (sender as Button)?.Content as string;
        Close();
    } 
}