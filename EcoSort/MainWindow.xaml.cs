using EcoSort.Pages;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using Windows.Graphics;
using WinRT.Interop;

namespace EcoSort
{
    public sealed partial class MainWindow : Window
    {
        private const int MinWindowWidth = 1000;
        private const int MinWindowHeight = 720;
        private const int MaxWindowWidth = 2400;
        private const int MaxWindowHeight = 1600;

        private readonly AppWindow _appWindow;

        public MainWindow()
        {
            InitializeComponent();

            var hWnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            ConfigureTitleBar();
            ConfigureWindowSizeConstraints();

            ContentFrame.Navigated += ContentFrame_Navigated;
            ContentFrame.Navigate(typeof(HomePage));
            AppNavigationView.SelectedItem = HomeNavItem;
            AppNavigationView.IsBackEnabled = ContentFrame.CanGoBack;
        }

        private void ConfigureTitleBar()
        {
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            if (!AppWindowTitleBar.IsCustomizationSupported())
            {
                return;
            }

            var titleBar = _appWindow.TitleBar;
            titleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
            titleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
            titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(60, 255, 255, 255);
            titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(100, 255, 255, 255);
        }

        private void ConfigureWindowSizeConstraints()
        {
            _appWindow.Changed += AppWindow_Changed;
            EnforceWindowBounds();
        }

        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidSizeChange)
            {
                EnforceWindowBounds();
            }
        }

        private void EnforceWindowBounds()
        {
            var currentSize = _appWindow.Size;
            var boundedWidth = Math.Clamp(currentSize.Width, MinWindowWidth, MaxWindowWidth);
            var boundedHeight = Math.Clamp(currentSize.Height, MinWindowHeight, MaxWindowHeight);

            if (boundedWidth == currentSize.Width && boundedHeight == currentSize.Height)
            {
                return;
            }

            _appWindow.Resize(new SizeInt32(boundedWidth, boundedHeight));
        }

        private void AppNavigationView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if (ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();
            }
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            AppNavigationView.IsBackEnabled = ContentFrame.CanGoBack;
            SetSelectedNavItemForPage(e.SourcePageType);
        }

        private void SetSelectedNavItemForPage(Type pageType)
        {
            if (pageType == typeof(HomePage))
            {
                AppNavigationView.SelectedItem = HomeNavItem;
                return;
            }

            if (pageType == typeof(ClassifyPage))
            {
                AppNavigationView.SelectedItem = ClassifyNavItem;
                return;
            }

            if (pageType == typeof(EducationPage))
            {
                AppNavigationView.SelectedItem = EducationNavItem;
                return;
            }

            if (pageType == typeof(CentersPage))
            {
                AppNavigationView.SelectedItem = CentersNavItem;
                return;
            }

            if (pageType == typeof(SettingsPage))
            {
                AppNavigationView.SelectedItem = AppNavigationView.SettingsItem;
            }
        }

        private void AppNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                if (ContentFrame.CurrentSourcePageType != typeof(SettingsPage))
                {
                    ContentFrame.Navigate(typeof(SettingsPage));
                }
                return;
            }

            if (args.SelectedItemContainer is not NavigationViewItem item || item.Tag is not string tag)
            {
                return;
            }

            var targetPage = tag switch
            {
                "home" => typeof(HomePage),
                "classify" => typeof(ClassifyPage),
                "education" => typeof(EducationPage),
                "centers" => typeof(CentersPage),
                _ => typeof(HomePage)
            };

            if (ContentFrame.CurrentSourcePageType != targetPage)
            {
                ContentFrame.Navigate(targetPage);
            }
        }
    }
}
