using System.IO;
using WPF_Sorter_2._0.Core.Models;

namespace WPF_Sorter_2._0.Services
{
    public class CloudProviderService
    {
        public Dictionary<CloudProvider, string> GetCloudPaths()
        {
            var result = new Dictionary<CloudProvider, string>();

            // OneDrive
            var oneDrive = Environment.GetEnvironmentVariable("OneDrive");
            if (!string.IsNullOrEmpty(oneDrive) && Directory.Exists(oneDrive))
                result.Add(CloudProvider.OneDrive, oneDrive);

            // Google Drive
            var googleDrive = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Google Drive");
            if (Directory.Exists(googleDrive))
                result.Add(CloudProvider.GoogleDrive, googleDrive);

            // Dropbox
            var dropbox = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Dropbox");
            if (Directory.Exists(dropbox))
                result.Add(CloudProvider.Dropbox, dropbox);

            // Яндекс.Диск
            var yandex = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "YandexDisk");
            if (Directory.Exists(yandex))
                result.Add(CloudProvider.YandexDisk, yandex);

            return result;
        }
    }
}
