using System;
using System.Collections.Generic;

namespace Mosaic
{
    enum ErrorType
    {
        NoErrors,
        // Errors that are displayed in the source selection window
        WrongSourceURI, SourceAlreadyIndexed, IndexingCancelled, NoSourceToRemove,
        IndexingInProgress, DirectoryNotFound, PartiallyIndexed, CantAccessSource,

        // Errors that are displayed in the main window
        WrongImageURI, EmptyImageURI, CantAccessImage, TooManySectors
    }
    class ErrorMessage
    {     
        public static String getMessage(ErrorType type)
        {
            return errorMessages[type];
        }
        private static Dictionary<ErrorType, String> errorMessages = new Dictionary<ErrorType, string>
        {
            {ErrorType.SourceAlreadyIndexed, "This source is already indexed. To reindex this source remove it and then add it again"},
            {ErrorType.WrongSourceURI, "This source can't be used to construct mosaic. Use any folder on your computer or a link to imgur gallery or album"},
            {ErrorType.IndexingCancelled, "Source indexing was cancelled"},
            {ErrorType.NoSourceToRemove, "Select sources to remove"},
            {ErrorType.IndexingInProgress, "You can't close this window while source indexing is in progress"},
            {ErrorType.DirectoryNotFound, "This directory was not found on your computer"},
            {ErrorType.PartiallyIndexed, "images in the source cannot be accessed"},
            {ErrorType.CantAccessSource, "Source can't be accessed or does not exist"},
            {ErrorType.WrongImageURI, "Wrong image URI. Use a path to the image on your computer or the internet link"},
            {ErrorType.EmptyImageURI, "No image was specified for mosaic"},
            {ErrorType.CantAccessImage, "Selected image can't be accessed or does not exist"},
            {ErrorType.TooManySectors, "Too many sectors for the image of this size"}
        };

    }
}
