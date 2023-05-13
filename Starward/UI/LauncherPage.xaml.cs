﻿// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Starward.Core;
using Starward.Core.Launcher;
using Starward.Model;
using Starward.Service;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Starward.UI;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
[INotifyPropertyChanged]
public sealed partial class LauncherPage : Page
{

    private readonly ILogger<LauncherPage> _logger;

    private readonly LauncherService _launcherService;

    private readonly DispatcherQueueTimer _timer;

    private GameBiz gameBiz;



    public LauncherPage()
    {
        this.InitializeComponent();
        _logger = ServiceProvider.GetLogger<LauncherPage>();
        _launcherService = ServiceProvider.GetService<LauncherService>();

        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(5);
        _timer.IsRepeating = true;
        _timer.Tick += _timer_Tick;
    }


    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is GameBiz biz)
        {
            gameBiz = biz;
        }
    }




    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            InitializeBameBiz();
            UpdateGameState();
            GetGameAccount();
            await GetLauncherContentAsync();
            _timer.Start();
        }
        catch (Exception ex)
        {

        }
    }


    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        GameProcess?.Dispose();
    }



    private void InitializeBameBiz()
    {
        try
        {
#pragma warning disable MVVMTK0034 // Direct field reference to [ObservableProperty] backing field
            startGameArgument = AppConfig.GetStartArgument(gameBiz);
            OnPropertyChanged(nameof(StartGameArgument));
            if (gameBiz is GameBiz.hkrpg_cn or GameBiz.hkrpg_global)
            {
                targetFPS = GameService.GetStarRailFPS(gameBiz);
                OnPropertyChanged(nameof(TargetFPS));
            }
#pragma warning restore MVVMTK0034 // Direct field reference to [ObservableProperty] backing field 
        }
        catch (Exception ex)
        {

        }
    }






    #region Anncounce & Post



    [ObservableProperty]
    private List<LauncherBanner> bannerList;


    [ObservableProperty]
    private List<LauncherPostGroup> launcherPostGroupList;


    [ObservableProperty]
    private bool enableBannerAndPost = AppConfig.EnableBannerAndPost;
    partial void OnEnableBannerAndPostChanged(bool value)
    {
        Grid_BannerAndPost.Opacity = value ? 1 : 0;
        Grid_BannerAndPost.IsHitTestVisible = value;
        AppConfig.EnableBannerAndPost = value;
    }



    private async Task GetLauncherContentAsync()
    {
        try
        {
            var content = await _launcherService.GetLauncherContentAsync(gameBiz);
            BannerList = content.Banner;
            LauncherPostGroupList = content.Post.GroupBy(x => x.Type).OrderBy(x => x.Key).Select(x => new LauncherPostGroup(PostTypeToString(x.Key), x)).ToList();
            if (EnableBannerAndPost)
            {
                Grid_BannerAndPost.Opacity = 1;
                Grid_BannerAndPost.IsHitTestVisible = true;
            }
        }
        catch (Exception ex)
        {

        }
    }


    private string PostTypeToString(PostType type)
    {
        return type switch
        {
            PostType.POST_TYPE_ACTIVITY => "活动",
            PostType.POST_TYPE_ANNOUNCE => "公告",
            PostType.POST_TYPE_INFO => "资讯",
            _ => "",
        };
    }



    private void _timer_Tick(DispatcherQueueTimer sender, object args)
    {
        try
        {
            if (BannerList?.Any() ?? false)
            {
                PipsPager_Banner.SelectedPageIndex = (PipsPager_Banner.SelectedPageIndex + 1) % PipsPager_Banner.NumberOfPages;
            }
        }
        catch { }
    }


    private async void Image_Banner_Tapped(object sender, TappedRoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement fe && fe.DataContext is LauncherBanner banner)
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(banner.Url));
            }
        }
        catch (Exception ex)
        {

        }
    }



    private void FlipView_Banner_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _timer.Stop();
    }

    private void FlipView_Banner_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _timer.Start();
    }



    #endregion




    #region Start Game



    [ObservableProperty]
    private Process? gameProcess;
    partial void OnGameProcessChanged(Process? oldValue, Process? newValue)
    {
        oldValue?.Dispose();
        processTimer?.Stop();
        if (newValue != null)
        {
            try
            {
                Button_StartGame.IsEnabled = false;
                TextBlock_StartGame.Text = "游戏正在运行";
                newValue.EnableRaisingEvents = true;
                newValue.Exited += (_, _) => CheckGameExited();
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
            {
                // Access is denied
                processTimer?.Start();
            }
        }
    }


    private Timer processTimer = new(1000);



    [ObservableProperty]
    private string? startGameArgument;
    partial void OnStartGameArgumentChanged(string? value)
    {
        AppConfig.SetStartArgument(gameBiz, value);
    }


    [ObservableProperty]
    private int targetFPS;
    partial void OnTargetFPSChanged(int value)
    {
        try
        {
            value = Math.Clamp(value, 60, 320);
            GameService.SetStarRailFPS(gameBiz, value);
        }
        catch (Exception ex)
        {

        }
    }


    [ObservableProperty]
    private bool ignoreRunningGame = AppConfig.IgnoreRunningGame;
    partial void OnIgnoreRunningGameChanged(bool value)
    {
        AppConfig.IgnoreRunningGame = value;
    }



    private void UpdateGameState()
    {
        try
        {
            if (IgnoreRunningGame)
            {
                return;
            }
            processTimer.Elapsed += (_, _) => CheckGameExited();
            GameProcess = GameService.GetGameProcess(gameBiz);
        }
        catch (Exception ex)
        {

        }
    }



    private void CheckGameExited()
    {
        try
        {
            if (GameProcess != null)
            {
                if (GameProcess.HasExited)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        Button_StartGame.IsEnabled = true;
                        TextBlock_StartGame.Text = "开始游戏";
                    });
                    GameProcess.Dispose();
                    GameProcess = null;
                }
            }
        }
        catch (Exception ex)
        {

        }
    }



    [RelayCommand]
    private void StartGame()
    {
        try
        {
            if (IgnoreRunningGame)
            {
                GameService.StartGame(gameBiz, IgnoreRunningGame);
            }
            else
            {
                if (GameProcess?.HasExited ?? true)
                {
                    GameProcess = GameService.StartGame(gameBiz, IgnoreRunningGame);
                }
            }
        }
        catch (Exception ex)
        {

        }
    }




    #endregion




    #region Game Account



    [ObservableProperty]
    private List<GameAccount> gameAccountList;


    [ObservableProperty]
    private GameAccount? selectGameAccount;
    partial void OnSelectGameAccountChanged(GameAccount? value)
    {
        CanChangeGameAccount = value is not null;
    }


    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChangeGameAccountCommand))]
    private bool canChangeGameAccount;



    private void GetGameAccount()
    {
        try
        {
            GameAccountList = GameService.GetGameAccounts(gameBiz).ToList();
            SelectGameAccount = GameAccountList.FirstOrDefault(x => x.IsLogin);
            CanChangeGameAccount = false;
        }
        catch (Exception ex)
        {

        }
    }




    [RelayCommand(CanExecute = nameof(CanChangeGameAccount))]
    private void ChangeGameAccount()
    {
        try
        {
            if (SelectGameAccount is not null)
            {
                GameService.ChangeGameAccount(SelectGameAccount);
                foreach (var item in GameAccountList)
                {
                    item.IsLogin = false;
                }
                CanChangeGameAccount = false;
                SelectGameAccount.IsLogin = true;
            }
        }
        catch (Exception ex)
        {

        }
    }


    [RelayCommand]
    private async Task SaveGameAccountAsync()
    {
        try
        {
            if (SelectGameAccount is not null)
            {
                SelectGameAccount.Time = DateTime.Now;
                GameService.SaveGameAccount(SelectGameAccount);
                FontIcon_SaveGameAccount.Glyph = "\uE10B";
                await Task.Delay(3000);
                FontIcon_SaveGameAccount.Glyph = "\uE105";
            }
        }
        catch (Exception ex)
        {

        }
    }


    [RelayCommand]
    private void DeleteGameAccount()
    {
        try
        {
            if (SelectGameAccount is not null)
            {
                GameService.DeleteGameAccount(SelectGameAccount);
                GetGameAccount();
            }
        }
        catch (Exception ex)
        {

        }
    }




    #endregion


















}
