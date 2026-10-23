using Drexel.VidUp.Youtube.PlaylistItemService.Data;
using Drexel.VidUp.Youtube.PlaylistService.Data;
using Drexel.VidUp.Youtube.VideoService.Data;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Drexel.VidUp.Test
{
    //BUG-17: the YouTube API omits "items" entirely when a result set is empty.
    //The response DTOs initialize the array empty so deserializing such a
    //response no longer produces a null array for the callers to crash on.
    //(An explicit "items": null in the JSON still yields null, which is why the
    //services additionally guard their dereferences.)
    [TestFixture]
    public class ApiResponseNullTests
    {
        [Test]
        public void PlaylistsResponse_WithoutItems_ItemsIsEmpty()
        {
            YoutubePlaylistsGetResponse response = JsonConvert.DeserializeObject<YoutubePlaylistsGetResponse>(
                "{\"kind\":\"youtube#playlistListResponse\",\"pageInfo\":{\"totalResults\":0}}");

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Items, Is.Not.Null);
            Assert.That(response.Items.Length, Is.EqualTo(0));
        }

        [Test]
        public void PlaylistItemsResponse_WithoutItems_ItemsIsEmpty()
        {
            YoutubePlaylistItemsGetResponse response = JsonConvert.DeserializeObject<YoutubePlaylistItemsGetResponse>(
                "{\"kind\":\"youtube#playlistItemListResponse\",\"pageInfo\":{\"totalResults\":0}}");

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Items, Is.Not.Null);
            Assert.That(response.Items.Length, Is.EqualTo(0));
        }

        [Test]
        public void VideosResponse_WithoutItems_ItemsIsEmpty()
        {
            YoutubeVideosGetResponse response = JsonConvert.DeserializeObject<YoutubeVideosGetResponse>(
                "{\"kind\":\"youtube#videoListResponse\",\"pageInfo\":{\"totalResults\":0}}");

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Items, Is.Not.Null);
            Assert.That(response.Items.Length, Is.EqualTo(0));
        }

        [Test]
        public void PlaylistsResponse_WithItems_ParsesItems()
        {
            YoutubePlaylistsGetResponse response = JsonConvert.DeserializeObject<YoutubePlaylistsGetResponse>(
                "{\"items\":[{\"id\":\"PL123\",\"snippet\":{\"title\":\"My Playlist\"}}]}");

            Assert.That(response.Items.Length, Is.EqualTo(1));
            Assert.That(response.Items[0].Id, Is.EqualTo("PL123"));
            Assert.That(response.Items[0].Snippet.Title, Is.EqualTo("My Playlist"));
        }
    }
}
