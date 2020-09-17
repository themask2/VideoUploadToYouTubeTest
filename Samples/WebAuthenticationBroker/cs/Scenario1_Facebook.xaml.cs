//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using Facebook;
using winsdkfb;
using winsdkfb.Graph;
using Newtonsoft.Json;
using SDKTemplate;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Security.Authentication.Web;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;
using Windows.Foundation.Collections;
using WabCS;
using System.IO;
using System.Net.Http;

namespace WebAuthentication
{
    public sealed partial class Scenario1_Facebook : Page
    {
        private MainPage rootPage = MainPage.Current;
        bool authzInProgress = false;
        private String fb_token;
        private FileOpenPicker fileOpenPicker;
        private StorageFile pickedVideoFile;
        private List<StorageFile> pickedVideoFiles;
        private string filePath;
        private string userID;
        private string Page_token;
        private string PageId;

        public Scenario1_Facebook()
        {
            this.InitializeComponent();

            // Use these SIDs to register the app with Facebook.
            WindowsStoreSidTextBlock.Text = WebAuthenticationBroker.GetCurrentApplicationCallbackUri().Host;
            Debug.Write(WindowsStoreSidTextBlock.Text + "\n");
            WindowsPhoneStoreSidTextBlock.Text = "feaebe20-b974-4857-a51c-3525e4dfe2a8"; // copied from Package.appxmanifest
        }

        private async void Launch_Click(object sender, RoutedEventArgs e)
        {
            if (authzInProgress)
            {
                return;
            }

            FacebookReturnedToken.Text = "";
            FacebookUserName.Text = "";

            if (String.IsNullOrEmpty(FacebookClientID.Text))
            {
                rootPage.NotifyUser("Please enter a Client ID.", NotifyType.StatusMessage);
                return;
            }

            Uri callbackUri;
            if (!Uri.TryCreate(FacebookCallbackUrl.Text, UriKind.Absolute, out callbackUri))
            {
                rootPage.NotifyUser("Please enter a Callback URL.", NotifyType.StatusMessage);
                return;
            }

            Uri facebookStartUri = new Uri($"https://www.facebook.com/dialog/oauth?client_id={Uri.EscapeDataString(FacebookClientID.Text)}&redirect_uri={Uri.EscapeDataString(callbackUri.AbsoluteUri)}&display=popup&response_type=token&scope=email,user_birthday,publish_video,user_videos,pages_show_list,pages_read_engagement,pages_manage_posts");

            rootPage.NotifyUser($"Navigating to {facebookStartUri}", NotifyType.StatusMessage);

            authzInProgress = true;
            try
            {
                WebAuthenticationResult WebAuthenticationResult = await WebAuthenticationBroker.AuthenticateAsync(WebAuthenticationOptions.None, facebookStartUri, callbackUri);
                if (WebAuthenticationResult.ResponseStatus == WebAuthenticationStatus.Success)
                {
                    FacebookReturnedToken.Text = WebAuthenticationResult.ResponseData;
                    Debug.Write(WebAuthenticationResult.ResponseData + "\n");
                    await GetFacebookUserNameAsync(WebAuthenticationResult.ResponseData);
                }
                else if (WebAuthenticationResult.ResponseStatus == WebAuthenticationStatus.ErrorHttp)
                {
                    FacebookReturnedToken.Text = $"HTTP error: {WebAuthenticationResult.ResponseErrorDetail}";
                }
                else
                {
                    FacebookReturnedToken.Text = $"Error: {WebAuthenticationResult.ResponseStatus}";
                }

            }
            catch (Exception Error)
            {
                rootPage.NotifyUser(Error.Message, NotifyType.ErrorMessage);
            }

            authzInProgress = false;
        }

        /// <summary>
        /// This function extracts access_token from the response returned from web authentication broker
        /// and uses that token to get user information using facebook graph api. 
        /// </summary>
        /// <param name="responseData">responseData returned from AuthenticateAsync result.</param>
        private async Task GetFacebookUserNameAsync(string responseData)
        {
            var decoder = new WwwFormUrlDecoder(responseData);
            var error = decoder.TryGetValue("error");
            if (error != null)
            {
                FacebookUserName.Text = $"Error: {error}";
                return;
            }

            // You can store access token locally for further use.
            string access_token = decoder[0].Value;
            this.fb_token = decoder[0].Value;
            string expires_in = decoder.TryGetValue("expires_in"); // expires_in is optional

            Windows.Web.Http.HttpClient httpClient = new Windows.Web.Http.HttpClient();
            Uri uri = new Uri($"https://graph.facebook.com/me?fields=id,name,picture,birthday&access_token={Uri.EscapeDataString(access_token)}");

            HttpGetStringResult result = await httpClient.TryGetStringAsync(uri);
            if (result.Succeeded)
            {
                Windows.Data.Json.JsonObject userInfo = Windows.Data.Json.JsonObject.Parse(result.Value).GetObject();
                FacebookUserName.Text = userInfo.GetNamedString("name");
                this.userID = userInfo.GetNamedString("id");
            }
            else
            {
                FacebookUserName.Text = "Error contacting Facebook";
            }

            Windows.Web.Http.HttpClient httpClient2 = new Windows.Web.Http.HttpClient();
            Uri uri2 = new Uri($"https://graph.facebook.com/{this.userID}/accounts?fields=name,access_token&access_token={this.fb_token}");

            HttpGetStringResult myresult = await httpClient.TryGetStringAsync(uri2);
            if (myresult.Succeeded)
            {
                Windows.Data.Json.JsonObject userInfo2 = Windows.Data.Json.JsonObject.Parse(myresult.Value).GetObject();
                this.Page_token = userInfo2["data"].GetArray()[0].GetObject().GetNamedString("access_token");

            } else
            {
                Debug.Write("man ta osso!");
            }

            Windows.Web.Http.HttpClient httpClient3 = new Windows.Web.Http.HttpClient();
            Uri uri3 = new Uri($"https://graph.facebook.com/{this.userID}/accounts?access_token={this.fb_token}");

            HttpGetStringResult result3 = await httpClient.TryGetStringAsync(uri3);
            if (result3.Succeeded)
            {
                Windows.Data.Json.JsonObject userInfo = Windows.Data.Json.JsonObject.Parse(result3.Value).GetObject();
                this.PageId = userInfo["data"].GetArray()[0].GetObject().GetNamedString("id");
            }
            else
            {
                FacebookUserName.Text = "Error contacting Facebook";
            }
        }

        private async void SubirFBVideoAsync(object sender, RoutedEventArgs e)
        {
            //FBSession sess = FBSession.ActiveSession;
            //sess.FBAppId = "634395434138810";
            //sess.WinAppId = WebAuthenticationBroker.GetCurrentApplicationCallbackUri().Host;
            ////FBSession sess = FBSession.ActiveSession;

            //// Add permissions required by the app
            //List<String> permissionList = new List<String>();
            //permissionList.Add("public_profile");
            //permissionList.Add("user_friends");
            //permissionList.Add("user_likes");
            //permissionList.Add("user_groups");
            //permissionList.Add("user_location");
            //permissionList.Add("user_photos");
            //permissionList.Add("publish_actions");
            //FBPermissions permissions = new FBPermissions(permissionList);

            //// Login to Facebook
            //FBResult resultlogin = await sess.LoginAsync(permissions);

            //if (resultlogin.Succeeded)
            //{
            //    //Login successful
            //    Debug.Write("Failed ok!!");
            //}
            //else
            //{
            //    Debug.Write("Failed login!!");
            //}

            //fileOpenPicker = GetFileOpenPicker(new List<string> { ".avi", ".mp4", ".wmv" }, PickerLocationId.VideosLibrary);
            //this.pickedVideoFile = await fileOpenPicker.PickSingleFileAsync();
            //this.pickedVideoFiles = new List<StorageFile> { pickedVideoFile };
            //this.filePath = pickedVideoFiles[0].Path;

            try
            {
                //FacebookClient app = new FacebookClient(this.fb_token);
                //dynamic parameters2 = new ExpandoObject();
                //parameters2.message = "This is a test message that has been published by the Facebook C# SDK on Codeplex. " + DateTime.UtcNow.Ticks.ToString();

                //dynamic res = await app.PostTaskAsync("https://graph.facebook.com/" + this.userID + "/videos", parameters2);

                //while (res.Status == TaskStatus.WaitingForActivation);

                //if (res.Status == TaskStatus.Faulted)
                //{
                //    Debug.Write("Deu ruim!");
                //}

                var fbp = new FacebookClient(this.Page_token);

                dynamic parameters = new ExpandoObject();
                parameters.source = new FacebookMediaObject { ContentType = "multipart/form-data", FileName = "123.mp4" }.SetValue(System.IO.File.ReadAllBytes("./123.mp4"));
                parameters.title = "Release New Video";
                parameters.description = "meu teste em release";
                string url = "https://graph-video.facebook.com/v8.0/" + this.PageId + "/videos";
                dynamic result = fbp.PostTaskAsync(url, parameters);

                // Wait for activation
                while (result.Status == TaskStatus.WaitingForActivation) ;

                // Check if it succeded or failed
                if (result.Status == TaskStatus.RanToCompletion)
                {
                    //string photoId = result.Result["post_id"];//post_id
                    //if (null != postData.TaggedUserEmail)
                    //{
                    //    bool result = TagPhoto(photoId, GetUserID(postData.TaggedUserEmail).Id);
                    //}
                    Debug.Write("ok!!!: ");
                }
                else if (result.Status == TaskStatus.Faulted)
                {
                    //CommonEventsHelper.WriteToEventLog(string.Format("Error posting message - {0}", (publishResponse.Exception as Exception).Message), System.Diagnostics.EventLogEntryType.Error);
                    throw (new InvalidOperationException((((Exception)result.Exception).InnerException).Message, (Exception)result.Exception));
                }

                //----------------------------------------------------------------------------------------------

                //--https://graph-video.facebook.com/
                //string UserAccessToken = this.fb_token;
                //string message = "having fun";
                //string title = "hi";
                //string filePath = "./123.mp4";
                //if (!string.IsNullOrEmpty(UserAccessToken))
                //{
                //    string FileUploadUrl = string.Format("https://graph.facebook.com/" + this.userID + "/videos?title={0}&description={1}&access_token={2}", HttpUtility.UrlEncode(title), HttpUtility.UrlEncode(message), UserAccessToken);
                //    WebClient uploadClient = new WebClient();
                //    byte[] returnBytes = uploadClient.UploadFile(FileUploadUrl, filePath);
                //}
                //return View();

                //---------------------------------------------------------------------------------------------------

                //var fop = new FileOpenPicker();
                //fop.ViewMode = PickerViewMode.Thumbnail;
                //fop.SuggestedStartLocation = PickerLocationId.VideosLibrary;
                //fop.FileTypeFilter.Add(".mp4");

                //var storageFile = await fop.PickSingleFileAsync();
                //var stream = await storageFile.OpenReadAsync();
                //var mediaStream = new FBMediaStream(storageFile.Name, stream);

                //FBSession sess = FBSession.ActiveSession;
                //if (sess.LoggedIn)
                //{
                //    var user = sess.User;
                //    var parameters = new PropertySet();
                //    parameters.Add("title", "Test video");
                //    parameters.Add("source", mediaStream);
                //    string path = "/" + user.Id + "/videos";

                //    var factory = new FBJsonClassFactory(s => {
                //        return JsonConvert.DeserializeObject<FBReturnObject>(s);
                //    });

                //    var singleValue = new FBSingleValue(path, parameters, factory);
                //    var result = await singleValue.PostAsync();
                //    if (result.Succeeded)
                //    {
                //        var response = result.Object as FBReturnObject;
                //    }
                //}




                //Upload(this.fb_token);
            }
            catch (Exception ex)
            {

                throw;
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

        private static void Upload(string token)
        {
            //using (var client = new System.Net.Http.HttpClient())
            //{
            //    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);




            //    using (var content = new System.Net.Http.MultipartFormDataContent())
            //    {
            //        var path = @"./123.mp4";



            //        string assetName = Path.GetFileName(path);



            //        FileStream fs = File.OpenRead(path);



            //        var streamContent = new System.Net.Http.StreamContent(fs);
            //        streamContent.Headers.Add("Content-Type", "application/octet-stream");
            //        streamContent.Headers.Add("Content-Disposition", "form-data; name=\"file\"; filename=\"" + Path.GetFileName(path) + "\"");
            //        content.Add(streamContent, "file", Path.GetFileName(path));




            //        Task<System.Net.Http.HttpResponseMessage> message = client.PostAsync("https://graph.facebook.com/v8.0/me/videos", content);



            //        var input = message.Result.Content.ReadAsStringAsync();
            //        Console.WriteLine(input.Result);
            //        Console.Read();
            //    }
            //}
            //using (var httpClient = new Windows.Web.Http.HttpClient())
            //{
            //    httpClient.BaseAddress = new Uri("https://graph.facebook.com/");

            //    var parametters = new Dictionary<string, string>
            //    {
            //        { "access_token", FB_ACCESS_TOKEN },
            //        { "message", message }
            //    };
            //    var encodedContent = new FormUrlEncodedContent(parametters);

            //    var result = await httpClient.PostAsync($"{FB_PAGE_ID}/feed", encodedContent);
            //    var msg = result.EnsureSuccessStatusCode();
            //    return await msg.Content.ReadAsStringAsync();
            //}
        }
    }
}
