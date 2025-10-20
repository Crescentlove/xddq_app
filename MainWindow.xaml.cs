using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Text;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace App_xddq
{
    public sealed partial class MainWindow : Window
    {
        private bool _sidebarExpanded = false;
        // Add shared services
        private readonly AdbService _adbService;
        private readonly ConfigManager _configManager;
        private readonly TaskExecutor _taskExecutor;
        private readonly SettingsManager _settingsManager;

        // live log textbox reference
        private TextBox _liveLogTextBox;

        public MainWindow()
        {
            this.InitializeComponent();
            // Initialize shared services
            _adbService = new AdbService();
            _configManager = new ConfigManager();
            _taskExecutor = new TaskExecutor(_adbService, _configManager);
            _settingsManager = new SettingsManager();

            // subscribe to realtime logs
            _taskExecutor.LogUpdated += OnLogUpdated;

            ShowHomePage();
        }

        private void OnLogUpdated(string line)
        {
            try
            {
                // update UI thread
                this.DispatcherQueue?.TryEnqueue(() =>
                {
                    if (_liveLogTextBox == null) return;
                    _liveLogTextBox.Text += line + "\n";
                    try
                    {
                        _liveLogTextBox.SelectionStart = _liveLogTextBox.Text.Length;
                        _liveLogTextBox.SelectionLength = 0;
                    }
                    catch { }
                });
            }
            catch { }
        }

        private void HamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            _sidebarExpanded = !_sidebarExpanded;
            Sidebar.Width = _sidebarExpanded ? 180 : 60;
            // 可选择性地显示/隐藏图标旁边的文本（尚未实现）
        }

        private void HomeNav_Click(object sender, RoutedEventArgs e) => ShowHomePage();
        private void FeatureNav_Click(object sender, RoutedEventArgs e) => ShowFeaturePage();
        private void ConfigNav_Click(object sender, RoutedEventArgs e) => ShowConfigPage();
        private void LogNav_Click(object sender, RoutedEventArgs e) => ShowLogPage();
        private void AboutNav_Click(object sender, RoutedEventArgs e) => ShowAboutPage();
        private void SettingsNav_Click(object sender, RoutedEventArgs e) => ShowSettingsPage();

        private void ShowHomePage()
        {
            var grid = new Grid { Margin = new Thickness(40) };
            grid.ColumnDefinitions.Add(new ColumnDefinition()); // 内容区
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 按钮区

            var infoPanel = new StackPanel();
            infoPanel.Children.Add(new TextBlock { Text = "首页", FontSize = 24, Margin = new Thickness(0,0,0,20) });
            infoPanel.Children.Add(new TextBlock { Text = "请先连接手机，连接后将显示设备信息。", FontSize = 16, Margin = new Thickness(0,0,0,10) });
            Grid.SetColumn(infoPanel, 0);
            grid.Children.Add(infoPanel);

            var connectBtn = new Button
            {
                Content = new StackPanel {
                    Orientation = Orientation.Horizontal,
                    Children = {
                        new FontIcon { Glyph = "\uE88B", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 20 }, // USB图标
                        new TextBlock { Text = "连接手机", Margin = new Thickness(5,0,0,0), FontSize = 14 }
                    }
                },
                Margin = new Thickness(0,0,0,0),
                VerticalAlignment = VerticalAlignment.Top
            };
            connectBtn.Click += async (s, e) => await ConnectPhoneAndShowInfo(infoPanel);
            Grid.SetColumn(connectBtn, 1);
            grid.Children.Add(connectBtn);

            MainFrame.Content = grid;
            // 程序启动自动连接
            _ = ConnectPhoneAndShowInfo(infoPanel);
        }

        private async Task ConnectPhoneAndShowInfo(StackPanel infoPanel)
        {
            infoPanel.Children.Clear();
            infoPanel.Children.Add(new TextBlock { Text = "首页", FontSize = 24, Margin = new Thickness(0,0,0,20) });
            // use shared adb service
            string devices = await _adbService.GetDevicesAsync();
            var lines = devices.Split('\n');
            string deviceId = null;
            foreach (var line in lines)
            {
                if (line.Contains("\tdevice"))
                {
                    deviceId = line.Split('\t')[0].Trim();
                    break;
                }
            }
            if (string.IsNullOrEmpty(deviceId))
            {
                infoPanel.Children.Add(new TextBlock { Text = "未检测到设备，请检查USB连接和授权。", FontSize = 16, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0)) });
                return;
            }
            infoPanel.Children.Add(new TextBlock { Text = $"已连接设备: {deviceId}", FontSize = 16, Margin = new Thickness(0,10,0,0) });
            // 查询所有可用信息
            await AddDeviceInfoAsync(infoPanel, _adbService, deviceId);
        }

        private async Task AddDeviceInfoAsync(StackPanel infoPanel, AdbService adb, String deviceId)
        {
            // 分辨率
            string resolution = await adb.RunAdbCommandAsync($"-s {deviceId} shell wm size");
            // 设备名
            string model = await adb.RunAdbCommandAsync($"-s {deviceId} shell getprop ro.product.model");
            // 品牌
            string brand = await adb.RunAdbCommandAsync($"-s {deviceId} shell getprop ro.product.brand");
            // Android版本
            string androidVer = await adb.RunAdbCommandAsync($"-s {deviceId} shell getprop ro.build.version.release");
            // 序列号
            string serial = await adb.RunAdbCommandAsync($"-s {deviceId} shell getprop ro.serialno");
            // IMEI
            string imei = await adb.RunAdbCommandAsync($"-s {deviceId} shell service call iphonesubinfo 1");
            // 电量
            string battery = await adb.RunAdbCommandAsync($"-s {deviceId} shell dumpsys battery | findstr level");
            // 是否root
            string root = await adb.RunAdbCommandAsync($"-s {deviceId} shell su -c 'id' 2>&1");

            var card = new Border {
                BorderThickness = new Thickness(2),
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 215)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20),
                Margin = new Thickness(0,10,0,0),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 0, 120, 215))
            };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = $"分辨率: {resolution.Trim()}", FontSize = 16 });
            stack.Children.Add(new TextBlock { Text = $"设备名: {model.Trim()}", FontSize = 16 });
            stack.Children.Add(new TextBlock { Text = $"品牌: {brand.Trim()}", FontSize = 16 });
            stack.Children.Add(new TextBlock { Text = $"Android版本: {androidVer.Trim()}", FontSize = 16 });
            stack.Children.Add(new TextBlock { Text = $"序列号: {serial.Trim()}", FontSize = 16 });
            stack.Children.Add(new TextBlock { Text = $"IMEI: {imei.Trim()}", FontSize = 16 });
            stack.Children.Add(new TextBlock { Text = $"电量: {battery.Trim()}", FontSize = 16 });
            stack.Children.Add(new TextBlock { Text = $"Root权限: {(root.Contains("uid=0") ? "是" : "否")}", FontSize = 16 });
            card.Child = stack;
            infoPanel.Children.Add(card);
        }

        private void ShowFeaturePage()
        {
            var scroll = new ScrollViewer { Margin = new Thickness(20) };
            var panel = new StackPanel();

            var topBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,10) };
            var stopBtn = new Button { Content = "停止执行", Margin = new Thickness(0,0,10,0) };
            stopBtn.Click += (s, e) => { _taskExecutor.Stop(); };
            topBar.Children.Add(stopBtn);
            panel.Children.Add(topBar);

            panel.Children.Add(CreateFeatureSection("日常任务", new[] {
                "砍树", "超值礼包", "仙缘", "邮件领取", "轮回殿", "座驾注灵", "道友", "仙树等级"
            }));
            panel.Children.Add(CreateFeatureSection("挑战副本", new[] {
                "斗法挑战", "妖王挑战", "异兽挑战", "镇妖塔挑战", "星辰挑战", "诸天挑战", "法则挑战", "元辰试炼"
            }));
            panel.Children.Add(CreateFeatureSection("资源收集", new[] {
                "道途试炼", "宗门任务", "仙友游历", "仙宫点赞"
            }));
            panel.Children.Add(CreateFeatureSection("功能系统", new[] {
                "内丹凝聚", "神通领取", "法宝寻宝", "法象功能", "玄诀修炼", "神躯修炼"
            }));
            panel.Children.Add(CreateFeatureSection("妖盟专区", new[] {
                "妖盟悬赏", "妖邪挑战", "砍价任务", "妖盟商店", "妖盟排行"
            }));
            panel.Children.Add(CreateFeatureSection("其他功能", new[] {
                "自动领桃子"
            }));
            scroll.Content = panel;
            MainFrame.Content = scroll;
        }

        private UIElement CreateFeatureSection(string title, string[] items)
        {
            var border = new Border { BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 221, 221, 221)), CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 10, 0, 0), Padding = new Thickness(10) };
            var stack = new StackPanel();
            // create itemsPanel early so header handlers can access it
            var itemsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };

            var header = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var selectAll = new CheckBox { Content = "", VerticalAlignment = VerticalAlignment.Center };
            // properly toggle checkboxes in itemsPanel
            selectAll.Checked += (s, e) => { foreach (var cb in itemsPanel.Children.OfType<CheckBox>()) cb.IsChecked = true; };
            selectAll.Unchecked += (s, e) => { foreach (var cb in itemsPanel.Children.OfType<CheckBox>()) cb.IsChecked = false; };
            header.Children.Add(selectAll);
            header.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.Bold, FontSize = 16, Margin = new Thickness(5, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            var autoBtn = new Button { Content = "自动执行", Margin = new Thickness(20, 0, 0, 0) };
            // wire auto execute to TaskExecutor
            autoBtn.Click += async (s, e) =>
            {
                try
                {
                    autoBtn.IsEnabled = false;
                    // collect selected items from itemsPanel
                    if (itemsPanel == null)
                    {
                        await ShowInfoDialog("未找到项目列表。");
                        return;
                    }

                    var selected = itemsPanel.Children.OfType<CheckBox>().Where(cb => cb.IsChecked == true).Select(cb => cb.Tag as string ?? cb.Content?.ToString()).Where(n => !string.IsNullOrEmpty(n)).ToList();
                    if (!selected.Any())
                    {
                        await ShowInfoDialog("请先勾选要执行的功能。");
                        return;
                    }

                    // map UI name to funcSteps key: append '功能' if not present
                    var funcNames = selected.Select(n => n.EndsWith("功能") ? n : n + "功能").ToList();

                    // run sequentially
                    var log = await _taskExecutor.RunMultipleFuncsAsync(funcNames);
                    await ShowInfoDialog(log);
                }
                catch (Exception ex)
                {
                    await ShowInfoDialog("执行出错: " + ex.Message);
                }
                finally
                {
                    autoBtn.IsEnabled = true;
                }
            };

            header.Children.Add(autoBtn);
            stack.Children.Add(header);

            foreach (var name in items)
            {
                var cb = new CheckBox { Content = name };
                // store the actual func key in Tag for future flexibility
                cb.Tag = name;
                itemsPanel.Children.Add(cb);
            }
            stack.Children.Add(itemsPanel);
            border.Child = stack;
            return border;
        }

        private async Task ShowInfoDialog(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "提示",
                Content = message,
                CloseButtonText = "确定",
                XamlRoot = this.Content.XamlRoot // 关键修正
            };
            await dialog.ShowAsync();
        }

        private void ShowConfigPage()
        {
            var scroll = new ScrollViewer { Margin = new Thickness(20) };
            var panel = new StackPanel { Spacing = 10 };

            var header = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            header.Children.Add(new TextBlock { Text = "配置区（上传截图与坐标分析）", FontSize = 24, Margin = new Thickness(0, 0, 20, 0) });
            var saveAllBtn = new Button { Content = "保存配置", Margin = new Thickness(0, 0, 10, 0) };
            saveAllBtn.Click += async (s, e) =>
            {
                var ok = _configManager.Save();
                await ShowInfoDialog(ok ? "保存成功" : "保存失败");
            };
            header.Children.Add(saveAllBtn);

            var addSectionBtn = new Button { Content = "添加分区" };
            addSectionBtn.Click += async (s, e) =>
            {
                var dlg = new ContentDialog { Title = "添加分区", PrimaryButtonText = "确定", CloseButtonText = "取消", XamlRoot = this.Content.XamlRoot };
                var box = new TextBox { PlaceholderText = "分区名称" };
                dlg.Content = box;
                var res = await dlg.ShowAsync();
                if (res == ContentDialogResult.Primary)
                {
                    var name = box.Text?.Trim();
                    if (!string.IsNullOrEmpty(name))
                    {
                        // ensure section exists by adding a dummy key then removing it
                        _configManager.AddOrUpdatePosition(name, "_placeholder", 0, 0);
                        _configManager.RemovePosition(name, "_placeholder");
                        ShowConfigPage();
                    }
                }
            };
            header.Children.Add(addSectionBtn);

            panel.Children.Add(header);

            var all = _configManager.GetAll();
            foreach (var sec in all)
            {
                var border = new Border { BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 200, 200, 200)), CornerRadius = new CornerRadius(6), Padding = new Thickness(10) };
                var secStack = new StackPanel();

                var secHeader = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                secHeader.Children.Add(new TextBlock { Text = sec.Key, FontSize = 18, FontWeight = FontWeights.Bold });
                var addKeyBtn = new Button { Content = "添加键", Margin = new Thickness(10, 0, 0, 0) };
                addKeyBtn.Click += async (s, e) =>
                {
                    var dlg = new ContentDialog { Title = $"在 {sec.Key} 添加键", PrimaryButtonText = "确定", CloseButtonText = "取消", XamlRoot = this.Content.XamlRoot };
                    var stackInput = new StackPanel();
                    var keyBox = new TextBox { PlaceholderText = "键名" };
                    var xBox = new TextBox { PlaceholderText = "x" };
                    var yBox = new TextBox { PlaceholderText = "y" };
                    stackInput.Children.Add(keyBox);
                    stackInput.Children.Add(xBox);
                    stackInput.Children.Add(yBox);
                    dlg.Content = stackInput;
                    var res = await dlg.ShowAsync();
                    if (res == ContentDialogResult.Primary)
                    {
                        if (string.IsNullOrWhiteSpace(keyBox.Text) || !int.TryParse(xBox.Text, out var xi) || !int.TryParse(yBox.Text, out var yi))
                        {
                            await ShowInfoDialog("请输入有效的键名和坐标（整数）。");
                        }
                        else
                        {
                            _configManager.AddOrUpdatePosition(sec.Key, keyBox.Text.Trim(), xi, yi);
                            ShowConfigPage();
                        }
                    }
                };
                secHeader.Children.Add(addKeyBtn);

                var removeSecBtn = new Button { Content = "删除分区", Margin = new Thickness(10, 0, 0, 0) };
                removeSecBtn.Click += async (s, e) =>
                {
                    var dlg = new ContentDialog { Title = $"确认删除分区 {sec.Key}？", PrimaryButtonText = "删除", CloseButtonText = "取消", XamlRoot = this.Content.XamlRoot };
                    var res = await dlg.ShowAsync();
                    if (res == ContentDialogResult.Primary)
                    {
                        // remove all keys under section
                        var keys = sec.Value.Keys.ToList();
                        foreach (var k in keys) _configManager.RemovePosition(sec.Key, k);
                        ShowConfigPage();
                    }
                };
                secHeader.Children.Add(removeSecBtn);

                secStack.Children.Add(secHeader);

                foreach (var key in sec.Value)
                {
                    var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                    row.Children.Add(new TextBlock { Text = key.Key, Width = 180, VerticalAlignment = VerticalAlignment.Center });
                    var xBox = new TextBox { Width = 80, Text = key.Value.x.ToString(), Margin = new Thickness(8, 0, 0, 0) };
                    var yBox = new TextBox { Width = 80, Text = key.Value.y.ToString(), Margin = new Thickness(8, 0, 0, 0) };
                    var updateBtn = new Button { Content = "更新", Margin = new Thickness(8, 0, 0, 0) };
                    var removeBtn = new Button { Content = "删除", Margin = new Thickness(8, 0, 0, 0) };

                    updateBtn.Click += async (s, e) =>
                    {
                        if (!int.TryParse(xBox.Text, out var nx) || !int.TryParse(yBox.Text, out var ny))
                        {
                            await ShowInfoDialog("坐标必须为整数。");
                            return;
                        }
                        _configManager.AddOrUpdatePosition(sec.Key, key.Key, nx, ny);
                        var ok = _configManager.Save();
                        await ShowInfoDialog(ok ? "更新并保存成功" : "更新成功但保存失败");
                        ShowConfigPage();
                    };

                    removeBtn.Click += async (s, e) =>
                    {
                        var dlg = new ContentDialog { Title = $"确认删除 {key.Key}？", PrimaryButtonText = "删除", CloseButtonText = "取消", XamlRoot = this.Content.XamlRoot };
                        var res = await dlg.ShowAsync();
                        if (res == ContentDialogResult.Primary)
                        {
                            _configManager.RemovePosition(sec.Key, key.Key);
                            var ok = _configManager.Save();
                            await ShowInfoDialog(ok ? "删除并保存成功" : "删除成功但保存失败");
                            ShowConfigPage();
                        }
                    };

                    row.Children.Add(xBox);
                    row.Children.Add(yBox);
                    row.Children.Add(updateBtn);
                    row.Children.Add(removeBtn);
                    secStack.Children.Add(row);

                    var pickBtn = new Button { Content = "拾取", Margin = new Thickness(8, 0, 0, 0) };

                    pickBtn.Click += async (s, e) =>
                    {
                        // get first connected device id
                        string devices = await _adbService.GetDevicesAsync();
                        var linesDev = devices.Split('\n');
                        string deviceId = null;
                        foreach (var ln in linesDev)
                        {
                            if (ln.Contains("\tdevice"))
                            {
                                deviceId = ln.Split('\t')[0].Trim();
                                break;
                            }
                        }
                        if (string.IsNullOrEmpty(deviceId))
                        {
                            await ShowInfoDialog("未检测到设备，无法截图。");
                            return;
                        }

                        try
                        {
                            var tmpDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tmp");
                            Directory.CreateDirectory(tmpDir);
                            var localPath = Path.Combine(tmpDir, $"screenshot_{Guid.NewGuid()}.png");

                            // take screenshot on device and pull
                            await _adbService.RunAdbCommandAsync($"-s {deviceId} shell screencap -p /sdcard/tmp_screenshot.png");
                            await _adbService.RunAdbCommandAsync($"-s {deviceId} pull /sdcard/tmp_screenshot.png \"{localPath}\"");
                            await _adbService.RunAdbCommandAsync($"-s {deviceId} shell rm /sdcard/tmp_screenshot.png");

                            if (!File.Exists(localPath))
                            {
                                await ShowInfoDialog("截图获取失败。");
                                return;
                            }

                            var dlg = new ContentDialog { Title = "点击图片以拾取坐标", CloseButtonText = "取消", XamlRoot = this.Content.XamlRoot };
                            var img = new Image { Stretch = Stretch.Uniform, MaxHeight = 600, MaxWidth = 800 };

                            var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri($"file:///{localPath.Replace('\\','/')}"));
                            img.Source = bmp;

                            double imgPixelW = 0, imgPixelH = 0;
                            img.ImageOpened += (snd, ev) =>
                            {
                                try
                                {
                                    imgPixelW = bmp.PixelWidth;
                                    imgPixelH = bmp.PixelHeight;
                                }
                                catch { }
                            };

                            img.PointerPressed += (snd, ev) =>
                            {
                                try
                                {
                                    var pt = ev.GetCurrentPoint(img).Position;
                                    double controlW = img.ActualWidth;
                                    double controlH = img.ActualHeight;
                                    if (imgPixelW <= 0 || imgPixelH <= 0 || controlW <= 0 || controlH <= 0)
                                    {
                                        return;
                                    }

                                    var ratio = Math.Min(controlW / imgPixelW, controlH / imgPixelH);
                                    var displayedW = imgPixelW * ratio;
                                    var displayedH = imgPixelH * ratio;
                                    var offsetX = (controlW - displayedW) / 2.0;
                                    var offsetY = (controlH - displayedH) / 2.0;

                                    if (pt.X < offsetX || pt.X > offsetX + displayedW || pt.Y < offsetY || pt.Y > offsetY + displayedH)
                                    {
                                        // clicked outside image area
                                        return;
                                    }

                                    var rx = (pt.X - offsetX) / ratio;
                                    var ry = (pt.Y - offsetY) / ratio;

                                    xBox.Text = ((int)Math.Round(rx)).ToString();
                                    yBox.Text = ((int)Math.Round(ry)).ToString();

                                    // close dialog
                                    dlg.Hide();
                                }
                                catch { }
                            };

                            dlg.Content = new ScrollViewer { Content = img, HorizontalScrollMode = ScrollMode.Auto, VerticalScrollMode = ScrollMode.Auto };
                            await dlg.ShowAsync();

                            try { File.Delete(localPath); } catch { }
                        }
                        catch (Exception ex)
                        {
                            await ShowInfoDialog("截图失败: " + ex.Message);
                        }
                    };

                    row.Children.Add(pickBtn);
                }

                border.Child = secStack;
                panel.Children.Add(border);
            }

            scroll.Content = panel;
            MainFrame.Content = scroll;
        }

        private void ShowLogPage()
        {
            var panel = new StackPanel { Margin = new Thickness(40) };
            panel.Children.Add(new TextBlock { Text = "日志区", FontSize = 24, Margin = new Thickness(0,0,0,20) });

            // Top buttons: export and clear
            var btnBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            var exportBtn = new Button { Content = "导出日志", Margin = new Thickness(0, 0, 10, 0) };
            exportBtn.Click += async (s, e) =>
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "tmp", "app.log");
                if (!File.Exists(logPath))
                {
                    await ShowInfoDialog("日志文件不存在，无法导出。");
                    return;
                }
                try
                {
                    var exportDir = _settingsManager.GetExportPath();
                    if (string.IsNullOrWhiteSpace(exportDir)) exportDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    if (!Directory.Exists(exportDir)) Directory.CreateDirectory(exportDir);
                    var dest = Path.Combine(exportDir, $"app_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                    File.Copy(logPath, dest, true);
                    await ShowInfoDialog($"已导出到: {dest}");

                    try
                    {
                        // open explorer and select the file
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"/select,\"{dest}\"",
                            UseShellExecute = true
                        };
                        System.Diagnostics.Process.Start(psi);
                    }
                    catch { /* ignore UI open failures */ }
                }
                catch (Exception ex)
                {
                    await ShowInfoDialog("导出失败: " + ex.Message);
                }
            };
            btnBar.Children.Add(exportBtn);

            var clearBtn = new Button { Content = "清除日志" };
            clearBtn.Click += async (s, e) =>
            {
                var dlg = new ContentDialog { Title = "确认清除日志？", PrimaryButtonText = "确定", CloseButtonText = "取消", XamlRoot = this.Content.XamlRoot };
                var res = await dlg.ShowAsync();
                if (res == ContentDialogResult.Primary)
                {
                    string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "tmp", "app.log");
                    try
                    {
                        if (File.Exists(logPath)) File.WriteAllText(logPath, string.Empty);
                        if (_liveLogTextBox != null) _liveLogTextBox.Text = string.Empty;
                        await ShowInfoDialog("日志已清除。");
                    }
                    catch (Exception ex)
                    {
                        await ShowInfoDialog("清除失败: " + ex.Message);
                    }
                }
            };
            btnBar.Children.Add(clearBtn);

            panel.Children.Add(btnBar);

            var logText = new TextBox { FontSize = 14, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, IsReadOnly = true, Height = 400 };
            logText.Text = LoadLog();
            _liveLogTextBox = logText; // save reference for live updates
            panel.Children.Add(logText);
            MainFrame.Content = panel;
        }

        private string LoadLog()
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "tmp", "app.log");
            try
            {
                if (File.Exists(logPath))
                    return File.ReadAllText(logPath);
            }
            catch { }
            return "日志文件未找到。";
        }

        private void ShowAboutPage()
        {
            var scroll = new ScrollViewer { Margin = new Thickness(40) };
            var panel = new StackPanel();
            var readmeText = new TextBlock {
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Text = LoadReadme()
            };
            panel.Children.Add(readmeText);
            scroll.Content = panel;
            MainFrame.Content = scroll;
        }

        private void ShowSettingsPage()
        {
            var panel = new StackPanel { Margin = new Thickness(40) };
            panel.Children.Add(new TextBlock { Text = "设置区", FontSize = 24, Margin = new Thickness(0, 0, 0, 20) });

            panel.Children.Add(new TextBlock { Text = "导出日志路径：", FontSize = 16 });
            var pathBox = new TextBox { Text = _settingsManager.GetExportPath(), Width = 600 };
            panel.Children.Add(pathBox);

            var chooseBtn = new Button { Content = "选择路径", Margin = new Thickness(0, 10, 0, 0) };
            chooseBtn.Click += async (s, e) =>
            {
                var picker = new Windows.Storage.Pickers.FolderPicker();
                // Need to initialize with window handle for WinUI3 desktop apps; use helper
                WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
                var folder = await picker.PickSingleFolderAsync();
                if (folder != null)
                {
                    pathBox.Text = folder.Path;
                    _settingsManager.SetExportPath(folder.Path);
                    await ShowInfoDialog("已设置导出路径。");
                }
            };
            panel.Children.Add(chooseBtn);

            MainFrame.Content = panel;
        }

        private string LoadReadme()
        {
            // 先尝试输出目录
            string outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "README.md");
            if (File.Exists(outputPath))
                return File.ReadAllText(outputPath);

            // 再尝试项目根目录
            try
            {
                var dir = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory);
                if (dir != null && dir.Parent != null && dir.Parent.Parent != null)
                {
                    string projectPath = Path.Combine(dir.Parent.Parent.FullName, "README.md");
                    if (File.Exists(projectPath))
                        return File.ReadAllText(projectPath);
                }
            }
            catch { }
            return "README文件未找到。";
        }
    }
}
