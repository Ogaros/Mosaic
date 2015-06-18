using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Microsoft.Win32;
using System.Drawing.Imaging;

namespace Mosaic
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const double zoomIncrementPixels = 50;

        private MosaicBuilder mosaicBuilder = new MosaicBuilder();
        private ImageIndexer imageIndexer = new ImageIndexer();
        private double zoomValue = 1.0;
        private double zoomIncrement = 0.2;
        private Point dragMousePoint = new Point();
        private double dragHorizontalOffset = 1;
        private double dragVerticalOffset = 1;
        private bool isDragEnabled = false;
        private String imageDirectoryPath = @"D:\Projects\Mosaic\Images";//@"D:\Users\Ogare\Pictures\Desktop Images";
        private int imageCount = 0;


        public MainWindow()
        {
            InitializeComponent();
            l_StatusLabel.Content = "Image directory: " + imageDirectoryPath;
        }

        private void blockUI(bool isBlocked)
        {
            isBlocked = !isBlocked;
            b_Construct.IsEnabled = isBlocked;
            b_SaveMosaic.IsEnabled = isBlocked;
            b_SelectImagesFolder.IsEnabled = isBlocked;
            rb_MosaicView.IsEnabled = isBlocked;
            rb_OriginalImageView.IsEnabled = isBlocked;
            tb_SectorsNumHorizontal.IsEnabled = isBlocked;
            tb_SectorsNumVertical.IsEnabled = isBlocked;
            tb_ResolutionH.IsEnabled = isBlocked;
            tb_ResolutionW.IsEnabled = isBlocked;
            tb_URLBox.IsEnabled = isBlocked;
        }

        public void updateStatusText(int imageNumber, String imageTitle)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                l_StatusLabel.Content = "Indexing: " + imageTitle + " (" + imageNumber + " out of " + imageCount + ")";
            }));            
        }

        private async void b_Construct_Click(object sender, RoutedEventArgs e)
        {
            blockUI(true);

            DBManager.databaseCheck();
            imageCount = imageIndexer.countImages(imageDirectoryPath);
            setupProgressBar(imageCount, imageIndexer);

            await Task.Run(() => imageIndexer.indexImages(updateStatusText));
            
            int secHorizontal = Convert.ToInt32(tb_SectorsNumHorizontal.Text);
            int secVertical   = Convert.ToInt32(tb_SectorsNumVertical.Text);
            int resolutionW = Convert.ToInt32(tb_ResolutionW.Text);
            int resolutionH = Convert.ToInt32(tb_ResolutionH.Text);
            setupProgressBar(secHorizontal * secVertical, mosaicBuilder);

            BitmapImage bi = new BitmapImage(new Uri(tb_URLBox.Text));            
            mosaicBuilder.setImage(bi);
            l_StatusLabel.Content = "Constructing mosaic...";
            await Task.Run(() => mosaicBuilder.buildMosaic(resolutionW, resolutionH, secHorizontal, secVertical, imageDirectoryPath));
            
            blockUI(false);

            if((bool)rb_MosaicView.IsChecked)
                i_Image.Source = mosaicBuilder.getMosaic();
            else
                rb_MosaicView.IsChecked = true;

            zoomIncrement = zoomIncrementPixels / i_Image.Source.Width;
            
            pb_MosaicProgress.Visibility = Visibility.Collapsed;
            l_StatusLabel.Content = "Image directory: " + imageDirectoryPath;
            DBManager.closeDBConnection();
        }

        private void setupProgressBar(int maximum, object dataContext)
        {
            pb_MosaicProgress.DataContext = dataContext;
            pb_MosaicProgress.Maximum = maximum;
            pb_MosaicProgress.Value = 0;
            pb_MosaicProgress.Visibility = Visibility.Visible;
        }

        private void tb_URLBox_GotMouseCapture(object sender, MouseEventArgs e)
        {
            tb_URLBox.SelectAll();            
        }

        private void rb_MosaicView_Checked(object sender, RoutedEventArgs e)
        {
            i_Image.Source = mosaicBuilder.getMosaic();
        }

        private void rb_OriginalImageView_Checked(object sender, RoutedEventArgs e)
        {
            i_Image.Source = mosaicBuilder.getOriginal();
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (rb_MosaicView.IsEnabled)
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

        private void b_SelectImagesFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.ShowNewFolderButton = false;
            dialog.Description = "Select a folder that contains images that will be used to construct mosaic:";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                imageDirectoryPath = dialog.SelectedPath;            
        }

        private void b_SaveMosaic_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog();
            dialog.Filter = "JPEG image (*.jpg;*.jpeg)|*.jpg;*.jpeg|" +
                            "Portable Network Graphics (*.png)|*.png|" +
                            "Bitmap image (*.bmp)|*.bmp|" +
                            "Tagged Image File Format (*.tiff)|*.tiff|" +
                            "Graphics Interchange Format (*.gif)|*.gif";
            if(dialog.ShowDialog() == true)
            {
                ImageFormat imageFormat = null;
                String format = dialog.FileName.Substring(dialog.FileName.LastIndexOf('.'));
                switch (format)
                {
                    case ".jpg": imageFormat = ImageFormat.Jpeg;
                        break;
                    case ".png": imageFormat = ImageFormat.Png;
                        break;
                    case ".bmp": imageFormat = ImageFormat.Bmp;
                        break;
                    case ".tiff": imageFormat = ImageFormat.Tiff;
                        break;
                    case ".gif": imageFormat = ImageFormat.Gif;
                        break;
                }
                mosaicBuilder.mosaicBitmap.Save(dialog.FileName, imageFormat);
            }
        }       
        
    }
}
