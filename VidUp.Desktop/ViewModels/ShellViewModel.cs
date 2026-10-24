using System.Collections.ObjectModel;

namespace VidUp.Desktop.ViewModels;

public sealed class ShellViewModel : ObservableObject
{
    // This PoC has no repository, credential store, network client or persistent settings.
    private readonly List<VideoItem> items = [];
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mkv", ".mov", ".avi", ".webm", ".m4v", ".wmv", ".mpeg", ".mpg" };
    private string searchText = "";
    private string selectedSection = "アップロード";
    private VideoItem? selectedVideo;
    private string notice = "サンプルデータを表示中です。動画はメモリ内だけに追加され、保存・送信されません。";

    public IReadOnlyList<string> Sections { get; } =
        ["アップロード", "テンプレート", "公開スケジュール", "再生リスト", "アカウント", "設定 / ヘルプ"];
    public ObservableCollection<VideoItem> VisibleVideos { get; } = [];
    public string SearchText
    {
        get => searchText;
        set { if (Set(ref searchText, value)) Refresh(); }
    }
    public string SelectedSection
    {
        get => selectedSection;
        set
        {
            if (!Set(ref selectedSection, value)) return;
            Notify(nameof(IsUploadSection));
            Notify(nameof(IsPlaceholderSection));
        }
    }
    public bool IsUploadSection => SelectedSection == "アップロード";
    public bool IsPlaceholderSection => !IsUploadSection;
    public VideoItem? SelectedVideo { get => selectedVideo; set => Set(ref selectedVideo, value); }
    public string Notice { get => notice; private set => Set(ref notice, value); }
    public string QueueSummary => $"{items.Count} 件中 {VisibleVideos.Count} 件表示 · 送信機能は無効";
    public bool IsEmpty => VisibleVideos.Count == 0;

    public ShellViewModel()
    {
        items.AddRange([
            new("制作日記 #01 — 新しいプロジェクト", "サンプル / 実ファイルではありません", "制作チャンネル（サンプル）", "制作日記", "2026/11/01 18:00 JST（例）", "サンプル"),
            new("週末の散歩 — 川沿いの風景", "サンプル / 実ファイルではありません", "日常チャンネル（サンプル）", "週末の記録", "未設定", "サンプル"),
            new("チュートリアル #03 — 公開前の確認", "サンプル / 実ファイルではありません", "制作チャンネル（サンプル）", "チュートリアル", "未設定", "サンプル")
        ]);
        Refresh();
        SelectedVideo = VisibleVideos.FirstOrDefault();
    }

    public void AddFiles(IEnumerable<string> paths)
    {
        var added = 0;
        var skipped = 0;
        // Windows paths are case-insensitive; Unix paths must not be conflated.
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var known = new HashSet<string>(items.Select(x => x.FilePath), comparer);
        foreach (var path in paths)
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                if (!VideoExtensions.Contains(Path.GetExtension(fullPath)) || !File.Exists(fullPath) || !known.Add(fullPath))
                {
                    skipped++;
                    continue;
                }
                items.Add(VideoItem.FromFile(fullPath));
                added++;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException)
            {
                skipped++;
            }
        }
        Refresh();
        Notice = $"{added} 件追加、{skipped} 件スキップ（重複・対象外・アクセス不可等）。メモリ内のみ。保存・送信はしません。";
    }

    public void ReportFilePickerError() =>
        Notice = "ファイル選択を開けませんでした。もう一度試すか、動画をキューへドロップしてください。";

    public void ClearPreview()
    {
        items.Clear();
        Refresh();
        Notice = "プレビューをクリアしました。元の動画ファイルや既存データは変更していません。";
    }

    private void Refresh()
    {
        var previous = SelectedVideo;
        VisibleVideos.Clear();
        foreach (var item in items.Where(x => x.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                     || x.Account.Contains(SearchText, StringComparison.OrdinalIgnoreCase)))
            VisibleVideos.Add(item);
        SelectedVideo = previous is not null && VisibleVideos.Contains(previous) ? previous : VisibleVideos.FirstOrDefault();
        Notify(nameof(QueueSummary));
        Notify(nameof(IsEmpty));
    }
}
