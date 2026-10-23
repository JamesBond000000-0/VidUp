using Newtonsoft.Json;

namespace Drexel.VidUp.Youtube.VideoService.Data
{
    public class YoutubeVideosGetResponse
    {
        //initialized empty because the API omits "items" entirely when there are
        //no results, which left the array null and crashed the callers (BUG-17)
        [JsonProperty(PropertyName = "items")]
        public YoutubeVideosGetResponseVideo[] Items { get; set; } = System.Array.Empty<YoutubeVideosGetResponseVideo>();
    }
}