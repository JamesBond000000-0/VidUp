using NUnit.Framework;
using VidUp.Desktop.ViewModels;

namespace VidUp.Desktop.Tests;

public class ShellViewModelTests
{
    private string folder = null!;

    [SetUp]
    public void SetUp()
    {
        folder = Path.Combine(Path.GetTempPath(), "VidUp-Preview-Tests-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(folder, true);

    private string CreateFile(string name)
    {
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, "not a real video; metadata-only preview");
        return path;
    }

    [Test]
    public void StartsWithClearlyLabelledSamples()
    {
        var model = new ShellViewModel();
        Assert.That(model.VisibleVideos, Has.Count.EqualTo(3));
        Assert.That(model.VisibleVideos.All(x => x.Status == "サンプル"), Is.True);
        Assert.That(model.SelectedVideo, Is.Not.Null);
    }

    [Test]
    public void FiltersByTitleAndAccountAndClearsHiddenSelection()
    {
        var model = new ShellViewModel { SearchText = "日常チャンネル" };
        Assert.That(model.VisibleVideos, Has.Count.EqualTo(1));
        Assert.That(model.SelectedVideo, Is.EqualTo(model.VisibleVideos[0]));
        model.SearchText = "no match";
        Assert.That(model.IsEmpty, Is.True);
        Assert.That(model.SelectedVideo, Is.Null);
        model.SearchText = "";
        Assert.That(model.VisibleVideos, Has.Count.EqualTo(3));
    }

    [Test]
    public void AddsOnlyExistingSupportedFilesAndSkipsDuplicates()
    {
        var video = CreateFile("日本語タイトル.MP4");
        var text = CreateFile("notes.txt");
        var model = new ShellViewModel();
        model.ClearPreview();
        model.AddFiles([video, video, text, Path.Combine(folder, "missing.mp4")]);
        Assert.That(model.VisibleVideos, Has.Count.EqualTo(1));
        Assert.That(model.VisibleVideos[0].Title, Is.EqualTo("日本語タイトル"));
        Assert.That(model.Notice, Does.Contain("1 件追加、3 件スキップ"));
        model.AddFiles([video]);
        Assert.That(model.VisibleVideos, Has.Count.EqualTo(1));
    }

    [Test]
    public void ClearDoesNotDeleteOrModifySourceFile()
    {
        var video = CreateFile("video.mkv");
        var contents = File.ReadAllBytes(video);
        var model = new ShellViewModel();
        model.AddFiles([video]);
        model.ClearPreview();
        Assert.That(model.IsEmpty, Is.True);
        Assert.That(File.ReadAllBytes(video), Is.EqualTo(contents));
        Assert.That(Directory.GetFiles(folder), Has.Length.EqualTo(1));
    }

    [Test]
    public void SearchDoesNotDiscardUnderlyingQueue()
    {
        var model = new ShellViewModel { SearchText = "no match" };
        model.AddFiles([CreateFile("new.mp4")]);
        Assert.That(model.IsEmpty, Is.True);
        model.SearchText = "";
        Assert.That(model.VisibleVideos, Has.Count.EqualTo(4));
    }

    [Test]
    public void NavigationOnlyChangesVisibleSection()
    {
        var model = new ShellViewModel { SelectedSection = "テンプレート" };
        Assert.That(model.IsPlaceholderSection, Is.True);
        Assert.That(model.IsUploadSection, Is.False);
        model.SelectedSection = "アップロード";
        Assert.That(model.IsUploadSection, Is.True);
        Assert.That(model.VisibleVideos, Has.Count.EqualTo(3));
    }
}
