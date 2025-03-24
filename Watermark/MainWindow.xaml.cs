using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Wpf.Ui.Appearance;
using static System.Windows.Forms.AxHost;

namespace Watermark;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    private readonly MainWindowViewModel _viewModel;
    public MainWindow()
    {
        _viewModel = new MainWindowViewModel();
        this.DataContext = _viewModel;
        InitializeComponent();
        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(
          Wpf.Ui.Appearance.ApplicationTheme.Dark, // Theme type
          Wpf.Ui.Controls.WindowBackdropType.Mica,  // Background type
          true                                      // Whether to change accents automatically
        );
        
    }

    private void ListBox_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            _viewModel.Items = [.. files];
        }
    }

    private void Grid_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0)
            {
                _viewModel.WatermarkPath = files[0];
            }
        }
    }
    


}

public partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel()
    {
        WatermarkPath = Properties.Settings.Default.WatermarkPath;
        SelectedWatermarkPosition = (WatermarkPosition)Properties.Settings.Default.WatermarkPosition;
        WatermarkWidthRatio = Properties.Settings.Default.WatermarkWidthRatio;
        OffsetX = Properties.Settings.Default.OffsetX;
        OffsetY = Properties.Settings.Default.OffsetY;
        RotationAngle = Properties.Settings.Default.RotationAngle;
        ImagePadding = Properties.Settings.Default.ImagePadding;
    }

    [NotifyPropertyChangedFor(nameof(ShowItemsText))]
    [ObservableProperty]
    public partial List<string>? Items { get; set; }

    public Visibility ShowItemsText => Items is null || Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    public partial string? SelectedItem { get; set; }

    [NotifyPropertyChangedFor(nameof(ShowWatermarkPathText))]
    [ObservableProperty]
    public partial string? WatermarkPath { get; set; }

    public Visibility ShowWatermarkPathText => string.IsNullOrEmpty(WatermarkPath) ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    public partial BitmapSource? Watermark { get; set; }

    [ObservableProperty]
    public partial double WatermarkWidthRatio { get; set; } = 0.2;

    [ObservableProperty]
    public partial double OffsetX { get; set; }

    [ObservableProperty]
    public partial double OffsetY { get; set; }

    [ObservableProperty] public partial double RotationAngle { get; set; } = 0;

    [ObservableProperty] public partial int OutputQuality { get; set; } = 90;

    [ObservableProperty] public partial double ImagePadding { get; set; } = 20;

    [NotifyPropertyChangedFor(nameof(ShowProgress))]
    [NotifyPropertyChangedFor(nameof(ReRuning))]
    [ObservableProperty]
    public partial bool IsRuning { get; set; }

    public bool ReRuning => !IsRuning;

    public Visibility ShowProgress => IsRuning ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    public partial int Progress { get; set; }
    public Array WatermarkPositions { get; } = Enum.GetValues(typeof(WatermarkPosition));

    [ObservableProperty]
    public partial WatermarkPosition SelectedWatermarkPosition { get; set; } = WatermarkPosition.居中;

    partial void OnSelectedItemChanged(string? value)
    {
        _ = AddWatermarkToImage();
    }

    partial void OnWatermarkWidthRatioChanged(double value)
    {
        Properties.Settings.Default.WatermarkWidthRatio = value;
        Properties.Settings.Default.Save();
        _ = AddWatermarkToImage();
    }

    partial void OnWatermarkPathChanged(string? value)
    {
        Properties.Settings.Default.WatermarkPath = value;
        Properties.Settings.Default.Save();
        _ = AddWatermarkToImage();
    }

    partial void OnOffsetXChanged(double value)
    {
        Properties.Settings.Default.OffsetX = value;
        Properties.Settings.Default.Save();
        _ = AddWatermarkToImage();
    }

    partial void OnOffsetYChanged(double value)
    {
        Properties.Settings.Default.OffsetY = value;
        Properties.Settings.Default.Save();
        _ = AddWatermarkToImage();
    }

    partial void OnSelectedWatermarkPositionChanged(WatermarkPosition value)
    {
        Properties.Settings.Default.WatermarkPosition = (int)value;
        Properties.Settings.Default.Save();
        _ = AddWatermarkToImage();
    }

    partial void OnRotationAngleChanged(double value)
    {
        Properties.Settings.Default.RotationAngle = value;
        Properties.Settings.Default.Save();
        _ = AddWatermarkToImage();
    }

    partial void OnImagePaddingChanged(double value)
    {
        Properties.Settings.Default.ImagePadding = value;
        Properties.Settings.Default.Save();
        _ = AddWatermarkToImage();
    }

    partial void OnOutputQualityChanged(int value)
    {
        Properties.Settings.Default.OutputQuality = value;
        Properties.Settings.Default.Save();
    }

    [RelayCommand(IncludeCancelCommand = true)] 
    async Task Start(CancellationToken token)
    {
        if(Items is null || Items.Count == 0)
        {
            var uiMessageBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = "提示",
                Content = "请放入左侧图像后再继续",
                
            };
            _ = await uiMessageBox.ShowDialogAsync();
        }

        if (string.IsNullOrEmpty(WatermarkPath))
        {
            var uiMessageBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = "提示",
                Content = "请放入水印图像后再继续",
            };
            _ = await uiMessageBox.ShowDialogAsync();
        }

        var dir = SelectDir();
        if (string.IsNullOrEmpty(dir)) return;
        await Task.Run(async () =>
        {
            try
            {
                IsRuning = true;
                Progress = 0;
                foreach (var item in Items)
                {
                    Progress++;
                    var newFile = System.IO.Path.Combine(dir, System.IO.Path.GetFileName(item));
                    var bitmap = AddWatermarkToImage(item, WatermarkPath, WatermarkWidthRatio, SelectedWatermarkPosition, OffsetX, OffsetY);
                    SaveFile(bitmap, newFile);
                }
            }
            catch (Exception ex)
            {
                var uiMessageBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "提示",
                    Content = ex,
                };
                _ = await uiMessageBox.ShowDialogAsync();
            }
            finally
            {
                IsRuning = false;
                Progress = 0;
            }
        });
    }

    private string? SelectDir(string? initialFolder = null)
    {
        CommonOpenFileDialog dialog = new CommonOpenFileDialog();
        dialog.IsFolderPicker = true;
        if (!string.IsNullOrEmpty(initialFolder))
        {
            dialog.InitialDirectory = initialFolder;
        }

        dialog.Title = "请选择目录";
        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            return dialog.FileName;
        }
        else
        {
            return null;
        }
    }

    private Nito.AsyncEx.AsyncLock _lock = new Nito.AsyncEx.AsyncLock();
    private CancellationTokenSource? _cts = null;

    private async Task AddWatermarkToImage()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        if (!string.IsNullOrEmpty(SelectedItem) && !string.IsNullOrEmpty(WatermarkPath))
        {
            using (await _lock.LockAsync(token))
            {
                var res = await Task.Run(() => AddWatermarkToImage(SelectedItem, WatermarkPath, WatermarkWidthRatio, SelectedWatermarkPosition, OffsetX, OffsetY));
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Watermark = res;
                });
            }
        }
    }

    public void SaveFile(BitmapSource bitmapSource, string savePath)
    {
        BitmapEncoder encoder;
        switch (System.IO.Path.GetExtension(savePath).ToLower())
        {
            case ".jpeg":
            case ".jpg":
                JpegBitmapEncoder jpegEncoder = new JpegBitmapEncoder();
                jpegEncoder.QualityLevel = OutputQuality;
                encoder = jpegEncoder;
                break;
            case ".bmp":
                encoder = new BmpBitmapEncoder();
                break;
            case ".png":
            default:
                encoder = new PngBitmapEncoder();
                break;
        }

        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

        using (var fileStream = new FileStream(savePath, FileMode.Create))
        {
            encoder.Save(fileStream);
        }
    }

    public enum WatermarkPosition
    {
        上左,
        上中,
        上右,
        中左,
        居中,
        中右,
        下左,
        下中,
        下右,
        平铺,
        错位平铺
    }

    private BitmapSource AddWatermarkToImage(string imagePath, string watermarkPath, double watermarkWidthRatio, WatermarkPosition position, double offsetX, double offsetY)
    {
        BitmapImage bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.UriSource = new Uri(imagePath);
        bitmapImage.EndInit();
        bitmapImage.Freeze(); // 确保跨线程访问安全

        BitmapImage watermarkImage = new BitmapImage();
        watermarkImage.BeginInit();
        watermarkImage.CacheOption = BitmapCacheOption.OnLoad;
        watermarkImage.UriSource = new Uri(watermarkPath);
        watermarkImage.EndInit();
        watermarkImage.Freeze(); // 确保跨线程访问安全

        DrawingVisual visual = new DrawingVisual();
        using (DrawingContext context = visual.RenderOpen())
        {
            // 绘制原始图像
            context.DrawImage(bitmapImage, new Rect(0, 0, bitmapImage.Width, bitmapImage.Height));

            double targetWatermarkWidth = bitmapImage.Width * watermarkWidthRatio;
            double scale = targetWatermarkWidth / watermarkImage.Width;
            double targetWatermarkHeight = watermarkImage.Height * scale;

            if (position == WatermarkPosition.平铺)
            {
                // 平铺水印
                DrawTiledWatermark(context, watermarkImage, bitmapImage.Width, bitmapImage.Height,
                    targetWatermarkWidth, targetWatermarkHeight, offsetX, offsetY, RotationAngle, ImagePadding);
            }
            else if (position == WatermarkPosition.错位平铺)
            {
                // 错位平铺水印
                DrawStaggeredTiledWatermark(context, watermarkImage, bitmapImage.Width, bitmapImage.Height,
                    targetWatermarkWidth, targetWatermarkHeight, offsetX, offsetY, RotationAngle, ImagePadding);
            }
            else
            {
                // 单个水印
                Point watermarkPosition = CalculateWatermarkPosition(bitmapImage.Width, bitmapImage.Height,
                    targetWatermarkWidth, targetWatermarkHeight, position, offsetX, offsetY);

                // 计算实际水印大小（Padding越大，水印越小）
                double actualWatermarkWidth = Math.Max(1, targetWatermarkWidth - ImagePadding * 2);
                double actualWatermarkHeight = Math.Max(1, targetWatermarkHeight - ImagePadding * 2);

                // 应用旋转
                if (RotationAngle != 0)
                {
                    // 保存当前绘图状态
                    context.PushTransform(new TranslateTransform(
                        watermarkPosition.X + targetWatermarkWidth / 2,
                        watermarkPosition.Y + targetWatermarkHeight / 2));
                    context.PushTransform(new RotateTransform(RotationAngle));
                    context.PushTransform(new TranslateTransform(
                        -actualWatermarkWidth / 2,
                        -actualWatermarkHeight / 2));

                    // 绘制旋转后的水印
                    context.DrawImage(watermarkImage, new Rect(0, 0, actualWatermarkWidth, actualWatermarkHeight));

                    // 恢复绘图状态
                    context.Pop();
                    context.Pop();
                    context.Pop();
                }
                else
                {
                    // 计算带padding的水印位置，保持中心点不变
                    double paddedPosX = watermarkPosition.X + (targetWatermarkWidth - actualWatermarkWidth) / 2;
                    double paddedPosY = watermarkPosition.Y + (targetWatermarkHeight - actualWatermarkHeight) / 2;

                    // 不旋转直接绘制
                    Rect watermarkRect = new Rect(paddedPosX, paddedPosY, actualWatermarkWidth, actualWatermarkHeight);
                    context.DrawImage(watermarkImage, watermarkRect);
                }
            }
        }

        var renderBitmap = new RenderTargetBitmap(
            (int)bitmapImage.PixelWidth,
            (int)bitmapImage.PixelHeight,
            bitmapImage.DpiX,
            bitmapImage.DpiY,
            PixelFormats.Pbgra32);
        renderBitmap.Render(visual);
        renderBitmap.Freeze(); // 确保跨线程访问安全

        return renderBitmap;
    }

    private void DrawTiledWatermark(DrawingContext context, BitmapImage watermarkImage,
        double imageWidth, double imageHeight, double watermarkWidth, double watermarkHeight,
        double offsetX, double offsetY, double rotationAngle = 0, double padding = 0)
    {
        // 计算水平和垂直方向需要的水印数量
        int horizontalCount = (int)Math.Ceiling(imageWidth / (watermarkWidth + offsetX));
        int verticalCount = (int)Math.Ceiling(imageHeight / (watermarkHeight + offsetY));

        // 计算实际水印大小（Padding越大，水印越小）
        double actualWatermarkWidth = Math.Max(1, watermarkWidth - padding * 2);
        double actualWatermarkHeight = Math.Max(1, watermarkHeight - padding * 2);

        // 绘制平铺水印
        for (int y = 0; y < verticalCount; y++)
        {
            for (int x = 0; x < horizontalCount; x++)
            {
                double posX = x * (watermarkWidth + offsetX);
                double posY = y * (watermarkHeight + offsetY);

                // 确保水印不会超出图像边界
                if (posX < imageWidth && posY < imageHeight)
                {
                    if (rotationAngle != 0)
                    {
                        // 保存当前绘图状态
                        context.PushTransform(new TranslateTransform(
                            posX + watermarkWidth / 2,
                            posY + watermarkHeight / 2));
                        context.PushTransform(new RotateTransform(rotationAngle));
                        context.PushTransform(new TranslateTransform(
                            -actualWatermarkWidth / 2,
                            -actualWatermarkHeight / 2));

                        // 绘制旋转后的水印，考虑padding
                        context.DrawImage(watermarkImage, new Rect(0, 0, actualWatermarkWidth, actualWatermarkHeight));

                        // 恢复绘图状态
                        context.Pop();
                        context.Pop();
                        context.Pop();
                    }
                    else
                    {
                        // 计算带padding的水印位置，保持中心点不变
                        double paddedPosX = posX + (watermarkWidth - actualWatermarkWidth) / 2;
                        double paddedPosY = posY + (watermarkHeight - actualWatermarkHeight) / 2;

                        Rect watermarkRect = new Rect(paddedPosX, paddedPosY, actualWatermarkWidth, actualWatermarkHeight);
                        context.DrawImage(watermarkImage, watermarkRect);
                    }
                }
            }
        }
    }

    private void DrawStaggeredTiledWatermark(DrawingContext context, BitmapImage watermarkImage,
        double imageWidth, double imageHeight, double watermarkWidth, double watermarkHeight,
        double offsetX, double offsetY, double rotationAngle = 0, double padding = 0)
    {
        // 计算水平和垂直方向需要的水印数量
        int horizontalCount = (int)Math.Ceiling(imageWidth / (watermarkWidth + offsetX)) + 1; // 多加一列以确保覆盖
        int verticalCount = (int)Math.Ceiling(imageHeight / (watermarkHeight + offsetY));

        // 计算实际水印大小（Padding越大，水印越小）
        double actualWatermarkWidth = Math.Max(1, watermarkWidth - padding * 2);
        double actualWatermarkHeight = Math.Max(1, watermarkHeight - padding * 2);

        // 绘制错位平铺水印
        for (int y = 0; y < verticalCount; y++)
        {
            // 偶数行错位
            double rowOffset = (y % 2 == 0) ? 0 : (watermarkWidth + offsetX) / 2;

            for (int x = 0; x < horizontalCount; x++)
            {
                double posX = x * (watermarkWidth + offsetX) - rowOffset;
                double posY = y * (watermarkHeight + offsetY);

                // 确保水印不会超出图像左边界，但允许部分水印在右边界外
                if (posX + watermarkWidth > 0 && posX < imageWidth && posY < imageHeight)
                {
                    if (rotationAngle != 0)
                    {
                        // 保存当前绘图状态
                        context.PushTransform(new TranslateTransform(
                            posX + watermarkWidth / 2,
                            posY + watermarkHeight / 2));
                        context.PushTransform(new RotateTransform(rotationAngle));
                        context.PushTransform(new TranslateTransform(
                            -actualWatermarkWidth / 2,
                            -actualWatermarkHeight / 2));

                        // 绘制旋转后的水印，考虑padding
                        context.DrawImage(watermarkImage, new Rect(0, 0, actualWatermarkWidth, actualWatermarkHeight));

                        // 恢复绘图状态
                        context.Pop();
                        context.Pop();
                        context.Pop();
                    }
                    else
                    {
                        // 计算带padding的水印位置，保持中心点不变
                        double paddedPosX = posX + (watermarkWidth - actualWatermarkWidth) / 2;
                        double paddedPosY = posY + (watermarkHeight - actualWatermarkHeight) / 2;

                        Rect watermarkRect = new Rect(paddedPosX, paddedPosY, actualWatermarkWidth, actualWatermarkHeight);
                        context.DrawImage(watermarkImage, watermarkRect);
                    }
                }
            }
        }
    }

    private Point CalculateWatermarkPosition(double imageWidth, double imageHeight, double watermarkWidth, double watermarkHeight, WatermarkPosition position, double offsetX, double offsetY)
    {
        double x = 0, y = 0;
        double margin = ImagePadding;

        switch (position)
        {
            case WatermarkPosition.上左:
                x = margin + offsetX;
                y = margin + offsetY;
                break;
            case WatermarkPosition.上中:
                x = (imageWidth - watermarkWidth) / 2 + offsetX;
                y = margin + offsetY;
                break;
            case WatermarkPosition.上右:
                x = imageWidth - watermarkWidth - margin + offsetX;
                y = margin + offsetY;
                break;
            case WatermarkPosition.中左:
                x = margin + offsetX;
                y = (imageHeight - watermarkHeight) / 2 + offsetY;
                break;
            case WatermarkPosition.居中:
                x = (imageWidth - watermarkWidth) / 2 + offsetX;
                y = (imageHeight - watermarkHeight) / 2 + offsetY;
                break;
            case WatermarkPosition.中右:
                x = imageWidth - watermarkWidth - margin + offsetX;
                y = (imageHeight - watermarkHeight) / 2 + offsetY;
                break;
            case WatermarkPosition.下左:
                x = margin + offsetX;
                y = imageHeight - watermarkHeight - margin + offsetY;
                break;
            case WatermarkPosition.下中:
                x = (imageWidth - watermarkWidth) / 2 + offsetX;
                y = imageHeight - watermarkHeight - margin + offsetY;
                break;
            case WatermarkPosition.下右:
                x = imageWidth - watermarkWidth - margin + offsetX;
                y = imageHeight - watermarkHeight - margin + offsetY;
                break;
        }

        // 确保水印位置没有超出图像边界
        if (x < 0) x = 0;
        if (y < 0) y = 0;
        if (x + watermarkWidth > imageWidth) x = imageWidth - watermarkWidth;
        if (y + watermarkHeight > imageHeight) y = imageHeight - watermarkHeight;

        return new Point(x, y);
    }
}

public class ImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return ConvertToImageSource(value?.ToString(), 180);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    public ImageSource? ConvertToImageSource(string? imageUrl, int? decodePixelWidth = null)
    {
        try
        {
            if (string.IsNullOrEmpty(imageUrl)) return null;
            bool isHttp = imageUrl.StartsWith("http");
            bool isData = imageUrl.StartsWith("data:image/");
            if (!isHttp && !isData && !File.Exists(imageUrl)) throw new Exception("文件不存在");
            BitmapImage bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            if (decodePixelWidth is not null)
                bitmapImage.DecodePixelWidth = (int)decodePixelWidth;

            if (isHttp)
            {
                bitmapImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmapImage.UriSource = new Uri(imageUrl);
                bitmapImage.EndInit();
                return bitmapImage;
            }
            else if (isData)
            {
                string base64String = imageUrl.Split(',').Last();
                byte[] imageBytes = System.Convert.FromBase64String(base64String);
                using (MemoryStream stream = new MemoryStream(imageBytes))
                {
                    bitmapImage.StreamSource = stream;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                    return bitmapImage;
                }
            }
            else
            {
                bitmapImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmapImage.UriSource = new Uri(imageUrl);
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                return bitmapImage;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("转换图像失败: " + ex.Message);
            return null;
        }
    }
}
