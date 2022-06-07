using System.Collections.Generic;
using System.IO;

namespace Ryujinx.Ui.Common.Helper
{
    public interface IFileSystemHelper
    {
        Stream GetContentStream(string uri);
        string[] GetFileEntries(string directory, string search);
        string[] GetDirectories(string directory);
        bool FileExist(string uri);
        bool IsFileHidden(string uri);
        bool DirectoryExist(string directory);
        long GetFileLength(string file);
        IEnumerable<string> GetFileEntries(string directory, string search, SearchOption searchOption);
    }
}