namespace VidUp.Desktop.ViewModels;

// UI-only preview data. Never serialized into a VidUp profile.
public sealed record VideoItem(string Title, string FilePath, string Account, string Template,
    string PublishAt, string Status)
{
    public static VideoItem FromFile(string path) => new(Path.GetFileNameWithoutExtension(path),
        path, "未設定", "未選択", "未設定", "プレビュー追加");
}
