﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using GitterSharp.Configuration;
using GitterSharp.Helpers;
using GitterSharp.Model;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace GitterSharp.Services
{
    public class GitterApiService : IGitterApiService
    {
        #region Fields

        private string _accessToken;

        private readonly string _baseApiAddress = $"{Constants.ApiBaseUrl}{Constants.ApiVersion}";
        private readonly string _baseStreamingApiAddress = $"{Constants.StreamApiBaseUrl}{Constants.ApiVersion}";

        private HttpClient HttpClient
        {
            get
            {
                var httpClient = new HttpClient();
                
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                if (!string.IsNullOrWhiteSpace(_accessToken))
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                return httpClient;
            }
        }

        #endregion


        #region Authentication

        public void TryAuthenticate(string token = null)
        {
            if (!string.IsNullOrWhiteSpace(token))
                _accessToken = token;
        }

        #endregion

        #region User

        public async Task<User> GetCurrentUserAsync()
        {
            string url = _baseApiAddress + "user";
            var users = await HttpClient.GetAsync<IEnumerable<User>>(url);
            return users.FirstOrDefault();
        }

        public async Task<IEnumerable<Organization>> GetOrganizationsAsync(string userId)
        {
            string url = _baseApiAddress + $"user/{userId}/orgs";
            return await HttpClient.GetAsync<IEnumerable<Organization>>(url);
        }

        public async Task<IEnumerable<Repository>> GetRepositoriesAsync(string userId)
        {
            string url = _baseApiAddress + $"user/{userId}/repos";
            return await HttpClient.GetAsync<IEnumerable<Repository>>(url);
        }

        #endregion

        #region Unread Items

        public async Task<UnreadItems> RetrieveUnreadChatMessagesAsync(string userId, string roomId)
        {
            string url = _baseApiAddress + $"user/{userId}/rooms/{roomId}/unreadItems";
            return await HttpClient.GetAsync<UnreadItems>(url);
        }

        public async Task MarkUnreadChatMessagesAsync(string userId, string roomId, IEnumerable<string> messageIds)
        {
            string url = _baseApiAddress + $"user/{userId}/rooms/{roomId}/unreadItems";
            var content = new StringContent("{\"chat\": " + JsonConvert.SerializeObject(messageIds) + "}",
                Encoding.UTF8,
                "application/json");

            await HttpClient.PostAsync(url, content);
        }

        #endregion

        #region Rooms

        public async Task<IEnumerable<Room>> GetRoomsAsync()
        {
            string url = _baseApiAddress + "rooms";
            return await HttpClient.GetAsync<IEnumerable<Room>>(url);
        }

        public async Task<Room> JoinRoomAsync(string roomName)
        {
            string url = _baseApiAddress + "rooms";
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"uri", roomName}
            });

            return await HttpClient.PostAsync<Room>(url, content);
        }

        #endregion

        #region Messages

        public IObservable<Message> GetRealtimeMessages(string roomId)
        {
            string url = _baseStreamingApiAddress + $"rooms/{roomId}/chatMessages";

            return Observable.Using(() => HttpClient,
                client => client.GetStreamAsync(new Uri(url))
                    .ToObservable()
                    .Select(x => Observable.FromAsync(() => StreamHelper.ReadStreamAsync(x)).Repeat())
                    .Concat()
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(JsonConvert.DeserializeObject<Message>));
        }

        public async Task<Message> GetSingleRoomMessageAsync(string roomId, string messageId)
        {
            string url = _baseApiAddress + $"rooms/{roomId}/chatMessages/{messageId}";
            return await HttpClient.GetAsync<Message>(url);
        }

        public async Task<IEnumerable<Message>> GetRoomMessagesAsync(string roomId, int limit = 50, string beforeId = null, string afterId = null, int skip = 0)
        {
            string url = _baseApiAddress + $"rooms/{roomId}/chatMessages?limit={limit}";

            if (!string.IsNullOrWhiteSpace(beforeId))
                url += $"&beforeId={beforeId}";

            if (!string.IsNullOrWhiteSpace(afterId))
                url += $"&afterId={afterId}";

            if (skip > 0)
                url += $"&skip={skip}";

            return await HttpClient.GetAsync<IEnumerable<Message>>(url);
        }

        public async Task<Message> SendMessageAsync(string roomId, string message)
        {
            string url = _baseApiAddress + $"rooms/{roomId}/chatMessages";
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"text", message}
            });

            return await HttpClient.PostAsync<Message>(url, content);
        }

        public async Task<Message> UpdateMessageAsync(string roomId, string messageId, string message)
        {
            string url = _baseApiAddress + $"rooms/{roomId}/chatMessages/{messageId}";
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"text", message}
            });

            return await HttpClient.PutAsync<Message>(url, content);
        }

        #endregion
    }
}