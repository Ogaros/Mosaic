﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.IO;

namespace Mosaic
{
    /// <summary>
    /// Interaction logic for ImageSourceWindow.xaml
    /// </summary>
    public partial class ImageSourceWindow : Window
    {
        private enum ErrorType { WrongSourceURL, SourceAlreadyIndexed, IndexingCancelled, NoSourceToRemove, IndexingInProgress, DirectoryNotFound }
        private Dictionary<ErrorType, String> errorMessages = new Dictionary<ErrorType, string>
        {
            {ErrorType.SourceAlreadyIndexed, "This source is already indexed. To reindex this source remove it and then add it again"},
            {ErrorType.WrongSourceURL, "This source can't be used to construct mosaic. Use any folder on your computer or a link to imgur gallery or album"},
            {ErrorType.IndexingCancelled, "Source indexing was cancelled"},
            {ErrorType.NoSourceToRemove, "Select sources to remove"},
            {ErrorType.IndexingInProgress, "You can't close this window while source indexing is in progress"},
            {ErrorType.DirectoryNotFound, "This directory was not found on your computer"}
        };

        private const String newSourceTBText = "New source path";
        public ObservableCollection<ImageSource> imageSources = null;
        private ImageIndexer indexer = null;
        private Regex regexDirectory = new Regex(@".:\\(.*\\)*(.*)");
        private Regex regexImgurGallery = new Regex(@"http:\/\/imgur.com\/gallery\/(.*)");
        private Regex regexImgurAlbum = new Regex(@"http:\/\/imgur.com\/a\/(.*)");

        public ImageSourceWindow()
        {
            DataContext = this;
            InitializeComponent();
            fillSourceList();
            lv_SourceList.ItemsSource = imageSources;        
        }

        private void fillSourceList()
        {
            imageSources = new ObservableCollection<ImageSource>(DBManager.getAllSources());
        }

        private void b_Ok_Click(object sender, RoutedEventArgs e)
        {
            foreach(ImageSource source in imageSources)
            {
                DBManager.updateIsUsedField(source);
            }
            this.DialogResult = true;
            this.Close();
        }

        private void tb_SourcePath_LostFocus(object sender, RoutedEventArgs e)
        {
            if (tb_SourcePath.Text == "")
                tb_SourcePath.Text = newSourceTBText;
        }

        private void tb_SourcePath_GotFocus(object sender, RoutedEventArgs e)
        {
            if (tb_SourcePath.Text == newSourceTBText)
                tb_SourcePath.Text = "";
        }

        private void b_RemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            hideErrorMessage();
            List<ImageSource> sourcesToDelete = new List<ImageSource>();
            foreach(ImageSource source in imageSources)
            {
                if (source.isUsed)
                {
                    sourcesToDelete.Add(source);                    
                }
            }
            if(sourcesToDelete.Count == 0)
            {
                showErrorMessage(ErrorType.NoSourceToRemove);
                return;
            }
            foreach(ImageSource source in sourcesToDelete)
            {
                DBManager.removeSource(source);
                imageSources.Remove(source);
            }
        }

        private async void b_AddNewSource_Click(object sender, RoutedEventArgs e)
        {
            hideErrorMessage();
            ImageSource source = null;
            if (fillSourceFromTextbox(ref source) == false)            
                return;            
            indexer = new ImageIndexer();
            g_IndexingGrid.DataContext = indexer;
            blockUI(true);
            showIndexingUI(true);

            int returnCode = await Task.Run(() => indexer.indexImages(source));
            switch (returnCode)
            {
                    // finished successfully
                case 0: imageSources.Add(source);
                    break;
                    // source already indexed
                case 1: showErrorMessage(ErrorType.SourceAlreadyIndexed);
                    break;
                    // indexing cancelled
                case 2:
                    {
                        DBManager.removeSource(source);
                        sp_ImageCountPanel.Visibility = System.Windows.Visibility.Visible;
                        l_IndexedImagePathLabel.SetBinding(Label.ContentProperty, new Binding("currentImagePath"));
                        showErrorMessage(ErrorType.IndexingCancelled);
                    }
                    break;
            }
            indexer = null;
            showIndexingUI(false);
            blockUI(false);     
        }

        private bool fillSourceFromTextbox(ref ImageSource source)
        {
            String path = tb_SourcePath.Text;
            String name;
            ImageSource.Type type;
            Match match;
            if ((match = regexDirectory.Match(path)).Success)
            {
                if(Directory.Exists(path) == false)
                {
                    showErrorMessage(ErrorType.DirectoryNotFound);
                    return false;
                }
                type = ImageSource.Type.Directory;
                name = match.Groups[2].Value;
                if (name == "")
                    name = path;
            }
            else if ((match = regexImgurAlbum.Match(path)).Success)
            {
                type = ImageSource.Type.ImgurAlbum;
                name = match.Groups[1].Value;
            }
            else if ((match = regexImgurGallery.Match(path)).Success)
            {
                type = ImageSource.Type.ImgurGallery;
                name = match.Groups[1].Value;
            }
            else
            {
                showErrorMessage(ErrorType.WrongSourceURL);
                return false;
            }
            source = new ImageSource(name, path, type, 0);
            return true;
        }

        private void b_CancelIndexing_Click(object sender, RoutedEventArgs e)
        {
            indexer.stopIndexing = true;
            sp_ImageCountPanel.Visibility = System.Windows.Visibility.Hidden;
            l_IndexedImagePathLabel.Content = "Cancelling source indexing...";
        }

        private void blockUI(bool isBlocked)
        {
            isBlocked = !isBlocked;
            lv_SourceList.IsEnabled = isBlocked;
            b_Ok.IsEnabled = isBlocked;
            b_Cancel.IsEnabled = isBlocked;
            b_RemoveSelected.IsEnabled = isBlocked;
            tb_SourcePath.IsEnabled = isBlocked;
            b_AddNewSource.IsEnabled = isBlocked;
        }

        private void showIndexingUI(bool show)
        {
            var visibility = show ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            g_IndexingGrid.Visibility = visibility;
        }      
  
        private void showErrorMessage(ErrorType errorType)
        {
            tblock_ErrorMessage.Text = errorMessages[errorType];
            tblock_ErrorMessage.Visibility = System.Windows.Visibility.Visible;
            System.Media.SystemSounds.Beep.Play();
        }

        private void hideErrorMessage()
        {
            tblock_ErrorMessage.Visibility = System.Windows.Visibility.Collapsed;
        }        

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if(indexer != null)
            {
                showErrorMessage(ErrorType.IndexingInProgress);                
                e.Cancel = true;
            }
        }
    }
}