using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mosaic
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const double zoomIncrementPixels = 50;

        private MosaicBuilder mosaicBuilder = new MosaicBuilder();
        private double zoomValue = 1.0;
        private double zoomIncrement = 0.2;
        private Point dragMousePoint = new Point();
        private double dragHorizontalOffset = 1;
        private double dragVerticalOffset = 1;
        private bool isDragEnabled = false;
        private List<ImageSource> imageSources;
        private bool repeatImageSetup = true;

        public MainWindow()
        {
            InitializeComponent();
            DBManager.OpenDBConnection();
            imageSources = DBManager.GetUsedSources();
            DataContext = mosaicBuilder;
            tb_ResolutionW.Text = SystemParameters.VirtualScreenWidth.ToString();
            tb_ResolutionH.Text = SystemParameters.VirtualScreenHeight.ToString();
        }

        private void BlockUI(bool isBlocked)
        {
            isBlocked = !isBlocked;
            b_Construct.IsEnabled = isBlocked;
            b_SaveMosaic.IsEnabled = isBlocked;
            b_SelectSources.IsEnabled = isBlocked;
            if (mosaicBuilder.Original != null)
            {
                rb_MosaicView.IsEnabled = isBlocked;
                rb_OriginalImageView.IsEnabled = isBlocked;
            }
            tb_SectorsNumHorizontal.IsEnabled = isBlocked;
            tb_SectorsNumVertical.IsEnabled = isBlocked;
            tb_ResolutionH.IsEnabled = isBlocked;
            tb_ResolutionW.IsEnabled = isBlocked;
            tb_URLBox.IsEnabled = isBlocked;
        }

        private async void b_Construct_Click(object sender, RoutedEventArgs e)
        {            
            HideErrorMessage();
            BlockUI(true);
            BitmapImage bi = null;
            int secHorizontal, secVertical, resolutionW, resolutionH;
            try
            {
                bi = new BitmapImage(new Uri(tb_URLBox.Text));
                mosaicBuilder.SetImage(bi);
                // for some reason first time bitmap image is set, the resolution is wrong and the resulting mosaic looks bigger than the original image
                // of the same size. I don't know why it's happening so i just restart the builder after a short delay when the button is clicked for the first time.
                // Restarting instantly doesn't work.
                if (repeatImageSetup)
                {
                    await Task.Delay(500);
                    repeatImageSetup = false;
                    b_Construct_Click(sender, e);
                    return;
                }
                secHorizontal = Convert.ToInt32(tb_SectorsNumHorizontal.Text);
                secVertical = Convert.ToInt32(tb_SectorsNumVertical.Text);
                resolutionW = Convert.ToInt32(tb_ResolutionW.Text);
                resolutionH = Convert.ToInt32(tb_ResolutionH.Text);
            }
            catch (Exception ex)
            {
                if (ex is UriFormatException)
                {
                    if (tb_URLBox.Text == "")
                        ShowErrorMessage(ErrorType.EmptyImageURI);
                    else
                        ShowErrorMessage(ErrorType.WrongImageURI);
                }
                else if (ex is System.IO.FileNotFoundException || ex is System.Net.WebException)
                    ShowErrorMessage(ErrorType.CantAccessImage);
                else if (ex is FormatException)
                    ShowErrorMessage(ErrorType.WrongMosaicParameter);
                else
                    throw;
                BlockUI(false);
                return;
            }

            if ((bool)rb_OriginalImageView.IsChecked)
                i_Image.Source = mosaicBuilder.Original;
            else
                rb_OriginalImageView.IsChecked = true;
            SetZoomIncrement();

            if (resolutionH == 0 || resolutionW == 0)
            {
                ShowErrorMessage(ErrorType.ZeroMosaicResolution);
                BlockUI(false);
                return;
            }            
            ShowProgressBar(true);           
            l_StatusLabel.Content = "Constructing mosaic...";
            await Task.Run(() => mosaicBuilder.BuildMosaic(resolutionW, resolutionH, secHorizontal, secVertical, imageSources));       
            BlockUI(false);
            ShowProgressBar(false);  
            if(mosaicBuilder.ErrorStatus != ErrorType.NoErrors)
            {
                ShowErrorMessage(mosaicBuilder.ErrorStatus);
                return;
            }

            if((bool)rb_MosaicView.IsChecked)
                i_Image.Source = mosaicBuilder.Mosaic;
            else
                rb_MosaicView.IsChecked = true;
            SetZoomIncrement();
                       
            l_StatusLabel.Content = "Mosaic constructed";
        }    
        
        private void SetZoomIncrement()
        {
            zoomIncrement = zoomIncrementPixels / i_Image.Source.Width; 
        }

        private void b_SelectSources_Click(object sender, RoutedEventArgs e)
        {
            HideErrorMessage();
            ImageSourceWindow window = new ImageSourceWindow();
            window.Owner = this;
            window.Left = this.Left + ((this.Width / 2) - (window.Width / 2));
            window.Top = this.Top + ((this.Height / 2) - (window.Height / 2));                
            if(window.ShowDialog() == true)
            {
                imageSources = DBManager.GetUsedSources();
            }   
        }

        private void b_SaveMosaic_Click(object sender, RoutedEventArgs e)
        {
            HideErrorMessage();
            var dialog = new Microsoft.Win32.SaveFileDialog();
            dialog.Filter = "JPEG image (*.jpg;*.jpeg)|*.jpg;*.jpeg|" +
                            "Portable Network Graphics (*.png)|*.png|" +
                            "Bitmap image (*.bmp)|*.bmp|" +
                            "Tagged Image File Format (*.tiff)|*.tiff|" +
                            "Graphics Interchange Format (*.gif)|*.gif";
            if(dialog.ShowDialog() == true)
            {
                String format = dialog.FileName.Substring(dialog.FileName.LastIndexOf('.'));
                BitmapEncoder encoder = null;
                switch (format)
                {
                    case ".jpg": 
                        encoder = new JpegBitmapEncoder();
                        break;
                    case ".png": 
                        encoder = new PngBitmapEncoder();
                        break;
                    case ".bmp": 
                        encoder = new BmpBitmapEncoder();
                        break;
                    case ".tiff":
                        encoder = new TiffBitmapEncoder();
                        break;
                    case ".gif":
                        encoder = new GifBitmapEncoder();
                        break;
                }
                encoder.Frames.Add(BitmapFrame.Create(mosaicBuilder.Mosaic));
                using (FileStream stream = new FileStream(dialog.FileName, FileMode.Create))
                {
                    encoder.Save(stream);
                }
            }
            l_StatusLabel.Content = "Mosaic saved as " + dialog.FileName;
        }        

        private void ShowErrorMessage(ErrorType errorType)
        {
            l_StatusLabel.Content = "There was an error during mosaic construction";
            tblock_ErrorMessage.Text = ErrorMessage.GetMessage(errorType);
            tblock_ErrorMessage.Visibility = System.Windows.Visibility.Visible;
            System.Media.SystemSounds.Beep.Play();
        }

        private void HideErrorMessage()
        {
            l_StatusLabel.Content = "";
            tblock_ErrorMessage.Visibility = System.Windows.Visibility.Hidden;
        }        

        private void ShowProgressBar(bool show)
        {
            if (show)
            {
                pb_MosaicProgress.Visibility = System.Windows.Visibility.Visible;
                TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;
            }
            else
            {
                pb_MosaicProgress.Visibility = System.Windows.Visibility.Collapsed;
                TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
            }
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (isDragEnabled == false)
            {
                if (e.Delta > 0)
                    zoomValue += zoomIncrement;
                else
                {
                    if (zoomValue > 0.0)
                        zoomValue -= zoomIncrement;
                }
                ScaleTransform scale = new ScaleTransform(zoomValue, zoomValue);
                i_Image.LayoutTransform = scale;
            }
            e.Handled = true;
        }

        private void ScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            sv_ScrollViewer.CaptureMouse();
            dragMousePoint = e.GetPosition(sv_ScrollViewer);
            dragHorizontalOffset = sv_ScrollViewer.HorizontalOffset;
            dragVerticalOffset = sv_ScrollViewer.VerticalOffset;
            isDragEnabled = true;
        }

        private void ScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            sv_ScrollViewer.ReleaseMouseCapture();
            isDragEnabled = false;
        }

        private void sv_ScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (isDragEnabled && sv_ScrollViewer.IsMouseCaptured)
            {
                sv_ScrollViewer.ScrollToHorizontalOffset(dragHorizontalOffset + (dragMousePoint.X - e.GetPosition(sv_ScrollViewer).X));
                sv_ScrollViewer.ScrollToVerticalOffset(dragVerticalOffset + (dragMousePoint.Y - e.GetPosition(sv_ScrollViewer).Y));
            }
        }

        private void rb_MosaicView_Checked(object sender, RoutedEventArgs e)
        {
            i_Image.Source = mosaicBuilder.Mosaic;
        }

        private void rb_OriginalImageView_Checked(object sender, RoutedEventArgs e)
        {
            i_Image.Source = mosaicBuilder.Original;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            DBManager.CloseDBConnection();
            mosaicBuilder.Dispose();
        }

        private static void CheckIfNumeric(TextCompositionEventArgs e)
        {
            char c = Convert.ToChar(e.Text);
            if (Char.IsNumber(c))
                e.Handled = false;
            else
                e.Handled = true;
        }

        private static void CheckIfSpace(KeyEventArgs e)
        {
            e.Handled = (e.Key == Key.Space);
        }

        private void tb_SectorsNumHorizontal_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            CheckIfNumeric(e);
        }

        private void tb_SectorsNumVertical_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            CheckIfNumeric(e);
        }

        private void tb_ResolutionW_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            CheckIfNumeric(e);
        }

        private void tb_ResolutionH_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            CheckIfNumeric(e);
        }

        private void tb_SectorsNumHorizontal_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            CheckIfSpace(e);
        }

        private void tb_SectorsNumVertical_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            CheckIfSpace(e);
        }

        private void tb_ResolutionW_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            CheckIfSpace(e);
        }

        private void tb_ResolutionH_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            CheckIfSpace(e);
        }

        private void pb_MosaicProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            TaskbarItemInfo.ProgressValue = pb_MosaicProgress.Value / pb_MosaicProgress.Maximum;
        }
        
    }
}
