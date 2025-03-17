﻿using System.Threading.Tasks;
using System.Windows;
using MahApps.Metro.Controls.Dialogs;

namespace WindowsGSM.Functions
{
    public static class UI
    {
        // Create Yes or No Prompt V1
        public static async Task<bool> CreateYesNoPromptV1(string title, string message, string affirmativeButtonText, string negativeButtonText)
        {
            return await Application.Current?.Dispatcher.Invoke(async () =>
            {
                MainWindow WindowsGSM = (MainWindow)Application.Current.MainWindow;
                MetroDialogSettings settings = new() {
                    AffirmativeButtonText = affirmativeButtonText,
                    NegativeButtonText = negativeButtonText,
                    DefaultButtonFocus = MessageDialogResult.Affirmative
                };

                MessageDialogResult result = await WindowsGSM.ShowMessageAsync(title, message, MessageDialogStyle.AffirmativeAndNegative, settings);
                return result == MessageDialogResult.Affirmative;
            });
        }
    }
}
