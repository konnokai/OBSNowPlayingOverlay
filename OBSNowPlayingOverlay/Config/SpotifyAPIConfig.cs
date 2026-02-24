using SpotifyAPI.Web;

namespace OBSNowPlayingOverlay.Config
{
    public class SpotifyAPIConfig
    {
        public AuthorizationCodeTokenResponse? AuthorizationCodeToken { get; set; } = null;
        public string ClientId { get; set; } = "";
        public string ClientSecret { get; set; } = "";
        public string UserLogin { get; set; } = "";
        public bool AutoLogin { get; set; } = true;
    }
}
