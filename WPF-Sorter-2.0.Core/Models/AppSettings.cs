using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.Generic;

namespace WPF_Sorter_2._0.Core.Models;

public class AppSettings
{
    // 👇 Настройки категорий
    public List<FileCategory> Categories { get; set; } = new();

    // 👇 Настройки темы
    public string Theme { get; set; } = "Default";

    // 👇 Последняя выбранная папка
    public string LastSelectedFolder { get; set; } = string.Empty;

    // 👇 Настройки сортировки
    public bool IncludeSubfolders { get; set; } = true;
    public bool MoveInsteadOfCopy { get; set; } = true;
    public bool OverwriteExisting { get; set; } = false;

    // 👇 Ветка обновлений
    public string UpdateBranch { get; set; } = "Main";

    // 👇 Версия настроек (для будущей миграции)
    public int Version { get; set; } = 1;
}
