using System;
using System.Windows;

namespace ExtintorCrm.App.Presentation;

public static class DialogService
{
    public static bool Confirm(string title, string message, Window? owner = null)
    {
        return ConfirmDialogWindow.Show(title, message, owner ?? Application.Current?.MainWindow);
    }

    public static bool ConfirmWithText(string title, string message, string requiredText, Window? owner = null)
    {
        return ConfirmTextWindow.Show(title, message, requiredText, owner ?? Application.Current?.MainWindow);
    }

    public static void Info(string title, string message, Window? owner = null)
    {
        InfoDialogWindow.Show(title, message, owner ?? Application.Current?.MainWindow);
    }

    public static void Error(string title, string message, Window? owner = null)
    {
        InfoDialogWindow.Show(title, message, owner ?? Application.Current?.MainWindow);
    }
}
