using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Text;
using System;
using System.IO;
using System.Threading.Tasks;

namespace App_xddq
{
    public sealed partial class MainWindow : Window
    {
        private bool _sidebarExpanded = false;
        public MainWindow()
        {
            this.InitializeComponent();
            ShowHomePage();
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
            var adb = new AdbService();
            string devices = await adb.GetDevicesAsync();
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
            await AddDeviceInfoAsync(infoPanel, adb, deviceId);
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
            var header = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var selectAll = new CheckBox { Content = "", VerticalAlignment = VerticalAlignment.Center };
            selectAll.Checked += (s, e) => { foreach (var cb in stack.Children) if (cb is CheckBox box) box.IsChecked = true; };
            selectAll.Unchecked += (s, e) => { foreach (var cb in stack.Children) if (cb is CheckBox box) box.IsChecked = false; };
            header.Children.Add(selectAll);
            header.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.Bold, FontSize = 16, Margin = new Thickness(5, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            var autoBtn = new Button { Content = "自动执行", Margin = new Thickness(20, 0, 0, 0) };
            autoBtn.Click += async (s, e) => { await ShowInfoDialog(title + "自动执行（未实现）"); };
            header.Children.Add(autoBtn);
            stack.Children.Add(header);
            var itemsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            foreach (var name in items)
                itemsPanel.Children.Add(new CheckBox { Content = name });
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

        private void ShowConfigPage() => MainFrame.Content = new TextBlock { Text = "配置区（上传截图与坐标分析）", FontSize = 24, Margin = new Thickness(40) };

        private void ShowLogPage()
        {
            var panel = new StackPanel { Margin = new Thickness(40) };
            panel.Children.Add(new TextBlock { Text = "日志区", FontSize = 24, Margin = new Thickness(0,0,0,20) });
            var logText = new TextBox { FontSize = 14, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, IsReadOnly = true, Height = 400 };
            logText.Text = LoadLog();
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
            panel.Children.Add(new TextBlock { Text = "设置区", FontSize = 24, Margin = new Thickness(0,0,0,20) });
            panel.Children.Add(new TextBlock { Text = "配色风格：", FontSize = 16 });
            var colorCombo = new ComboBox { Width = 180, Margin = new Thickness(0,5,0,20) };
            colorCombo.Items.Add("默认");
            colorCombo.Items.Add("深色");
            colorCombo.Items.Add("浅色");
            colorCombo.SelectedIndex = 0;
            colorCombo.SelectionChanged += ColorCombo_SelectionChanged;
            panel.Children.Add(colorCombo);
            panel.Children.Add(new TextBlock { Text = "图标风格：", FontSize = 16 });
            var iconCombo = new ComboBox { Width = 180, Margin = new Thickness(0,5,0,20) };
            iconCombo.Items.Add("默认");
            iconCombo.Items.Add("圆形");
            iconCombo.Items.Add("方形");
            iconCombo.SelectedIndex = 0;
            iconCombo.SelectionChanged += IconCombo_SelectionChanged;
            panel.Children.Add(iconCombo);
            MainFrame.Content = panel;
        }

        private void ColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = sender as ComboBox;
            switch (combo.SelectedIndex)
            {
                case 1:// 深色
                    Sidebar.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 45, 45, 48));
                    break;
                case 2:// 浅色
                    Sidebar.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 240, 240, 240));
                    break;
                default:
                    Sidebar.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 45, 45, 48));
                    break;
            }
        }

        private void IconCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 这里只是预留，后续可根据选择切换不同风格的图标
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
