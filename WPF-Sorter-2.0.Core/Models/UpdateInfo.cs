using System;
using System.Collections.Generic;
using System.Text;

namespace WPF_Sorter_2._0.Core.Models
{
    public class UpdateInfo
    {
        public string Version { get; set; } = string.Empty;
        public string ReleaseDate { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string Changelog { get; set; } = string.Empty;
        public string AssetName { get; set; } = string.Empty;
        public string AssetHash { get; set; } = string.Empty;
        public long AssetSize { get; set; }
        public bool IsPrerelease { get; set; }
        public bool IsNewer { get; set; }
    }
}
