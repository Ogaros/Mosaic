﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;

namespace Mosaic
{
    /// <summary>
    /// Interaction logic for ImageSourceWindow.xaml
    /// </summary>
    public partial class ImageSourceWindow : Window
    {
        private const String newSourceTBText = "New source path";
        private ObservableCollection<ImageSource> imageSources = null;
        private ImageIndexer indexer = null;
        private readonly Regex regexDirectory = new Regex(@".:\\(.*\\)*(.*)");
        private readonly Regex regexImgurGallery = new Regex(@"https?:\/\/imgur.com\/gallery\/(.*)");
        private readonly Regex regexImgurAlbum = new Regex(@"https?:\/\/imgur.com\/a\/(.*)");

        public ImageSourceWindow()
        {
            DataContext = this;
            InitializeComponent();
            FillSourceList();
            lv_SourceList.ItemsSource = imageSources;                       
        }

        private void FillSourceList()
        {
            imageSources = new ObservableCollection<ImageSource>(DBManager.GetAllSources());
        }

        private void b_Ok_Click(object sender, RoutedEventArgs e)
        {
            foreach(ImageSource source in imageSources)
            {
                DBManager.UpdateIsUsedField(source);
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
            HideErrorMessage();            
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
                ShowErrorMessage(ErrorType.NoSourceToRemove);
                return;
            }
            if (MessageBox.Show("Are you sure you want to remove selected sources?\n" +
                                "You will be unable to use them for mosaic.\n" +
                                "To use selected sources again, you will have to re-index them",
                                "Confirm removal", MessageBoxButton.YesNo) == MessageBoxResult.No)
                return;
            foreach(ImageSource source in sourcesToDelete)
            {
                DBManager.RemoveSource(source);
                imageSources.Remove(source);
            }
        }

        private async void b_AddNewSource_Click(object sender, RoutedEventArgs e)
        {
            HideErrorMessage();             
            ImageSource source = null;
            if (FillSourceFromTextbox(ref source) == false) // error with the source URI           
                return;            
            indexer = new ImageIndexer();
            g_IndexingGrid.DataContext = indexer;
            BlockUI(true);
            ShowIndexingUI(true);

            await Task.Run(() => indexer.IndexImages(source));
            switch (indexer.ErrorStatus)
            {
                case ErrorType.NoErrors:
                    {
                        imageSources.Add(source);
                        break;
                    }
                case ErrorType.IndexingCancelled:
                    {
                        // Restore image path label that was used to show cancelling message
                        sp_ImageCountPanel.Visibility = System.Windows.Visibility.Visible;
                        l_IndexedImagePathLabel.SetBinding(Label.ContentProperty, new Binding("CurrentImagePath"));
                        ShowErrorMessage(indexer.ErrorStatus);
                        break;
                    }
                case ErrorType.PartiallyIndexed:
                    {
                        ShowErrorMessage(indexer.ErrorStatus, indexer.FailedToIndex.ToString());
                        break;
                    }
                default:
                    {
                        ShowErrorMessage(indexer.ErrorStatus);
                        break;
                    }
            }
            indexer.Dispose();
            indexer = null;
            ShowIndexingUI(false);
            BlockUI(false);            
        }

        private bool FillSourceFromTextbox(ref ImageSource source)
        {
            String path = tb_SourcePath.Text;
            String name = "";
            ImageSource.Type type;
            Match match;
            if ((match = regexDirectory.Match(path)).Success)
            {
                if (Directory.Exists(path) == false)
                {
                    ShowErrorMessage(ErrorType.DirectoryNotFound);
                    return false;
                }
                type = ImageSource.Type.Directory;
                name = match.Groups[2].Value;
                if (name == "")
                    name = path;
            } 
                // Names for imgur albums and galleries are set during image indexing to avoid extra api calls
            else if ((match = regexImgurAlbum.Match(path)).Success)
            {
                type = ImageSource.Type.ImgurAlbum;
            }
            else if ((match = regexImgurGallery.Match(path)).Success)
            {
                type = ImageSource.Type.ImgurGallery;
            }
            else
            {
                ShowErrorMessage(ErrorType.WrongSourceURI);
                return false;
            }
            source = new ImageSource(name, path, type, 0);
            return true;
        }

        private void b_CancelIndexing_Click(object sender, RoutedEventArgs e)
        {
            indexer.StopIndexing = true;
            sp_ImageCountPanel.Visibility = System.Windows.Visibility.Hidden;
            l_IndexedImagePathLabel.Content = "Cancelling source indexing...";
        }

        private void BlockUI(bool isBlocked)
        {
            isBlocked = !isBlocked;
            lv_SourceList.IsEnabled = isBlocked;
            b_Ok.IsEnabled = isBlocked;
            b_Cancel.IsEnabled = isBlocked;
            b_RemoveSelected.IsEnabled = isBlocked;
            tb_SourcePath.IsEnabled = isBlocked;
            b_AddNewSource.IsEnabled = isBlocked;
        }

        private void ShowIndexingUI(bool show)
        {
            var visibility = show ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            g_IndexingGrid.Visibility = visibility;
            if(show)
                Owner.TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;
            else
                Owner.TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
        }      
  
        private void ShowErrorMessage(ErrorType errorType, String parameter = "")
        {
            tblock_ErrorMessage.Text = ErrorMessage.GetMessage(errorType);
            if (errorType == ErrorType.PartiallyIndexed) // Add number of images that cannot be accessed
                tblock_ErrorMessage.Text = parameter + " " + tblock_ErrorMessage.Text;
            tblock_ErrorMessage.Visibility = System.Windows.Visibility.Visible;
            System.Media.SystemSounds.Beep.Play();
        }

        private void HideErrorMessage()
        {
            tblock_ErrorMessage.Visibility = System.Windows.Visibility.Collapsed;
        }        

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if(indexer != null)
            {
                ShowErrorMessage(ErrorType.IndexingInProgress);                
                e.Cancel = true;
            }
        }

        private void pb_IndexingProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Owner.TaskbarItemInfo.ProgressValue = pb_IndexingProgress.Value / pb_IndexingProgress.Maximum;
        }
    }
}
