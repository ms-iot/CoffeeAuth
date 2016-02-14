﻿using CoffeeAuth.Models;
using System;
using Windows.UI;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Globalization;
using Windows.ApplicationModel.Resources;
using Microsoft.Maker.RemoteWiring;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace CoffeeAuth
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class UserPage : Page
    {
        private string badgeCIN;
        private User user;

        DispatcherTimer timer;
        int numTicks;

        CultureInfo ci;

        // Price value
        private const int grindPrice = 1;
        private const int beanBagPrice = 14;
        private const int milkJugPrice = 6;

        public UserPage()
        {
            this.InitializeComponent();
            ci = CultureInfo.CurrentCulture;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            badgeCIN = e.Parameter as string;


            // check if user is in the database
            user = DrinkerDatabase.Instance.GetUser(badgeCIN);
            if (user == null)
            {
                // create user
                var action = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
                {
                    this.Frame.Navigate(typeof(UserCreatePage), badgeCIN);
                }
                ));
            }
            else
            {
                // Show user profile
                userName.Text = user.Name;
                userBalance.Text = user.Balance.ToString();

                if(user.IsAdmin)
                {
                    AdminButton.Visibility = Visibility.Visible;
                    AdminButton.IsEnabled = true;
                }

                user.NumLogins++;
                DrinkerDatabase.Instance.UpdateUser(user);
            }
        }


        /// <summary>
        /// Credits user value of a Bag of Beans
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bagButton_Click(object sender, RoutedEventArgs e)
        {
            showToast(App.rl.GetString("BagNotify_Title"), ci.NumberFormat.CurrencySymbol + beanBagPrice + App.rl.GetString("Credit"), App.rl.GetString("Appreciate"));
            user.Balance += beanBagPrice;
            user.NumBags++;
            userBalance.Text = user.Balance.ToString();
            DrinkerDatabase.Instance.UpdateUser(user);

        }

        /// <summary>
        /// Credits user value of a Jug of Milk
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void milkButton_Click(object sender, RoutedEventArgs e)
        {
            showToast(App.rl.GetString("MilkNotify_Title"), ci.NumberFormat.CurrencySymbol + milkJugPrice + App.rl.GetString("Credit"), App.rl.GetString("Appreciate"));
            user.Balance += milkJugPrice;
            user.NumMilks++;
            userBalance.Text = user.Balance.ToString();
            DrinkerDatabase.Instance.UpdateUser(user);
        }

        /// <summary>
        /// Debits user value of a cup of coffee
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void getCoffeeButton_Click(object sender, RoutedEventArgs e)
        {
            if (!App.isArduinoConnected)
            {
                await ArduinoReconnectDialog.ShowAsync();
            }
            else
            {

                showToast(App.rl.GetString("ShotNotify_Title"), ci.NumberFormat.CurrencySymbol + grindPrice + App.rl.GetString("Debit"), App.rl.GetString("Thanks"));
                user.Balance -= grindPrice;
                user.NumShots++;
                userBalance.Text = user.Balance.ToString();
                DrinkerDatabase.Instance.UpdateUser(user);

                Countdown();
                numTicks = 30;
                timer = new DispatcherTimer();
                timer.Interval = new TimeSpan(0, 0, 1);
                timer.Tick += Timer_Tick;
                timer.Start();

                // Turn on grinder

#if HARDWARE
                App.arduino.digitalWrite(13, PinState.HIGH);
#endif
            }

        }


        /// <summary>
        /// Sets text on the CountdownDialog as well as text color
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Timer_Tick(object sender, object e)
        {
            DispatcherTimer timer = (DispatcherTimer)sender;
            numTicks--;

            if (numTicks == 0)
            {
            // Turn off grinder
#if HARDWARE
                App.arduino.digitalWrite(13, PinState.LOW);
#endif
                timer.Stop();
                CountDownDialog.Hide();
                Frame.Navigate(typeof(MainPage));
            }
            else
            {
                //update timer UI 
                countdownTimerText.Text = numTicks.ToString();
                if (numTicks <= 15)
                {
                    countdownTimerText.Foreground = new SolidColorBrush(Colors.Orange);
                }
                if (numTicks <= 5)
                {
                    countdownTimerText.Foreground = new SolidColorBrush(Colors.Red);
                }
            }
        }


        private async void Countdown()
        {
            await CountDownDialog.ShowAsync();
        }


        private void AppBarButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage), user);
        }

        /// <summary>
        /// Shows a notification to affirm an action taken by a user.
        /// </summary>
        /// <param name="heading"></param>
        /// <param name="body"></param>
        /// <param name="body2"></param>
        private void showToast(string heading, string body, string body2)

        {
            // use template toast 4 
            var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText04); // bold title and 2 normal lines 
            var stringElements = toastXml.GetElementsByTagName("text");

            // set title, body, and body2
            stringElements[0].AppendChild(toastXml.CreateTextNode(heading));
            stringElements[1].AppendChild(toastXml.CreateTextNode(body));
            stringElements[2].AppendChild(toastXml.CreateTextNode(body2));

            ToastNotification toast = new ToastNotification(toastXml);
            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }

        private async void settleButton_Click(object sender, RoutedEventArgs e)
        {
            await SettleDialog.ShowAsync();
        }

        private void SettleDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            string val = settleUpTextBox.Text;
            try
            {
                int num = Convert.ToInt32(val);

                string body = ci.NumberFormat.CurrencySymbol + num + App.rl.GetString("Credit");
                showToast(App.rl.GetString("Settle_Title"), body, App.rl.GetString("Appreciate"));

                user.Balance += num;
                userBalance.Text = user.Balance.ToString();
                DrinkerDatabase.Instance.UpdateUser(user);

                SettleDialog.Hide();
            }
            catch
            {
                // todo show error
            }
        }

        private void SettleDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            string val = settleUpTextBox.Text;
            try
            {
                int num = Convert.ToInt32(val);

                string body = ci.NumberFormat.CurrencySymbol + num + App.rl.GetString("Debit");
                showToast(App.rl.GetString("Settle_Title"), body, App.rl.GetString("Appreciate"));

                user.Balance -= num;
                userBalance.Text = user.Balance.ToString();
                DrinkerDatabase.Instance.UpdateUser(user);

                SettleDialog.Hide();
            }
            catch
            {
                // todo show error
            }
        }

        private void ArduinoReconnectDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            App.Usb_Reconnect();
            ArduinoReconnectDialog.Hide();
        }

        private async void AppBarSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            await NameChangeDialog.ShowAsync();
        }

        private void AppBarAdminButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(AdminPage), user);
        }

        private void NameChangeDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (nameTextBox.Text.Length > 0)
            {
                user.Name = nameTextBox.Text;
                userName.Text = nameTextBox.Text;
                DrinkerDatabase.Instance.UpdateUser(user);
            }
        }
    }
}
