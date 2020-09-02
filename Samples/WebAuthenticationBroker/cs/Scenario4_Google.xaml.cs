using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Plus.v1;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using SDKTemplate;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Security.Authentication.Web;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Web.Http;

namespace WebAuthentication
{
    public sealed partial class Scenario4_Google : Page
    {

        MainPage rootPage = MainPage.Current;
        private FileOpenPicker fileOpenPicker;
        private StorageFile pickedVideoFile;
        private string filePath;
        private List<StorageFile> pickedVideoFiles;


        String ClientID = "372426487674-ruspbkhcu5rvlavofl78ib4vfbuvl0nr.apps.googleusercontent.com";
        String ClientSecret = "ML5Iw5aP2uNTGZuouM9Sb06_";
        String Response;
        String Token_Access;
        String Token_Reresh;
        private String ChannelTitle;
        private String Thumbnail_uri;

        public Scenario4_Google()
        {
            this.InitializeComponent();
        }

        private void OutputToken(String TokenUri)
        {
            GoogleReturnedToken.Text = TokenUri;
        }

        private async void Launch_Click(object sender, RoutedEventArgs e)
        {
            if (GoogleCallbackUrl.Text == "")
            {
                rootPage.NotifyUser("Please enter an Callback URL.", NotifyType.StatusMessage);
            }

            try
            {
                String GoogleURL = "https://accounts.google.com/o/oauth2/auth?client_id=" + ClientID + "&redirect_uri=" + Uri.EscapeDataString(GoogleCallbackUrl.Text) + "&response_type=code&scope=" + Uri.EscapeDataString("https://www.googleapis.com/auth/youtube.upload") + "+" + Uri.EscapeDataString("https://www.googleapis.com/auth/youtube") + "+" + Uri.EscapeDataString("https://www.googleapis.com/auth/youtubepartner");

                Uri StartUri = new Uri(GoogleURL);
                // When using the desktop flow, the success code is displayed in the html title of this end uri
                Uri EndUri = new Uri("https://accounts.google.com/o/oauth2/approval");

                rootPage.NotifyUser("Navigating to: " + GoogleURL, NotifyType.StatusMessage);

                WebAuthenticationResult WebAuthenticationResult = await WebAuthenticationBroker.AuthenticateAsync(WebAuthenticationOptions.UseTitle, StartUri, EndUri);
                if (WebAuthenticationResult.ResponseStatus == WebAuthenticationStatus.Success)
                {
                    OutputToken(WebAuthenticationResult.ResponseData.ToString());
                    var autorizationCode = WebAuthenticationResult.ResponseData.Substring(13);
                    var pairs = new Dictionary<string, string>();
                    pairs.Add("code", autorizationCode);
                    pairs.Add("client_id", ClientID);
                    pairs.Add("client_secret", ClientSecret);
                    pairs.Add("redirect_uri", "urn:ietf:wg:oauth:2.0:oob");
                    pairs.Add("grant_type", "authorization_code");

                    var formContent = new HttpFormUrlEncodedContent(pairs);

                    var client = new Windows.Web.Http.HttpClient();
                    var httpResponseMessage = await client.PostAsync(new Uri("https://accounts.google.com/o/oauth2/token"), formContent);
                    Response = await httpResponseMessage.Content.ReadAsStringAsync();
                }
                else if (WebAuthenticationResult.ResponseStatus == WebAuthenticationStatus.ErrorHttp)
                {
                    OutputToken("HTTP Error returned by AuthenticateAsync() : " + WebAuthenticationResult.ResponseErrorDetail.ToString());
                }
                else
                {
                    OutputToken("Error returned by AuthenticateAsync() : " + WebAuthenticationResult.ResponseStatus.ToString());
                }
                int pFrom = Response.IndexOf("\"access_token\": ") + "\"access_token\": ".Length;
                int pTo = Response.LastIndexOf("\"expires_in\":");

                this.Token_Access = Response.Substring(pFrom, pTo - pFrom).Replace("\"", "").Replace(",", "").Replace("\n", "").Trim();

                pFrom = Response.IndexOf("\"refresh_token\": ") + "\"refresh_token\": ".Length;
                pTo = Response.LastIndexOf("\"scope\"");
                this.Token_Reresh = Response.Substring(pFrom, pTo - pFrom).Replace("\"", "").Replace(",", "").Replace("\n", "").Trim();

                await Task.Run(ShowBasicUserInfo);
            }
            catch (Exception Error)
            {
                rootPage.NotifyUser(Error.Message, NotifyType.ErrorMessage);
            }

            if (this.ChannelTitle != "" || this.ChannelTitle != null)
                LoginNameTextBlock.Text = ChannelTitle;

            if (this.Thumbnail_uri != "" || this.Thumbnail_uri != null)
            {
                Image img = new Image();
                img.Source = new BitmapImage(new Uri(this.Thumbnail_uri));
                ImgThumbnail.Source = img.Source;
            }

        }

        private async void SubirVideoAsync(object sender, RoutedEventArgs e)
        {
            fileOpenPicker = GetFileOpenPicker(new List<string> { ".avi", ".mp4", ".wmv" }, PickerLocationId.VideosLibrary);
            this.pickedVideoFile = await fileOpenPicker.PickSingleFileAsync();
            this.pickedVideoFiles = new List<StorageFile> { pickedVideoFile };
            this.filePath = pickedVideoFiles[0].Path;

            try
            {
                await Task.Run(UploadTheFile);
            }
            catch (AggregateException ex)
            {
                Debug.Write("Error: " + ex.Message);
            }
        }

        public static FileOpenPicker GetFileOpenPicker(List<string> fileTypes, PickerLocationId locationId = PickerLocationId.DocumentsLibrary, PickerViewMode viewMode = PickerViewMode.Thumbnail)
        {
            FileOpenPicker fileOpenPicker = new FileOpenPicker
            {
                SuggestedStartLocation = locationId,
                ViewMode = viewMode
            };
            fileTypes.ForEach((fileType) => fileOpenPicker.FileTypeFilter.Add(fileType));
            return fileOpenPicker;
        }

        private async Task ShowBasicUserInfo()
        {
            GoogleCredential cred = GoogleCredential.FromAccessToken(Token_Access);

            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = cred,
                ApplicationName = "MyYouTubeTest"
            });

            var listRequest = youtubeService.Channels.List("id");
            listRequest.Mine = true;
            var searchListResponse = listRequest.Execute();
            string channelId = searchListResponse.Items[0].Id;
            Debug.Write(channelId);

            listRequest = youtubeService.Channels.List("snippet");
            listRequest.Id = channelId;
            searchListResponse = listRequest.Execute();
            this.ChannelTitle = searchListResponse.Items[0].Snippet.Title;
            Debug.Write(ChannelTitle);
            this.Thumbnail_uri = searchListResponse.Items[0].Snippet.Thumbnails.Default__.Url;
            Debug.Write(Thumbnail_uri);

        }

        private async Task UploadTheFile()
        {
            //            string[] scopes = new string[] {
            //                PlusService.Scope.PlusLogin,
            //                PlusService.Scope.UserinfoEmail,
            //                PlusService.Scope.UserinfoProfile
            //};

            //            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            //            {
            //                ClientSecrets = new ClientSecrets
            //                {
            //                    ClientId = this.ClientID,
            //                    ClientSecret = this.ClientSecret
            //                },
            //                Scopes = scopes,
            //                DataStore = new FileDataStore("Store")
            //            });

            //            var token = new TokenResponse
            //            {
            //                AccessToken = Token_Access,
            //                RefreshToken = Token_Reresh
            //            };

            //            var credential = new UserCredential(flow, Environment.UserName, token);

            GoogleCredential cred = GoogleCredential.FromAccessToken(Token_Access);

            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = cred,
                ApplicationName = "MyYouTubeTest"
            });


            //String CLIENT_ID = "648437414732-vhfugp8lu8q45qvupr9dfbbri27b6t2s.apps.googleusercontent.com";
            //String CLIENT_SECRET = "whgv3IalKr3BCaL1URwkb0YE";
            //var youtubeService = AuthenticateOauth(CLIENT_ID, CLIENT_SECRET, "user");

            var video = new Video();
            video.Snippet = new VideoSnippet();
            video.Snippet.Title = "Default Video Title";
            video.Snippet.Description = "Default Video Description";
            video.Snippet.Tags = new string[] { "tag1", "tag2" };
            video.Snippet.CategoryId = "22"; // See https://developers.google.com/youtube/v3/docs/videoCategories/list
            video.Status = new VideoStatus();
            video.Status.PrivacyStatus = "unlisted"; // or "private" or "public"
            //var filePath = this.filePath; // Replace with path to actual movie file.





            using (var fileStream = (await pickedVideoFiles[0].OpenAsync(Windows.Storage.FileAccessMode.Read)).AsStream())
            {
                var videosInsertRequest = youtubeService.Videos.Insert(video, "snippet,status", fileStream, "video/*");
                videosInsertRequest.ProgressChanged += videosInsertRequest_ProgressChanged;
                videosInsertRequest.ResponseReceived += videosInsertRequest_ResponseReceived;

                await videosInsertRequest.UploadAsync();
            }
        }

        void videosInsertRequest_ProgressChanged(IUploadProgress progress)
        {
            switch (progress.Status)
            {
                case UploadStatus.Uploading:
                    Debug.Write("{0} bytes sent.", progress.BytesSent.ToString());
                    break;

                case UploadStatus.Failed:
                    Debug.Write("An error prevented the upload from completing.\n{0}", progress.Exception.ToString());
                    break;
            }
        }

        void videosInsertRequest_ResponseReceived(Video video)
        {
            Debug.Write("Video id '{0}' was successfully uploaded.", video.Id);
        }
    }
}
