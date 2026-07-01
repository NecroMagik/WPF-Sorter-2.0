using System;
using System.Collections.Generic;
using System.Text;

namespace WPF_Sorter_2._0.Core.Models
{
    public class FileCategory
    {
        public string Name { get; set; } = string.Empty;
        public string IconGlyph { get; set; } = string.Empty;
        public List<string> Extensions { get; set; } = new();
        public string DefaultPath { get; set; } = string.Empty;
        public string CustomPath { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;

        public bool IsDefaultPath => CustomPath == DefaultPath;
    }
}
