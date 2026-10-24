# VidUp
A template based Youtube uploader

VidUp eases the process of uploading an publishing videos on YouTube, 
especially if you upload videos belonging to a series on a regular basis.
It helps you with adding standard texts, tags and thumbnails to your 
videos, with scheduling videos for publication automatically, with adding
videos to playlists after publication etc. by the use of project templates.
During uploads it prevents standby etc. and shuts down the system after
all uploads finished e.g.

You can find more information here:
https://www.vidup.info/
https://twitter.com/DrexelsVidUp
https://letsplayforum.de/thread/178125-drexel-s-vidup-ein-templatebasierter-youtube-uploader/ (German language sorry!)

## Signing in when building from source

The source code ships with an empty YouTube API client ID and client secret
(`VidUp.Youtube/Credentials.cs`). Only the official release binaries contain
the author's credentials, so a build from source cannot sign in with the
default credentials: Google rejects the sign-in with the error
**"Missing required parameter: client_id"**.

To sign in with a source build, use your own Google API credentials:

1. Go to the [Google Cloud Console](https://console.cloud.google.com/) and
   create a project (or select an existing one).
2. Enable the **YouTube Data API v3** for the project
   ("APIs & Services" → "Library").
3. Configure the **OAuth consent screen** ("APIs & Services" → "OAuth consent
   screen"). If the app stays in "Testing" publishing status, add your own
   Google account under "Test users".
4. Go to "APIs & Services" → "Credentials" → "Create credentials" →
   **"OAuth client ID"**, choose **"Desktop app"** as the application type
   and create the client. Note the **client ID** and **client secret**.
5. In VidUp, open the settings and enable
   **"Use custom YouTube API credentials"**, then enter the client ID and
   client secret from step 4.
6. Sign in again. The browser-based sign-in should now work.

Notes:

- VidUp signs in via a loopback redirect (localhost) with PKCE, which the
  "Desktop app" client type allows by default; no redirect URI needs to be
  registered.
- Keep your client secret private, and keep in mind that uploads count
  against the quota of your own Google Cloud project.
