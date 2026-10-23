using Newtonsoft.Json;

namespace Drexel.VidUp.Youtube.PlaylistItemService.Data
{
    public class YoutubePlaylistItemsGetResponse
    {
        [JsonProperty(PropertyName = "nextPageToken")]
        public string NextPageToken { get; set; }

        [JsonProperty(PropertyName = "prevPageToken")]
        public string PrevPageToken { get; set; }

        //initialized empty because the API omits "items" entirely when there are
        //no results, which left the array null and crashed the callers (BUG-17)
        [JsonProperty(PropertyName = "items")]
        public YoutubePlaylistItemsGetResponsePlaylistItem[] Items { get; set; } = System.Array.Empty<YoutubePlaylistItemsGetResponsePlaylistItem>();
    }
}