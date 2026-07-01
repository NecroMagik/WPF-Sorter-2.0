using System;
using System.Collections.Generic;
using System.Text;

namespace WPF_Sorter_2._0.Core.Models
{
    public class UserSettings
    {
        public CloudProvider SelectedCloud { get; set; } = CloudProvider.None;
        public List<FileCategory> Categories { get; set; } = new();
        public string UpdateBranch { get; set; } = "Main";
        public bool CheckForUpdatesOnStart { get; set; } = true;
    }
}
