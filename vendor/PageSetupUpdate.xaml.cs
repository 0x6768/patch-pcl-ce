using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;

namespace PCL;

public partial class PageSetupUpdate
{
    public VersionDataModel? updateInfo;

    // ============================================================
    // 是否修改版本（你们自己控制这个开关）
    // 改成 true = 禁用一切更新相关功能
    // 改成 false = 恢复正常更新逻辑
    // ============================================================
    private static readonly bool IsModifiedVersion = true;

    public PageSetupUpdate()
    {
        InitializeComponent();
        Loaded += (_, _) => Init();
    }

    private void Init()
    {
        // 原：ModAnimation.AniBeginControl(); —— 上游已移除该 API，直接去掉
        TextMirrorCDK.Password = Config.Update.MirrorChyanKey;

        ComboSystemUpdateChannel.SelectedIndex = (int)Config.Update.UpdateChannel;
        ComboSystemUpdateMode.SelectedIndex = (int)Config.Update.UpdateMode;

        TextCurrentVersion.Text = "PCL CE " + VersionNameFormat(ModBase.versionBaseName);

        // 修改版：灰掉所有更新相关按钮，显示禁用提示
        if (IsModifiedVersion)
        {
            BtnCheckAgain.IsEnabled = false;
            BtnUpdate.IsEnabled = false;
            ComboSystemUpdateMode.IsEnabled = false;
            ComboSystemUpdateChannel.IsEnabled = false;
            TextCurrentDesc.Text = "当前为修改版本，更新功能已禁用。";
            CardUpdate.Visibility = Visibility.Collapsed;
            CardCheck.Visibility = Visibility.Visible;
            // 原：ModAnimation.AniEndControl();
            return;
        }

        // 原：ModAnimation.AniEndControl();
        CheckUpdate();
    }

    private async Task<UpdateStatus> IsLatestAsync()
    {
        // 修改版直接返回 Latest，不联网
        if (IsModifiedVersion)
            return UpdateStatus.Latest;

        try
        {
            if (await UpdateManager.remoteServer.IsLatestAsync(
                    UpdateManager.IsCurrentVersionBeta ? UpdateChannel.beta : UpdateChannel.stable,
                    SystemInfo.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64,
                    SemVer.Parse(ModBase.versionBaseName),
                    ModBase.versionCode))
            {
                ModBase.Log("[Update] 已是最新版本");
                return UpdateStatus.Latest;
            }

            ModBase.Log("[Update] 有可用的新版本");
            return UpdateStatus.Available;
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                Lang.Text("Setup.Update.Error.NetworkFailed"),
                ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Setup.Update.Error.NetworkFailed"));
            return UpdateStatus.Error;
        }
    }

    public async void CheckUpdate()
    {
        // 修改版：直接弹窗，不检查
        if (IsModifiedVersion)
        {
            ModMain.MyMsgBox(
                "这是修改版本，无法检查更新。\n\n如需获取最新版本，请前往官方仓库手动下载。",
                "更新已禁用",
                Lang.Text("Common.Action.Confirm"));
            return;
        }

        ModBase.Log("[Update] 开始检查更新");
        CardUpdate.Visibility = Visibility.Collapsed;
        CardCheck.Visibility = Visibility.Visible;
        TextCurrentDesc.Text = Lang.Text("Setup.Update.Checking");
        BtnCheckAgain.IsEnabled = false;

        switch (await IsLatestAsync())
        {
            case UpdateStatus.Available:
            {
                Exception? checkUpdateEx = null;
                try
                {
                    updateInfo = UpdateManager.remoteServer.GetLatestVersion(
                        UpdateManager.IsCurrentVersionBeta
                            ? UpdateChannel.beta
                            : UpdateChannel.stable,
                        SystemInfo.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64);

                    TextUpdateName.Text = "PCL CE " + VersionNameFormat(updateInfo.VersionName);
                    var summary = updateInfo.Changelog.Between("<summary>", "</summary>");
                    if (!updateInfo.Changelog.Contains("<summary>") || string.IsNullOrWhiteSpace(summary.Trim()))
                        TextChangelog.Text = Lang.Text("Setup.Update.Changelog.Empty");
                    else
                        TextChangelog.Text = summary;
                }
                catch (Exception ex)
                {
                    checkUpdateEx = ex;
                }

                BtnCheckAgain.IsEnabled = true;
                if (updateInfo is null)
                {
                    TextCurrentDesc.Text = Lang.Text("Setup.Update.CheckFailed");
                    if (checkUpdateEx is not null)
                        ModBase.Log(
                            checkUpdateEx,
                            "[Update] 检查更新失败",
                            ModBase.LogLevel.Msgbox,
                            userSummary: Lang.Text("Update.Check.Failed"));
                    else
                        ModBase.Log(
                            "[Update] 检查更新失败",
                            ModBase.LogLevel.Msgbox,
                            userSummary: Lang.Text("Update.Check.Failed"));
                    return;
                }

                if (UpdateManager.updateLoader is not null && UpdateManager.updateLoader.State == ModBase.LoadState.Loading)
                {
                    BtnUpdate_Timer();
                    BtnUpdate.IsEnabled = false;
                }
                else if (UpdateManager.isUpdateWaitingRestart)
                {
                    BtnUpdate.Text = Lang.Text("Setup.Update.RestartInstall");
                    BtnUpdate.IsEnabled = true;
                }
                else
                {
                    BtnUpdate.Text = Lang.Text("Setup.Update.Install");
                    BtnUpdate.IsEnabled = true;
                }

                CardUpdate.Visibility = Visibility.Visible;
                CardCheck.Visibility = Visibility.Collapsed;
                break;
            }

            case UpdateStatus.Latest:
            {
                CardUpdate.Visibility = Visibility.Collapsed;
                CardCheck.Visibility = Visibility.Visible;
                BtnCheckAgain.IsEnabled = true;
                TextCurrentDesc.Text = Lang.Text("Setup.Update.Latest");
                break;
            }

            case UpdateStatus.Error:
            {
                CardUpdate.Visibility = Visibility.Collapsed;
                CardCheck.Visibility = Visibility.Visible;
                BtnCheckAgain.IsEnabled = true;
                TextCurrentDesc.Text = Lang.Text("Setup.Update.CheckFailed");
                break;
            }
        }
    }

    public void BtnUpdate_Timer()
    {
        while (UpdateManager.updateLoader is not null && UpdateManager.updateLoader.State == ModBase.LoadState.Loading)
        {
            ModBase.RunInUi(() => BtnUpdate.Text = Lang.Number(UpdateManager.updateLoader.Progress, "P2"));
            Thread.Sleep(200);
        }
    }

    private void BtnUpdate_Click(object sender, MouseButtonEventArgs e)
    {
        // 修改版：点安装也拦
        if (IsModifiedVersion)
        {
            ModMain.MyMsgBox(
                "这是修改版本，无法更新。\n\n如需获取最新版本，请前往官方仓库手动下载。",
                "更新已禁用",
                Lang.Text("Common.Action.Confirm"));
            return;
        }

        if (UpdateManager.isUpdateWaitingRestart) UpdateManager.UpdateRestart(true);
        UpdateManager.UpdateStart(UpdateEnums.UpdateType.UpdateNow);
    }

    private void BtnChangelogDetail_Click(object sender, EventArgs e)
    {
        if (IsModifiedVersion)
        {
            ModMain.MyMsgBox(
                "这是修改版本，无法查看更新日志。",
                "更新已禁用",
                Lang.Text("Common.Action.Confirm"));
            return;
        }

        if (updateInfo is null)
            ModMain.MyMsgBox(Lang.Text("Setup.Update.Changelog.Unavailable"), Lang.Text("Setup.Update.Changelog.Title"));
        else
            ModMain.MyMsgBoxMarkdown(updateInfo.Changelog, Lang.Text("Setup.Update.Changelog.Title"));
    }

    private void ComboSystemUpdateMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsModifiedVersion) return;
        // 原：if (!ModAnimation.AniExitsAnyControl())
        Config.Update.UpdateMode = (LauncherAutoUpdateBehavior)ComboSystemUpdateMode.SelectedIndex;
    }

    private void ComboSystemUpdateBranch_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsModifiedVersion) return;
        // 原：if (ModAnimation.AniExitsAnyControl()) return;

        var isCancelled = false;
        switch (ComboSystemUpdateChannel.SelectedIndex)
        {
            case 0:
                break;

            case 1:
                if (ModMain.MyMsgBox(Lang.Text("Setup.Update.Channel.Beta.Warning.Message"),
                        Lang.Text("Setup.Update.Channel.Common.Warning.Title"),
                        Lang.Text("Setup.Update.Channel.Common.Warning.Confirm"),
                        Lang.Text("Common.Action.Cancel"), isWarn: true) == 2)
                    isCancelled = true;
                else
                    CheckUpdate();
                break;

            case 2:
                if (ModMain.MyMsgBox(Lang.Text("Setup.Update.Channel.Dev.Warning.Message"),
                        Lang.Text("Setup.Update.Channel.Common.Warning.Title"),
                        Lang.Text("Setup.Update.Channel.Common.Warning.Confirm"),
                        Lang.Text("Common.Action.Cancel"), isWarn: true) == 2)
                {
                    isCancelled = true;
                    break;
                }

                var confirmText = Lang.Text("Setup.Update.Channel.Dev.FinalConfirm.ExpectedInput");
                var ret = ModMain.MyMsgBoxInput(
                    Lang.Text("Setup.Update.Channel.Dev.FinalConfirm.Title"),
                    Lang.Text("Setup.Update.Channel.Dev.FinalConfirm.Message", confirmText),
                    button1: Lang.Text("Setup.Update.Channel.Dev.FinalConfirm.Submit"),
                    button2: Lang.Text("Common.Action.Cancel"), isWarn: true);

                if (ret == confirmText)
                {
                    CheckUpdate();
                }
                else
                {
                    HintService.Hint(Lang.Text("Setup.Update.Channel.Dev.FinalConfirm.WrongInput"));
                    isCancelled = true;
                }
                break;
        }

        if (isCancelled)
        {
            // 原：ModAnimation.AniBeginControl();
            ComboSystemUpdateChannel.SelectedItem = e.RemovedItems[0];
            // 原：ModAnimation.AniEndControl();
        }
        else
        {
            Config.Update.UpdateChannel = (Core.App.UpdateChannel)ComboSystemUpdateChannel.SelectedIndex;
        }
    }

    private void TextMirrorCDK_PasswordChanged(object sender, EventArgs e)
    {
        if (IsModifiedVersion) return;
        Config.Update.MirrorChyanKey = TextMirrorCDK.Password;
    }

    private void BtnGetMirrorCDK_Click(object sender, MouseButtonEventArgs e)
    {
        if (IsModifiedVersion) return;
        ModBase.OpenWebsite("https://mirrorchyan.com/");
    }

    private void BtnChangelog_Click(object sender, MouseButtonEventArgs e)
    {
        if (IsModifiedVersion)
        {
            ModMain.MyMsgBox(
                "这是修改版本，无法查看更新日志。",
                "更新已禁用",
                Lang.Text("Common.Action.Confirm"));
            return;
        }

        ModBase.OpenWebsite("https://github.com/PCL-Community/PCL2-CE/releases/v" + ModBase.versionBaseName);
    }

    public string VersionNameFormat(string str)
    {
        str = str.Replace("v", "");
        if (!str.Contains("-"))
            return str;
        var add = str.AfterLast("-");
        str = str.BeforeLast("-");
        return $"{str} {add.Replace(".", " ").Replace("beta", "Beta").Replace("rc", "RC")}";
    }

    private void BtnCheckAgain_OnClick(object sender, MouseButtonEventArgs e)
    {
        // 修改版：直接弹窗
        if (IsModifiedVersion)
        {
            ModMain.MyMsgBox(
                "这是修改版本，无法检查更新。\n\n如需获取最新版本，请前往官方仓库手动下载。",
                "更新已禁用",
                Lang.Text("Common.Action.Confirm"));
            return;
        }

        CheckUpdate();
    }

    private enum UpdateStatus
    {
        Checking = 0,
        Available = 1,
        Error = 2,
        Latest = 3
    }
}